// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Operators;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Operators;

public sealed class Uia3OperatorTests
{
    private static ElementHandle Handle(IUIAutomationElement native) => new("1", "uia3", native);

    private static COMException StaleElement() => new("element not available", unchecked((int)0x80040201));

    [Fact]
    public async Task TryClickAsync_InvokePatternSupported_InvokesAndReturnsTrue()
    {
        var invoke = new Mock<IUIAutomationInvokePattern>();
        var element = new Mock<IUIAutomationElement>();
        element.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId)).Returns(invoke.Object);

        var result = await new Uia3Operator().TryClickAsync(Handle(element.Object));

        Assert.True(result);
        invoke.Verify(i => i.Invoke(), Times.Once);
    }

    [Fact]
    public async Task TryClickAsync_InvokePatternNotSupported_ReturnsFalse()
    {
        var element = new Mock<IUIAutomationElement>();
        element.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId)).Returns((object)null!);

        var result = await new Uia3Operator().TryClickAsync(Handle(element.Object));

        Assert.False(result);
    }

    [Fact]
    public async Task TryClickAsync_ElementGoneStale_ReturnsFalseInsteadOfThrowing()
    {
        var element = new Mock<IUIAutomationElement>();
        element.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId)).Throws(StaleElement());

        var result = await new Uia3Operator().TryClickAsync(Handle(element.Object));

        Assert.False(result);
    }

    [Fact]
    public async Task TrySetValueAsync_WritableValuePattern_SetsValueAndReturnsTrue()
    {
        var valuePattern = new Mock<IUIAutomationValuePattern>();
        valuePattern.SetupGet(v => v.CurrentIsReadOnly).Returns(0);
        var element = new Mock<IUIAutomationElement>();
        element.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId)).Returns(valuePattern.Object);

        var result = await new Uia3Operator().TrySetValueAsync(Handle(element.Object), "hello");

        Assert.True(result);
        valuePattern.Verify(v => v.SetValue("hello"), Times.Once);
    }

    [Fact]
    public async Task TrySetValueAsync_ReadOnlyValuePattern_ReturnsFalseWithoutSetting()
    {
        var valuePattern = new Mock<IUIAutomationValuePattern>();
        valuePattern.SetupGet(v => v.CurrentIsReadOnly).Returns(1);
        var element = new Mock<IUIAutomationElement>();
        element.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId)).Returns(valuePattern.Object);

        var result = await new Uia3Operator().TrySetValueAsync(Handle(element.Object), "hello");

        Assert.False(result);
        valuePattern.Verify(v => v.SetValue(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TrySetValueAsync_ValuePatternNotSupported_ReturnsFalse()
    {
        var element = new Mock<IUIAutomationElement>();
        element.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId)).Returns((object)null!);

        var result = await new Uia3Operator().TrySetValueAsync(Handle(element.Object), "hello");

        Assert.False(result);
    }

    [Fact]
    public async Task TrySetValueAsync_ElementGoneStale_ReturnsFalseInsteadOfThrowing()
    {
        var element = new Mock<IUIAutomationElement>();
        element.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId)).Throws(StaleElement());

        var result = await new Uia3Operator().TrySetValueAsync(Handle(element.Object), "hello");

        Assert.False(result);
    }

    [Fact]
    public async Task TryGetValueAsync_ValuePatternSupported_ReturnsCurrentValue()
    {
        var valuePattern = new Mock<IUIAutomationValuePattern>();
        valuePattern.SetupGet(v => v.CurrentValue).Returns("current text");
        var element = new Mock<IUIAutomationElement>();
        element.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId)).Returns(valuePattern.Object);

        var result = await new Uia3Operator().TryGetValueAsync(Handle(element.Object));

        Assert.Equal("current text", result);
    }

    [Fact]
    public async Task TryGetValueAsync_OnlyTextPatternSupported_FallsBackToDocumentRangeText()
    {
        var textRange = new Mock<IUIAutomationTextRange>();
        textRange.Setup(r => r.GetText(-1)).Returns("document text");
        var textPattern = new Mock<IUIAutomationTextPattern>();
        textPattern.SetupGet(t => t.DocumentRange).Returns(textRange.Object);
        var element = new Mock<IUIAutomationElement>();
        element.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId)).Returns((object)null!);
        element.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_TextPatternId)).Returns(textPattern.Object);

        var result = await new Uia3Operator().TryGetValueAsync(Handle(element.Object));

        Assert.Equal("document text", result);
    }

    [Fact]
    public async Task TryGetValueAsync_NeitherPatternSupported_ReturnsNull()
    {
        var element = new Mock<IUIAutomationElement>();
        element.Setup(e => e.GetCurrentPattern(It.IsAny<int>())).Returns((object)null!);

        var result = await new Uia3Operator().TryGetValueAsync(Handle(element.Object));

        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetValueAsync_ElementGoneStale_ReturnsNullInsteadOfThrowing()
    {
        var element = new Mock<IUIAutomationElement>();
        element.Setup(e => e.GetCurrentPattern(It.IsAny<int>())).Throws(StaleElement());

        var result = await new Uia3Operator().TryGetValueAsync(Handle(element.Object));

        Assert.Null(result);
    }
}
