// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Errors;

// Thrown when an action targets a window that is currently minimized — SendInput cannot land on a minimized window's client area.
public sealed class WindowMinimizedError : Exception
{
    public IntPtr WindowHandle { get; }

    // Thrown by App before sending input to a minimized root window.
    public WindowMinimizedError(IntPtr windowHandle)
        : base($"Window (handle {windowHandle}) is minimized — SendInput cannot target it meaningfully. Restore the window before interacting with it.")
        => WindowHandle = windowHandle;
}
