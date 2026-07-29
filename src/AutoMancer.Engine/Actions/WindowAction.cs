// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Possible visual states for a top-level window.
public enum WindowState { Normal, Maximized, Minimized }

// Moves, resizes, or changes the visual state of a top-level window; tries UIA patterns first, falls back to Win32.
public static class WindowAction
{
    private static readonly IUIAutomation Automation = new CUIAutomation8Class();

    // Returns the window's current bounding rectangle in physical screen coordinates.
    public static Task<Rect> GetSizeAsync(IntPtr hwnd, CancellationToken ct = default) => Task.Run(() =>
    {
        NativeMethods.GetWindowRect(hwnd, out var r);
        return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }, ct);

    // Moves the window's top-left corner to (x, y) in physical screen coordinates; size is unchanged.
    public static Task MoveAsync(IntPtr hwnd, int x, int y, CancellationToken ct = default) => Task.Run(() =>
    {
        var element = Automation.ElementFromHandle(hwnd);
        if (element?.GetCurrentPattern(UIA_PatternIds.UIA_TransformPatternId) is IUIAutomationTransformPattern transform
            && transform.CurrentCanMove != 0)
        {
            transform.Move(x, y);
            return;
        }
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }, ct);

    // Resizes the window to (width × height) in physical pixels; position is unchanged.
    public static Task ResizeAsync(IntPtr hwnd, int width, int height, CancellationToken ct = default) => Task.Run(() =>
    {
        var element = Automation.ElementFromHandle(hwnd);
        if (element?.GetCurrentPattern(UIA_PatternIds.UIA_TransformPatternId) is IUIAutomationTransformPattern transform
            && transform.CurrentCanResize != 0)
        {
            transform.Resize(width, height);
            return;
        }
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, width, height,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }, ct);

    // Changes the window's visual state (Normal/Maximized/Minimized); tries WindowPattern then ShowWindow.
    public static Task SetVisualStateAsync(IntPtr hwnd, WindowState state, CancellationToken ct = default) => Task.Run(() =>
    {
        var element = Automation.ElementFromHandle(hwnd);
        if (element?.GetCurrentPattern(UIA_PatternIds.UIA_WindowPatternId) is IUIAutomationWindowPattern windowPattern)
        {
            // Our WindowState ordinals match the COM WindowVisualState enum (Normal=0, Maximized=1, Minimized=2).
            windowPattern.SetWindowVisualState((WindowVisualState)state);
            return;
        }
        var sw = state switch
        {
            WindowState.Maximized => NativeMethods.SwMaximize,
            WindowState.Minimized => NativeMethods.SwMinimize,
            _ => NativeMethods.SwRestore,
        };
        NativeMethods.ShowWindow(hwnd, sw);
    }, ct);
}
