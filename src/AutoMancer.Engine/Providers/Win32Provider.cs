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
            NativeMethods.EnumChildWindows(session.RootWindowHandle, (hwnd, _) =>
            {
                if (!Matches(hwnd, locator))
                    return true;
                result = WrapHwnd(hwnd);
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
            NativeMethods.EnumChildWindows(session.RootWindowHandle, (hwnd, _) =>
            {
                if (Matches(hwnd, locator))
                    results.Add(WrapHwnd(hwnd));
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
            NativeMethods.EnumChildWindows(session.RootWindowHandle, (hwnd, _) =>
            {
                children.Add(BuildSnapshot(hwnd, []));
                return true;
            }, IntPtr.Zero);

            var root = BuildSnapshot(session.RootWindowHandle, children);
            return (IReadOnlyList<ElementSnapshot>)[root];
        }, ct);
    }

    // Returns true when the hwnd's title (Name strategy) or class name (ClassName strategy) matches the locator value.
    private static bool Matches(IntPtr hwnd, Locator locator) => locator.Strategy switch
    {
        LocatorStrategy.Name => GetTitle(hwnd).Contains(locator.Value, StringComparison.OrdinalIgnoreCase),
        LocatorStrategy.ClassName => GetClass(hwnd).Equals(locator.Value, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    // Wraps an HWND as an opaque ElementHandle with Win32-derived metadata.
    private ElementHandle WrapHwnd(IntPtr hwnd) => new($"hwnd:{hwnd}", "win32", hwnd)
    {
        Name = GetTitle(hwnd),
        ClassName = GetClass(hwnd),
        Provider = this,
    };

    // Builds an ElementSnapshot for hwnd with the given children list.
    private static ElementSnapshot BuildSnapshot(IntPtr hwnd, IReadOnlyList<ElementSnapshot> children) =>
        new($"hwnd:{hwnd}", GetTitle(hwnd), null, GetClass(hwnd), null, default, children);

    // Returns the window title text via GetWindowText.
    private static string GetTitle(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    // Returns the window class name via GetClassName.
    private static string GetClass(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
