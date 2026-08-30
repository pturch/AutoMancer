// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class TypeActionTests
{
    private static ElementHandle Handle(IElementOperator? op = null, bool? isEnabled = null, bool? isOffscreen = null) =>
        new ElementHandle("1", "test", new object()) { Name = "Input", Operator = op, IsEnabled = isEnabled, IsOffscreen = isOffscreen, BoundingRect = new Rect(0, 0, 10, 10) };

    [Fact]
    public async Task ExecuteAsync_OperatorSetsValue_ReturnsWithoutFallingThrough()
    {
        var op = new Mock<IElementOperator>();
        op.Setup(o => o.TrySetValueAsync(It.IsAny<ElementHandle>(), "hi", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var writer = new StringWriter();
        var element = Handle(op.Object);
        element.Logger = new EngineLogger(writer);

        await TypeAction.ExecuteAsync(element, "hi");

        op.Verify(o => o.TrySetValueAsync(It.IsAny<ElementHandle>(), "hi", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Typed via native pattern", writer.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_OperatorDeclines_DisabledElement_ThrowsBeforeSendingInput()
    {
        var op = new Mock<IElementOperator>();
        op.Setup(o => o.TrySetValueAsync(It.IsAny<ElementHandle>(), "hi", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<ElementNotInteractableError>(() => TypeAction.ExecuteAsync(Handle(op.Object, isEnabled: false), "hi"));

        Assert.Contains("Input", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NoOperator_OffscreenElement_ThrowsBeforeSendingInput()
    {
        var ex = await Assert.ThrowsAsync<ElementNotInteractableError>(() => TypeAction.ExecuteAsync(Handle(isEnabled: true, isOffscreen: true), "hi"));

        Assert.Contains("IsOffscreen=true", ex.Message);
    }

    // Empty text must build a zero-length INPUT batch rather than skip the call entirely — SendInputs([]) is a safe no-op.
    [Fact]
    public void SendUnicodeText_EmptyString_DoesNotThrow()
        => TypeAction.SendUnicodeText("", null);
}
