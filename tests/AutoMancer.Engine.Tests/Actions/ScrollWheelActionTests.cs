// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class ScrollWheelActionTests
{
    private static ElementHandle Handle(bool? isEnabled = null, bool? isOffscreen = null) =>
        new ElementHandle("1", "test", new object()) { Name = "Scrollable", IsEnabled = isEnabled, IsOffscreen = isOffscreen, BoundingRect = new Rect(0, 0, 10, 10) };

    // GetCenter's interactability check must run — and throw — before either wheel event is sent.
    [Fact]
    public async Task ExecuteAsync_Disabled_ThrowsBeforeSendingInput()
    {
        var ex = await Assert.ThrowsAsync<ElementNotInteractableError>(() => ScrollWheelAction.ExecuteAsync(Handle(isEnabled: false), deltaX: 0, deltaY: 1));

        Assert.Contains("Scrollable", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Offscreen_ThrowsBeforeSendingInput()
    {
        var ex = await Assert.ThrowsAsync<ElementNotInteractableError>(() => ScrollWheelAction.ExecuteAsync(Handle(isEnabled: true, isOffscreen: true), deltaX: 0, deltaY: 1));

        Assert.Contains("IsOffscreen=true", ex.Message);
    }
}
