// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class HoverActionTests
{
    private static ElementHandle Handle(bool? isEnabled = null, bool? isOffscreen = null) =>
        new ElementHandle("1", "test", new object()) { Name = "Tooltip target", IsEnabled = isEnabled, IsOffscreen = isOffscreen, BoundingRect = new Rect(0, 0, 10, 10) };

    // GetCenter's interactability check must run — and throw — before the MOUSEEVENTF_MOVE is sent.
    [Fact]
    public async Task ExecuteAsync_Disabled_ThrowsBeforeSendingInput()
    {
        var ex = await Assert.ThrowsAsync<ElementNotInteractableError>(() => HoverAction.ExecuteAsync(Handle(isEnabled: false)));

        Assert.Contains("Tooltip target", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Offscreen_ThrowsBeforeSendingInput()
    {
        var ex = await Assert.ThrowsAsync<ElementNotInteractableError>(() => HoverAction.ExecuteAsync(Handle(isEnabled: true, isOffscreen: true)));

        Assert.Contains("IsOffscreen=true", ex.Message);
    }
}
