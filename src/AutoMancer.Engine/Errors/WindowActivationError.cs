// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Errors;

// Thrown when Windows declines to foreground the target window, verified via GetForegroundWindow rather than SetForegroundWindow's own unreliable return value.
public sealed class WindowActivationError : Exception
{
    public IntPtr WindowHandle { get; }

    // Thrown before SendInput would otherwise silently reach whatever window actually holds focus instead — commonly caused by Windows' anti-focus-stealing restriction when another process holds real input focus.
    public WindowActivationError(IntPtr windowHandle)
        : base($"Window (handle {windowHandle}) could not be brought to the foreground — SendInput was not sent, to avoid it landing in whichever window actually has focus.")
        => WindowHandle = windowHandle;
}
