// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
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
    public static Task<Rect> GetSizeAsync(IntPtr windowHandle, CancellationToken ct = default) => Task.Run(() =>
    {
        NativeMethods.GetWindowRect(windowHandle, out var r);
        return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }, ct);

    // Moves the window's top-left corner to (x, y) in physical screen coordinates; size is unchanged.
    public static Task MoveAsync(IntPtr windowHandle, int x, int y, CancellationToken ct = default)
        => MoveCoreAsync(windowHandle, x, y, null, ct);

    // Same as MoveAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task MoveCoreAsync(IntPtr windowHandle, int x, int y, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        var transform = TryGetInteractionPattern<IUIAutomationTransformPattern>(windowHandle, UIA_PatternIds.UIA_TransformPatternId);
        if (transform is not null && transform.CurrentCanMove != 0)
        {
            transform.Move(x, y);
            logger?.Info("Window moved via TransformPattern", new { x, y });
            return;
        }
        // No TransformPattern, or the element reports it can't be moved (CurrentCanMove == 0) — force it via Win32 instead.
        NativeMethods.SetWindowPos(windowHandle, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        logger?.Info("Window moved via Win32 SetWindowPos", new { x, y });
    }, ct);

    // Resizes the window to (width × height) in physical pixels; position is unchanged.
    public static Task ResizeAsync(IntPtr windowHandle, int width, int height, CancellationToken ct = default)
        => ResizeCoreAsync(windowHandle, width, height, null, ct);

    // Same as ResizeAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task ResizeCoreAsync(IntPtr windowHandle, int width, int height, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        var transform = TryGetInteractionPattern<IUIAutomationTransformPattern>(windowHandle, UIA_PatternIds.UIA_TransformPatternId);
        if (transform is not null && transform.CurrentCanResize != 0)
        {
            transform.Resize(width, height);
            logger?.Info("Window resized via TransformPattern", new { width, height });
            return;
        }
        // No TransformPattern, or the element reports it can't be resized (CurrentCanResize == 0) — force it via Win32 instead.
        NativeMethods.SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, width, height,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        logger?.Info("Window resized via Win32 SetWindowPos", new { width, height });
    }, ct);

    // Changes the window's visual state (Normal/Maximized/Minimized); tries WindowPattern then ShowWindow.
    public static Task SetVisualStateAsync(IntPtr windowHandle, WindowState state, CancellationToken ct = default)
        => SetVisualStateCoreAsync(windowHandle, state, null, ct);

    // Same as SetVisualStateAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task SetVisualStateCoreAsync(IntPtr windowHandle, WindowState state, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        if (TryGetInteractionPattern<IUIAutomationWindowPattern>(windowHandle, UIA_PatternIds.UIA_WindowPatternId) is { } windowPattern)
        {
            // Our WindowState ordinals match the COM WindowVisualState enum (Normal=0, Maximized=1, Minimized=2).
            windowPattern.SetWindowVisualState((WindowVisualState)state);
            logger?.Info("Window state set via WindowPattern", new { state });
            return;
        }
        // No WindowPattern support (e.g. some UWP hosts) — drive the show state directly via Win32 instead.
        var sw = state switch
        {
            WindowState.Maximized => NativeMethods.SwMaximize,
            WindowState.Minimized => NativeMethods.SwMinimize,
            _ => NativeMethods.SwRestore,
        };
        NativeMethods.ShowWindow(windowHandle, sw);
        logger?.Info("Window state set via Win32 ShowWindow", new { state });
    }, ct);

    // Closes the window; tries WindowPattern.Close() first, falls back to posting WM_CLOSE.
    public static Task CloseAsync(IntPtr windowHandle, CancellationToken ct = default)
        => CloseCoreAsync(windowHandle, null, ct);

    // Same as CloseAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task CloseCoreAsync(IntPtr windowHandle, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        if (TryGetInteractionPattern<IUIAutomationWindowPattern>(windowHandle, UIA_PatternIds.UIA_WindowPatternId) is { } windowPattern)
        {
            windowPattern.Close();
            logger?.Info("Window closed via WindowPattern");
            return;
        }
        // No WindowPattern support (e.g. some UWP hosts, confirmed for Calculator) — post WM_CLOSE directly instead.
        NativeMethods.PostMessage(windowHandle, NativeMethods.WmClose, IntPtr.Zero, IntPtr.Zero);
        logger?.Info("Window closed via Win32 PostMessage");
    }, ct);

    // Checks whether windowHandle's UIA element supports the capability identified by patternId, returning it as T if so, else null.
    private static T? TryGetInteractionPattern<T>(IntPtr windowHandle, int patternId) where T : class
        => Automation.ElementFromHandle(windowHandle)?.GetCurrentPattern(patternId) as T;
}
