// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.RegularExpressions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Providers;

// A native menu item's ElementHandle.NativeHandle: the #32768 popup showing it, the HMENU, and its position there — deliberately not an IntPtr, so window-oriented input helpers never mistake it for a window to foreground.
internal sealed record NativeMenuItem(IntPtr PopupWindow, IntPtr Menu, int Position);

// Last-resort provider using EnumChildWindows P/Invoke — works on apps with no accessibility tree, matching by title or class name; under a #32768 popup scope it finds the native menu's items instead.
public sealed class Win32Provider : IElementProvider, IElementIdentity
{
    public string ProviderName => "win32";

    // ---- IElementProvider implementation (public) + supporting private helpers ----

    // Finds the first child window under root matching the locator by title (Name) or class name (ClassName); null if not found, strategy unsupported, or root is null.
    private Task<ElementHandle?> FindFirstAsync(IntPtr? root, Locator locator, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            if (root is null) return null;

            ElementHandle? result = null;
            NativeMethods.EnumChildWindows(root.Value, (windowHandle, _) =>
            {
                if (!Matches(windowHandle, locator))
                    return true;
                result = WrapWindowHandle(windowHandle);
                return false;
            }, IntPtr.Zero);
            return result;
        }, ct);
    }

    // Finds all child windows under root matching the locator by title (Name) or class name (ClassName); empty if none match or root is null.
    private Task<IReadOnlyList<ElementHandle>> FindAllAsync(IntPtr? root, Locator locator, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            if (root is null) return (IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>();

            var results = new List<ElementHandle>();
            NativeMethods.EnumChildWindows(root.Value, (windowHandle, _) =>
            {
                if (Matches(windowHandle, locator))
                    results.Add(WrapWindowHandle(windowHandle));
                return true;
            }, IntPtr.Zero);
            return (IReadOnlyList<ElementHandle>)results;
        }, ct);
    }

    // Finds the first child window of the session root matching the locator, searching a RootElement (a popup) as a scope so a #32768's menu items are read; null if not found or strategy unsupported, StaleElementError if the RootElement has closed.
    public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        session.ThrowIfRootClosed();
        return session.RootElement is { } root ? FindScopedElementAsync(locator, session, root, ct) : FindFirstAsync(session.RootWindowHandle, locator, ct);
    }

    // Finds all child windows of the session root matching the locator, searching a RootElement as FindElementAsync does; empty if none match, StaleElementError if the RootElement has closed.
    public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        session.ThrowIfRootClosed();
        return session.RootElement is { } root ? FindScopedElementsAsync(locator, session, root, ct) : FindAllAsync(session.RootWindowHandle, locator, ct);
    }

    // Finds the first child window (or, under a #32768 popup, menu item) under scope's hwnd matching the locator; null if none match or scope's NativeHandle isn't an IntPtr (came from a different provider), StaleElementError if scope is gone.
    public async Task<ElementHandle?> FindScopedElementAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default)
    {
        if (ValidScopeHandle(scope, session) is not { } hwnd)
            return null;
        if (NativeMethods.IsPopupMenuWindow(hwnd))
            return (await FindMenuItemsAsync(hwnd, locator, ct).ConfigureAwait(false)).FirstOrDefault();
        return await FindFirstAsync(hwnd, locator, ct).ConfigureAwait(false);
    }

    // Finds all child windows (or, under a #32768 popup, menu items) under scope's hwnd matching the locator; empty if none match or scope came from a different provider, StaleElementError if scope is gone.
    public Task<IReadOnlyList<ElementHandle>> FindScopedElementsAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default)
    {
        if (ValidScopeHandle(scope, session) is not { } hwnd)
            return Task.FromResult<IReadOnlyList<ElementHandle>>([]);
        // A #32768 popup has no child windows; its items live in the HMENU it displays.
        if (NativeMethods.IsPopupMenuWindow(hwnd))
            return FindMenuItemsAsync(hwnd, locator, ct);
        return FindAllAsync(hwnd, locator, ct);
    }

    // Returns element's parent window wrapped, or for a native menu item the #32768 popup showing it; null for a top-level window or an element from a different provider.
    public Task<ElementHandle?> FindParentAsync(ElementHandle element, AppSession session, CancellationToken ct = default) => Task.Run(() => element.NativeHandle switch
    {
        NativeMenuItem item => WrapWindowHandle(item.PopupWindow),
        IntPtr windowHandle when NativeMethods.GetParentWindow(windowHandle) is var parent && parent != IntPtr.Zero => WrapWindowHandle(parent),
        _ => (ElementHandle?)null,
    }, ct);

    // Returns scope's hwnd, or null if it came from a different provider; throws StaleElementError if it no longer belongs to the process that owned it when found (destroyed, possibly recycled by the OS for an unrelated window) — the session's process for a handle that didn't record one.
    private static IntPtr? ValidScopeHandle(ElementHandle scope, AppSession session)
    {
        if (scope.NativeHandle is not IntPtr hwnd) return null;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        // A window view can be rooted at a helper process's window, so the owner recorded at find time, not the session's process, is what must still match.
        var expectedPid = scope.ProcessId != 0 ? scope.ProcessId : session.ProcessId;
        if ((int)pid != expectedPid)
            throw new StaleElementError(scope, $"Scope window \"{scope.Name}\" no longer belongs to the process that owned it.");
        return hwnd;
    }

    // Snapshots all child windows of the session root (a RootElement's hwnd when set) into a flat list of children under a single root snapshot; empty for a RootElement another provider wrapped.
    public Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            session.ThrowIfRootClosed();
            if (session.RootElement is { NativeHandle: not IntPtr })
                return (IReadOnlyList<ElementSnapshot>)[];
            var rootWindow = session.RootElement is { NativeHandle: IntPtr hwnd } ? hwnd : session.RootWindowHandle;

            var children = new List<ElementSnapshot>();
            NativeMethods.EnumChildWindows(rootWindow, (windowHandle, _) =>
            {
                children.Add(BuildSnapshot(windowHandle, []));
                return true;
            }, IntPtr.Zero);

            var root = BuildSnapshot(rootWindow, children);
            return (IReadOnlyList<ElementSnapshot>)[root];
        }, ct);
    }

    // True when both handles are windows with the same hwnd.
    bool IElementIdentity.IsSameElement(ElementHandle a, ElementHandle b) =>
        a.NativeHandle is IntPtr x && b.NativeHandle is IntPtr y && x == y;

    // Snapshots the tree and returns it with the Id of target's node; null if target isn't a window of this tree (a native menu item has no hwnd).
    async Task<(IReadOnlyList<ElementSnapshot> Tree, string TargetId)?> IElementIdentity.SnapshotLocatingAsync(ElementHandle target, AppSession session, CancellationToken ct)
    {
        if (target.NativeHandle is not IntPtr hwnd)
            return null;
        var tree = await SnapshotTreeAsync(session, ct).ConfigureAwait(false);
        var id = $"hwnd:{hwnd}";
        return tree.DescendantsAndSelf().Any(n => n.Id == id) ? (tree, id) : null;
    }

    // ---- Native #32768 menus ----

    // Lists a #32768 popup's items matching locator, read from the HMENU it displays; items with no text (separators, most owner-drawn items) are skipped; empty once the popup has closed.
    private Task<IReadOnlyList<ElementHandle>> FindMenuItemsAsync(IntPtr popupWindow, Locator locator, CancellationToken ct) => Task.Run(() =>
    {
        var menu = NativeMethods.GetPopupMenuHandle(popupWindow);
        if (menu == IntPtr.Zero)
            return (IReadOnlyList<ElementHandle>)[];

        NativeMethods.GetWindowThreadProcessId(popupWindow, out var processId);
        var items = new List<ElementHandle>();
        var count = NativeMethods.GetMenuItemCount(menu);
        for (var position = 0; position < count; position++)
            if (NativeMethods.ReadMenuItem(popupWindow, menu, position) is { RawText: { } rawText } info && MatchesMenuItem(MenuItemDisplayName(rawText), locator))
                items.Add(WrapMenuItem(popupWindow, menu, position, info, (int)processId));
        return items;
    }, ct);

    // Matches a menu item's display name by Name (substring, case-insensitive, like window titles) or any item by ControlType "MenuItem".
    private static bool MatchesMenuItem(string name, Locator locator) => locator.Strategy switch
    {
        LocatorStrategy.Name => name.Contains(locator.Value, StringComparison.OrdinalIgnoreCase),
        LocatorStrategy.ControlType => locator.Value.Equals("MenuItem", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    // Wraps one native menu item as an ElementHandle whose NativeHandle (NativeMenuItem) lets ElementInputHelpers re-read it live.
    private ElementHandle WrapMenuItem(IntPtr popupWindow, IntPtr menu, int position, NativeMethods.NativeMenuItemInfo info, int processId) =>
        new($"hmenu:{menu}:{position}", "win32", new NativeMenuItem(popupWindow, menu, position))
        {
            Name = MenuItemDisplayName(info.RawText!),
            ControlType = "MenuItem",
            BoundingRect = info.Bounds,
            IsEnabled = info.IsEnabled,
            ProcessId = processId,
            Provider = this,
        };

    // Converts raw menu text to the Name UIA3 reports for the same item, so one itemName works on both paths: drops the tab-separated shortcut and lone & mnemonic markers, but keeps && as-is (UIA3's menu proxy does).
    internal static string MenuItemDisplayName(string rawText)
    {
        var tab = rawText.IndexOf('\t');
        var label = tab >= 0 ? rawText[..tab] : rawText;
        // Removes an & only when neither neighbor is also &: "&Open" -> "Open", "Save && Exit" unchanged.
        return Regex.Replace(label, "(?<!&)&(?!&)", "");
    }

    // ---- Private helpers ----

    // Returns true when the windowHandle's title (Name strategy) or class name (ClassName strategy) matches the locator value.
    private static bool Matches(IntPtr windowHandle, Locator locator) => locator.Strategy switch
    {
        LocatorStrategy.Name => NativeMethods.GetWindowTitle(windowHandle).Contains(locator.Value, StringComparison.OrdinalIgnoreCase),
        LocatorStrategy.ClassName => NativeMethods.GetWindowClassName(windowHandle).Equals(locator.Value, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    // Wraps an HWND as an opaque ElementHandle with Win32-derived metadata; internal so context-menu detection can wrap a native #32768 popup as a scope for FindScopedElementsAsync.
    internal ElementHandle WrapWindowHandle(IntPtr windowHandle)
    {
        var (rect, rectOk) = GetRect(windowHandle);
        NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        return new($"hwnd:{windowHandle}", "win32", windowHandle)
        {
            Name = NativeMethods.GetWindowTitle(windowHandle),
            ClassName = NativeMethods.GetWindowClassName(windowHandle),
            BoundingRect = rect,
            IsEnabled = NativeMethods.IsWindowEnabled(windowHandle),
            // Providers never throw, so a failed GetWindowRect is signaled as IsOffscreen=true rather than a silently-zeroed rect.
            IsOffscreen = rectOk ? null : true,
            // Recorded so ValidScopeHandle can tell this window's handle being recycled for another process apart from a window owned by a helper process all along.
            ProcessId = (int)processId,
            Provider = this,
        };
    }

    // Builds an ElementSnapshot for windowHandle with the given children list.
    private static ElementSnapshot BuildSnapshot(IntPtr windowHandle, IReadOnlyList<ElementSnapshot> children) =>
        new($"hwnd:{windowHandle}", NativeMethods.GetWindowTitle(windowHandle), null, NativeMethods.GetWindowClassName(windowHandle), null, GetRect(windowHandle).Rect, children);

    // Returns windowHandle's current bounding rectangle in physical screen coordinates, and whether GetWindowRect actually succeeded — a stale/closed handle returns false with a zeroed rect.
    internal static (Rect Rect, bool Success) GetRect(IntPtr windowHandle)
    {
        var ok = NativeMethods.GetWindowRect(windowHandle, out var rect);
        return (new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), ok);
    }
}
