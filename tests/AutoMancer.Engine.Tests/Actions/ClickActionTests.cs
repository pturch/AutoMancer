// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class ClickActionTests
{
    // Reproduces a COMException observed against a live split-button MenuItem: get_CurrentNativeWindowHandle timed out mid-click.
    [Fact]
    public void EnsureForeground_CurrentNativeWindowHandleThrows_LogsInsteadOfThrowing()
    {
        var native = new Mock<IUIAutomationElement>();
        native.SetupGet(e => e.CurrentNativeWindowHandle).Throws(new COMException("Operation timed out.", unchecked((int)0x80131505)));
        var writer = new StringWriter();
        var element = new ElementHandle("1", "uia3", native.Object) { Logger = new EngineLogger(writer) };

        ClickAction.EnsureForeground(element);

        Assert.Contains("CurrentNativeWindowHandle failed", writer.ToString());
    }

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

    private static ElementHandle Handle(bool? isEnabled = null, bool? isOffscreen = null, Rect rect = default) =>
        new ElementHandle("1", "test", new object()) { Name = "Save", AutomationId = "SaveButton", IsEnabled = isEnabled, IsOffscreen = isOffscreen, BoundingRect = rect == default ? new Rect(0, 0, 10, 10) : rect };

    [Fact]
    public void GetCenter_EnabledOnscreenElement_ReturnsRectCenter()
    {
        var (x, y) = ClickAction.GetCenter(Handle(isEnabled: true, isOffscreen: false, rect: new Rect(10, 20, 30, 40)));

        Assert.Equal(25, x);
        Assert.Equal(40, y);
    }

    [Fact]
    public void GetCenter_UnknownEnabledOffscreenState_ReturnsRectCenter()
    {
        // Win32-resolved elements leave IsEnabled/IsOffscreen null when they can't be determined — that must not be treated as "known bad."
        var (x, y) = ClickAction.GetCenter(Handle(isEnabled: null, isOffscreen: null, rect: new Rect(0, 0, 10, 10)));

        Assert.Equal(5, x);
        Assert.Equal(5, y);
    }

    [Fact]
    public void GetCenter_Disabled_ThrowsElementNotInteractableError()
    {
        var ex = Assert.Throws<ElementNotInteractableError>(() => ClickAction.GetCenter(Handle(isEnabled: false)));

        Assert.Contains("Save", ex.Message);
        Assert.Contains("IsEnabled=false", ex.Message);
    }

    [Fact]
    public void GetCenter_Offscreen_ThrowsElementNotInteractableError()
    {
        var ex = Assert.Throws<ElementNotInteractableError>(() => ClickAction.GetCenter(Handle(isEnabled: true, isOffscreen: true)));

        Assert.Contains("Save", ex.Message);
        Assert.Contains("IsOffscreen=true", ex.Message);
    }
}
