// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

// Covers ToggleAction: no-op for non-UIA handles and unsupported patterns, and Toggle when supported.
public sealed class ToggleActionTests
{
    [Fact]
    public async Task ExecuteAsync_NonUiaHandle_NoOp()
    {
        var element = new ElementHandle("1", "win32", new object());

        await ToggleAction.ExecuteAsync(element);
    }

    [Fact]
    public async Task ExecuteAsync_TogglePatternNotSupported_NoOp()
    {
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId)).Returns((object)null!);
        var writer = new StringWriter();
        var element = new ElementHandle("1", "uia3", native.Object) { Logger = new EngineLogger(writer) };

        await ToggleAction.ExecuteAsync(element);

        Assert.Contains("TogglePattern not supported, nothing toggled", writer.ToString());
        Assert.DoesNotContain("Toggled via", writer.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_TogglePatternSupported_Toggles()
    {
        var pattern = new Mock<IUIAutomationTogglePattern>();
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId)).Returns(pattern.Object);
        var writer = new StringWriter();
        var element = new ElementHandle("1", "uia3", native.Object) { Logger = new EngineLogger(writer) };

        await ToggleAction.ExecuteAsync(element);

        pattern.Verify(p => p.Toggle(), Times.Once);
        Assert.Contains("Toggled via TogglePattern", writer.ToString());
    }
}
