// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class ScreenshotActionTests
{
    [Fact]
    public async Task CaptureAsync_InvalidHandle_ThrowsWin32CallError()
    {
        var ex = await Assert.ThrowsAsync<Win32CallError>(() => ScreenshotAction.CaptureAsync((IntPtr)0x7FFFFFFF));

        Assert.Equal("GetWindowRect", ex.Operation);
    }
}
