// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class ClearActionTests
{
    private static ElementHandle Handle(IElementOperator? op = null, bool? isEnabled = null, bool? isOffscreen = null) =>
        new ElementHandle("1", "test", new object()) { Name = "Editor", Operator = op, IsEnabled = isEnabled, IsOffscreen = isOffscreen, BoundingRect = new Rect(0, 0, 10, 10) };

    [Fact]
    public async Task ExecuteAsync_OperatorSetsValue_ReturnsWithoutFallingThrough()
    {
        var op = new Mock<IElementOperator>();
        op.Setup(o => o.TrySetValueAsync(It.IsAny<ElementHandle>(), "", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var writer = new StringWriter();
        var element = Handle(op.Object);
        element.Logger = new EngineLogger(writer);

        await ClearAction.ExecuteAsync(element);

        op.Verify(o => o.TrySetValueAsync(It.IsAny<ElementHandle>(), "", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Cleared via native pattern", writer.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_OperatorDeclines_DisabledElement_ThrowsBeforeSendingInput()
    {
        var op = new Mock<IElementOperator>();
        op.Setup(o => o.TrySetValueAsync(It.IsAny<ElementHandle>(), "", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<ElementNotInteractableError>(() => ClearAction.ExecuteAsync(Handle(op.Object, isEnabled: false)));

        Assert.Contains("Editor", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NoOperator_OffscreenElement_ThrowsBeforeSendingInput()
    {
        var ex = await Assert.ThrowsAsync<ElementNotInteractableError>(() => ClearAction.ExecuteAsync(Handle(isEnabled: true, isOffscreen: true)));

        Assert.Contains("IsOffscreen=true", ex.Message);
    }
}
