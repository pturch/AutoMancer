// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

// Covers ExpandCollapseAction: no-op for non-UIA handles and unsupported patterns, and Expand/Collapse calls when supported.
public sealed class ExpandCollapseActionTests
{
    [Fact]
    public async Task NonUiaHandle_NoOp()
    {
        var element = new ElementHandle("1", "win32", new object());

        await ExpandCollapseAction.ExpandAsync(element);
        await ExpandCollapseAction.CollapseAsync(element);
    }

    [Fact]
    public async Task PatternNotSupported_NoOp()
    {
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId)).Returns((object)null!);
        var writer = new StringWriter();
        var element = new ElementHandle("1", "uia3", native.Object) { Logger = new EngineLogger(writer) };

        await ExpandCollapseAction.ExpandAsync(element);
        await ExpandCollapseAction.CollapseAsync(element);

        Assert.Contains("nothing expanded", writer.ToString());
        Assert.Contains("nothing collapsed", writer.ToString());
    }

    [Fact]
    public async Task ExpandAsync_PatternSupported_Expands()
    {
        var pattern = new Mock<IUIAutomationExpandCollapsePattern>();
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId)).Returns(pattern.Object);
        var element = new ElementHandle("1", "uia3", native.Object);

        await ExpandCollapseAction.ExpandAsync(element);

        pattern.Verify(p => p.Expand(), Times.Once);
        pattern.Verify(p => p.Collapse(), Times.Never);
    }

    [Fact]
    public async Task CollapseAsync_PatternSupported_Collapses()
    {
        var pattern = new Mock<IUIAutomationExpandCollapsePattern>();
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId)).Returns(pattern.Object);
        var element = new ElementHandle("1", "uia3", native.Object);

        await ExpandCollapseAction.CollapseAsync(element);

        pattern.Verify(p => p.Collapse(), Times.Once);
        pattern.Verify(p => p.Expand(), Times.Never);
    }
}
