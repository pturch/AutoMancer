// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class ClickActionTests
{
    // Middle/Back/Forward have no live UI effect to assert in this project's integration tests, so this verifies the SendInput flag mapping directly.
    [Theory]
    [InlineData(MouseButton.Left, (uint)NativeMethods.MouseEventFlags.LeftDown, (uint)NativeMethods.MouseEventFlags.LeftUp, 0u)]
    [InlineData(MouseButton.Right, (uint)NativeMethods.MouseEventFlags.RightDown, (uint)NativeMethods.MouseEventFlags.RightUp, 0u)]
    [InlineData(MouseButton.Middle, (uint)NativeMethods.MouseEventFlags.MiddleDown, (uint)NativeMethods.MouseEventFlags.MiddleUp, 0u)]
    [InlineData(MouseButton.Back, (uint)NativeMethods.MouseEventFlags.XDown, (uint)NativeMethods.MouseEventFlags.XUp, NativeMethods.XButton1)]
    [InlineData(MouseButton.Forward, (uint)NativeMethods.MouseEventFlags.XDown, (uint)NativeMethods.MouseEventFlags.XUp, NativeMethods.XButton2)]
    public void ButtonFlags_MapsExpectedSendInputFlags(MouseButton button, uint expectedDown, uint expectedUp, uint expectedMouseData)
    {
        var (down, up, mouseData) = ClickAction.ButtonFlags(button);

        Assert.Equal(expectedDown, (uint)down);
        Assert.Equal(expectedUp, (uint)up);
        Assert.Equal(expectedMouseData, mouseData);
    }
}
