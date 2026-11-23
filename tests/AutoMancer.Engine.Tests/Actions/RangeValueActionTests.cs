// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

// Covers RangeValueAction: no-op set and null read when unsupported, SetValue with the value, and the value with its bounds.
public sealed class RangeValueActionTests
{
    // Builds a uia3 handle whose RangeValuePattern lookup returns pattern (null = unsupported).
    private static ElementHandle Uia3Handle(object? pattern, StringWriter? writer = null)
    {
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_RangeValuePatternId)).Returns(pattern!);
        return new ElementHandle("1", "uia3", native.Object) { Logger = writer is null ? null : new EngineLogger(writer) };
    }

    [Fact]
    public async Task NonUiaHandle_SetIsNoOpAndGetIsNull()
    {
        var element = new ElementHandle("1", "win32", new object());

        await RangeValueAction.SetValueAsync(element, 5);
        Assert.Null(await RangeValueAction.GetValueAsync(element));
    }

    [Fact]
    public async Task PatternNotSupported_SetIsNoOpAndGetIsNull()
    {
        var writer = new StringWriter();
        var element = Uia3Handle(null, writer);

        await RangeValueAction.SetValueAsync(element, 5);

        Assert.Contains("RangeValuePattern not supported, nothing set", writer.ToString());
        Assert.Null(await RangeValueAction.GetValueAsync(element));
    }

    [Fact]
    public async Task SetValueAsync_CallsSetValueWithTheValue()
    {
        var pattern = new Mock<IUIAutomationRangeValuePattern>();

        await RangeValueAction.SetValueAsync(Uia3Handle(pattern.Object), 7.5);

        pattern.Verify(p => p.SetValue(7.5), Times.Once);
    }

    [Fact]
    public async Task GetValueAsync_ReturnsValueAndBounds()
    {
        var pattern = new Mock<IUIAutomationRangeValuePattern>();
        pattern.SetupGet(p => p.CurrentValue).Returns(3);
        pattern.SetupGet(p => p.CurrentMinimum).Returns(0);
        pattern.SetupGet(p => p.CurrentMaximum).Returns(10);

        Assert.Equal(new RangeValueInfo(3, 0, 10), await RangeValueAction.GetValueAsync(Uia3Handle(pattern.Object)));
    }
}
