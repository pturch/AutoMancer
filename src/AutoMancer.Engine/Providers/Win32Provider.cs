// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Providers;

// Last-resort provider using EnumChildWindows P/Invoke — works on apps with no accessibility tree, matching by title or class name.
public sealed class Win32Provider : IElementProvider
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

    // Finds the first child window of the session root matching the locator; null if not found or strategy unsupported.
    public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
        FindFirstAsync(session.RootWindowHandle, locator, ct);

    // Finds all child windows of the session root matching the locator; empty if none match.
    public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
        FindAllAsync(session.RootWindowHandle, locator, ct);

    // Finds the first child window under scope's hwnd matching the locator; null if none match, scope's NativeHandle isn't an IntPtr (came from a different provider)
    // In Win32 its possible the hwnd no longer belongs to this session's process (destroyed and recycled by the OS for an unrelated window).
    public Task<ElementHandle?> FindScopedElementAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
        FindFirstAsync(ValidScopeHandle(scope, session), locator, ct);

    // Finds all child windows under scope's hwnd matching the locator; empty if none match or scope came from a different provider
    // In Win32 its possible the hwnd no longer belongs to this session's process (destroyed and recycled by the OS for an unrelated window).

    public Task<IReadOnlyList<ElementHandle>> FindScopedElementsAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
        FindAllAsync(ValidScopeHandle(scope, session), locator, ct);

    // Returns scope's hwnd only if it's still owned by session's own process.
    private static IntPtr? ValidScopeHandle(ElementHandle scope, AppSession session)
    {
        if (scope.NativeHandle is not IntPtr hwnd) return null;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if ((int)pid != session.ProcessId) return null;
        return hwnd;
    }

    // Snapshots all child windows of the session root into a flat list of children under a single root snapshot.
    public Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var children = new List<ElementSnapshot>();
            NativeMethods.EnumChildWindows(session.RootWindowHandle, (windowHandle, _) =>
            {
                children.Add(BuildSnapshot(windowHandle, []));
                return true;
            }, IntPtr.Zero);

            var root = BuildSnapshot(session.RootWindowHandle, children);
            return (IReadOnlyList<ElementSnapshot>)[root];
        }, ct);
    }

    // ---- Private helpers ----

    // Returns true when the windowHandle's title (Name strategy) or class name (ClassName strategy) matches the locator value.
    private static bool Matches(IntPtr windowHandle, Locator locator) => locator.Strategy switch
    {
        LocatorStrategy.Name => NativeMethods.GetWindowTitle(windowHandle).Contains(locator.Value, StringComparison.OrdinalIgnoreCase),
        LocatorStrategy.ClassName => NativeMethods.GetWindowClassName(windowHandle).Equals(locator.Value, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    // Wraps an HWND as an opaque ElementHandle with Win32-derived metadata.
    private ElementHandle WrapWindowHandle(IntPtr windowHandle)
    {
        var (rect, rectOk) = GetRect(windowHandle);
        return new($"hwnd:{windowHandle}", "win32", windowHandle)
        {
            Name = NativeMethods.GetWindowTitle(windowHandle),
            ClassName = NativeMethods.GetWindowClassName(windowHandle),
            BoundingRect = rect,
            IsEnabled = NativeMethods.IsWindowEnabled(windowHandle),
            // GetWindowRect failing (e.g. a stale handle from a window that closed mid-enumeration) can't be surfaced as an exception — providers never throw — so it's signaled as IsOffscreen=true instead of a silently-zeroed rect, which ElementInputHelpers.GetCenter's interactability guard already checks for.
            IsOffscreen = rectOk ? null : true,
            Provider = this,
        };
    }

    // Builds an ElementSnapshot for windowHandle with the given children list.
    private static ElementSnapshot BuildSnapshot(IntPtr windowHandle, IReadOnlyList<ElementSnapshot> children) =>
        new($"hwnd:{windowHandle}", NativeMethods.GetWindowTitle(windowHandle), null, NativeMethods.GetWindowClassName(windowHandle), null, GetRect(windowHandle).Rect, children);

    // Returns windowHandle's current bounding rectangle in physical screen coordinates, and whether GetWindowRect actually succeeded — a stale/closed handle returns false with a zeroed rect.
    private static (Rect Rect, bool Success) GetRect(IntPtr windowHandle)
    {
        var ok = NativeMethods.GetWindowRect(windowHandle, out var rect);
        return (new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), ok);
    }
}
