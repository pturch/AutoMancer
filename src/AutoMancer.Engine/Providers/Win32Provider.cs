// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Providers;

// Last-resort provider using EnumChildWindows P/Invoke — works on apps with no accessibility tree, matching by title or class name.
public sealed class Win32Provider : IElementProvider
{
    public string ProviderName => "win32";

    // Finds the first child window matching the locator by title (Name) or class name (ClassName); null if not found or strategy unsupported.
    public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ElementHandle? result = null;
            NativeMethods.EnumChildWindows(session.RootWindowHandle, (windowHandle, _) =>
            {
                if (!Matches(windowHandle, locator))
                    return true;
                result = WrapWindowHandle(windowHandle);
                return false;
            }, IntPtr.Zero);
            return result;
        }, ct);
    }

    // Finds all child windows matching the locator by title (Name) or class name (ClassName); empty if none match.
    public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var results = new List<ElementHandle>();
            NativeMethods.EnumChildWindows(session.RootWindowHandle, (windowHandle, _) =>
            {
                if (Matches(windowHandle, locator))
                    results.Add(WrapWindowHandle(windowHandle));
                return true;
            }, IntPtr.Zero);
            return (IReadOnlyList<ElementHandle>)results;
        }, ct);
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

    // Returns true when the windowHandle's title (Name strategy) or class name (ClassName strategy) matches the locator value.
    private static bool Matches(IntPtr windowHandle, Locator locator) => locator.Strategy switch
    {
        LocatorStrategy.Name => GetTitle(windowHandle).Contains(locator.Value, StringComparison.OrdinalIgnoreCase),
        LocatorStrategy.ClassName => GetClass(windowHandle).Equals(locator.Value, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    // Wraps an HWND as an opaque ElementHandle with Win32-derived metadata.
    private ElementHandle WrapWindowHandle(IntPtr windowHandle)
    {
        var (rect, rectOk) = GetRect(windowHandle);
        return new($"hwnd:{windowHandle}", "win32", windowHandle)
        {
            Name = GetTitle(windowHandle),
            ClassName = GetClass(windowHandle),
            BoundingRect = rect,
            IsEnabled = NativeMethods.IsWindowEnabled(windowHandle),
            // GetWindowRect failing (e.g. a stale handle from a window that closed mid-enumeration) can't be surfaced as an exception — providers never throw — so it's signaled as IsOffscreen=true instead of a silently-zeroed rect, which ClickAction.GetCenter's interactability guard already checks for.
            IsOffscreen = rectOk ? null : true,
            Provider = this,
        };
    }

    // Builds an ElementSnapshot for windowHandle with the given children list.
    private static ElementSnapshot BuildSnapshot(IntPtr windowHandle, IReadOnlyList<ElementSnapshot> children) =>
        new($"hwnd:{windowHandle}", GetTitle(windowHandle), null, GetClass(windowHandle), null, GetRect(windowHandle).Rect, children);

    // Returns windowHandle's current bounding rectangle in physical screen coordinates, and whether GetWindowRect actually succeeded — a stale/closed handle returns false with a zeroed rect.
    private static (Rect Rect, bool Success) GetRect(IntPtr windowHandle)
    {
        var ok = NativeMethods.GetWindowRect(windowHandle, out var rect);
        return (new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), ok);
    }

    // Returns the window title text via GetWindowText.
    private static string GetTitle(IntPtr windowHandle)
    {
        var sb = new StringBuilder(512);
        NativeMethods.GetWindowText(windowHandle, sb, sb.Capacity);
        return sb.ToString();
    }

    // Returns the window class name via GetClassName.
    private static string GetClass(IntPtr windowHandle)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(windowHandle, sb, sb.Capacity);
        return sb.ToString();
    }
}
