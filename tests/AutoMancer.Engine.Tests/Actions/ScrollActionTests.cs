// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class ScrollActionTests
{
    [Fact]
    public async Task ExecuteAsync_NonUiaHandle_NoOp()
    {
        var element = new ElementHandle("1", "win32", new object());

        await ScrollAction.ExecuteAsync(element);
    }

    [Fact]
    public async Task ExecuteAsync_ScrollItemPatternNotSupported_NoOp()
    {
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ScrollItemPatternId)).Returns((object)null!);
        var writer = new StringWriter();
        var element = new ElementHandle("1", "uia3", native.Object) { Logger = new EngineLogger(writer) };

        await ScrollAction.ExecuteAsync(element);

        Assert.Empty(writer.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ScrollItemPatternSupported_ScrollsIntoView()
    {
        var pattern = new Mock<IUIAutomationScrollItemPattern>();
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ScrollItemPatternId)).Returns(pattern.Object);
        var writer = new StringWriter();
        var element = new ElementHandle("1", "uia3", native.Object) { Logger = new EngineLogger(writer) };

        await ScrollAction.ExecuteAsync(element);

        pattern.Verify(p => p.ScrollIntoView(), Times.Once);
        Assert.Contains("Scrolled into view", writer.ToString());
    }
}
