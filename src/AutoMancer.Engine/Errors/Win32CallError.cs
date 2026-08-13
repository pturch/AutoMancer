// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Errors;

// Thrown when a bool-returning Win32 API call reports failure — e.g. a window handle that's gone stale between resolve and use.
public sealed class Win32CallError : Exception
{
    public string Operation { get; }
    public int Win32Error { get; }

    // Thrown by NativeMethods.ThrowIfFailed for the operation name and GetLastWin32Error() code at the point of failure.
    public Win32CallError(string operation, int win32Error)
        : base($"{operation} failed: {Win32ErrorCodes.Describe(win32Error)}")
    {
        Operation = operation;
        Win32Error = win32Error;
    }
}
