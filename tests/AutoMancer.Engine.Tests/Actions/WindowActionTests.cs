// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class WindowActionTests
{
    // A handle value guaranteed not to correspond to any real window.
    private static readonly IntPtr BogusHandle = (IntPtr)0x7FFFFFFF;

    [Fact]
    public async Task GetSizeAsync_InvalidHandle_ThrowsWin32CallError()
    {
        var ex = await Assert.ThrowsAsync<Win32CallError>(() => WindowAction.GetSizeAsync(BogusHandle));

        Assert.Equal("GetWindowRect", ex.Operation);
    }

    [Fact]
    public async Task MoveAsync_InvalidHandle_ThrowsWin32CallError()
    {
        var ex = await Assert.ThrowsAsync<Win32CallError>(() => WindowAction.MoveAsync(BogusHandle, 0, 0));

        Assert.Equal("SetWindowPos", ex.Operation);
    }

    [Fact]
    public async Task ResizeAsync_InvalidHandle_ThrowsWin32CallError()
    {
        var ex = await Assert.ThrowsAsync<Win32CallError>(() => WindowAction.ResizeAsync(BogusHandle, 100, 100));

        Assert.Equal("SetWindowPos", ex.Operation);
    }
}
