// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class ClickActionTests
{
    // Middle/Back/Forward have no live UI effect to assert in this project's integration tests, so this verifies the SendInput flag mapping directly.
    [Theory]
    [InlineData(MouseButton.Left, NativeMethods.MouseEventLeftDown, NativeMethods.MouseEventLeftUp, 0u)]
    [InlineData(MouseButton.Right, NativeMethods.MouseEventRightDown, NativeMethods.MouseEventRightUp, 0u)]
    [InlineData(MouseButton.Middle, NativeMethods.MouseEventMiddleDown, NativeMethods.MouseEventMiddleUp, 0u)]
    [InlineData(MouseButton.Back, NativeMethods.MouseEventXDown, NativeMethods.MouseEventXUp, NativeMethods.XButton1)]
    [InlineData(MouseButton.Forward, NativeMethods.MouseEventXDown, NativeMethods.MouseEventXUp, NativeMethods.XButton2)]
    public void ButtonFlags_MapsExpectedSendInputFlags(MouseButton button, uint expectedDown, uint expectedUp, uint expectedMouseData)
    {
        var (down, up, mouseData) = ClickAction.ButtonFlags(button);

        Assert.Equal(expectedDown, down);
        Assert.Equal(expectedUp, up);
        Assert.Equal(expectedMouseData, mouseData);
    }
}
