// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Providers;

public sealed class NativeMethodsTests
{
    [Fact]
    public void ThrowIfFailed_Success_DoesNotThrow()
        => true.ThrowIfFailed("SomeOperation");

    [Fact]
    public void ThrowIfFailed_Failure_ThrowsWin32CallErrorNamingOperation()
    {
        var ex = Assert.Throws<Win32CallError>(() => false.ThrowIfFailed("SomeOperation"));

        Assert.Equal("SomeOperation", ex.Operation);
    }

    [Fact]
    public void EnsureForegroundOrThrow_WindowNeverBecomesForeground_ThrowsWindowActivationError()
    {
        // Not a real window, so it can never equal GetForegroundWindow()'s result.
        var bogusHandle = (IntPtr)0x7FFFFFFF;

        var ex = Assert.Throws<WindowActivationError>(() => NativeMethods.EnsureForegroundOrThrow(bogusHandle, timeoutMs: 50, pollIntervalMs: 10));

        Assert.Equal(bogusHandle, ex.WindowHandle);
    }
}
