// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

// Covers SelectionAction: no-op writes and false/empty reads when unsupported, each write calling only its own method, and selected items wrapped as handles.
public sealed class SelectionActionTests
{
    // Builds a uia3 handle whose pattern lookup for patternId returns pattern (null = unsupported).
    private static ElementHandle Uia3Handle(int patternId, object? pattern, StringWriter? writer = null)
    {
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(patternId)).Returns(pattern!);
        return new ElementHandle("1", "uia3", native.Object) { Logger = writer is null ? null : new EngineLogger(writer) };
    }

    [Fact]
    public async Task Writes_NonUiaHandle_NoOp()
    {
        var element = new ElementHandle("1", "win32", new object());

        await SelectionAction.SelectAsync(element);
        await SelectionAction.AddToSelectionAsync(element);
        await SelectionAction.RemoveFromSelectionAsync(element);
    }

    [Fact]
    public async Task Writes_PatternNotSupported_NoOpAndLogs()
    {
        var writer = new StringWriter();
        var element = Uia3Handle(UIA_PatternIds.UIA_SelectionItemPatternId, null, writer);

        await SelectionAction.SelectAsync(element);

        Assert.Contains("SelectionItemPattern not supported, nothing selected", writer.ToString());
    }

    [Fact]
    public async Task SelectAsync_CallsOnlySelect()
    {
        var pattern = new Mock<IUIAutomationSelectionItemPattern>();
        var element = Uia3Handle(UIA_PatternIds.UIA_SelectionItemPatternId, pattern.Object);

        await SelectionAction.SelectAsync(element);

        pattern.Verify(p => p.Select(), Times.Once);
        pattern.Verify(p => p.AddToSelection(), Times.Never);
    }

    [Fact]
    public async Task AddToSelectionAsync_CallsOnlyAddToSelection()
    {
        var pattern = new Mock<IUIAutomationSelectionItemPattern>();
        var element = Uia3Handle(UIA_PatternIds.UIA_SelectionItemPatternId, pattern.Object);

        await SelectionAction.AddToSelectionAsync(element);

        pattern.Verify(p => p.AddToSelection(), Times.Once);
        pattern.Verify(p => p.Select(), Times.Never);
    }

    [Fact]
    public async Task RemoveFromSelectionAsync_CallsOnlyRemoveFromSelection()
    {
        var pattern = new Mock<IUIAutomationSelectionItemPattern>();
        var element = Uia3Handle(UIA_PatternIds.UIA_SelectionItemPatternId, pattern.Object);

        await SelectionAction.RemoveFromSelectionAsync(element);

        pattern.Verify(p => p.RemoveFromSelection(), Times.Once);
        pattern.Verify(p => p.Select(), Times.Never);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public async Task IsSelectedAsync_ReadsCurrentIsSelected(int raw, bool expected)
    {
        var pattern = new Mock<IUIAutomationSelectionItemPattern>();
        pattern.SetupGet(p => p.CurrentIsSelected).Returns(raw);
        var element = Uia3Handle(UIA_PatternIds.UIA_SelectionItemPatternId, pattern.Object);

        Assert.Equal(expected, await SelectionAction.IsSelectedAsync(element));
    }

    [Fact]
    public async Task IsSelectedAsync_UnsupportedOrNonUia_ReturnsFalse()
    {
        Assert.False(await SelectionAction.IsSelectedAsync(Uia3Handle(UIA_PatternIds.UIA_SelectionItemPatternId, null)));
        Assert.False(await SelectionAction.IsSelectedAsync(new ElementHandle("1", "win32", new object())));
    }

    [Fact]
    public async Task GetSelectedItemsAsync_UnsupportedOrNonUia_ReturnsEmpty()
    {
        Assert.Empty(await SelectionAction.GetSelectedItemsAsync(Uia3Handle(UIA_PatternIds.UIA_SelectionPatternId, null)));
        Assert.Empty(await SelectionAction.GetSelectedItemsAsync(new ElementHandle("1", "win32", new object())));
    }

    [Fact]
    public async Task GetSelectedItemsAsync_Supported_WrapsEachSelectedElement()
    {
        var item = new Mock<IUIAutomationElement>();
        item.Setup(e => e.GetRuntimeId()).Returns([42, 7]);
        var selected = new Mock<IUIAutomationElementArray>();
        selected.SetupGet(a => a.Length).Returns(1);
        selected.Setup(a => a.GetElement(0)).Returns(item.Object);
        var pattern = new Mock<IUIAutomationSelectionPattern>();
        pattern.Setup(p => p.GetCurrentSelection()).Returns(selected.Object);
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_SelectionPatternId)).Returns(pattern.Object);
        var container = new ElementHandle("1", "uia3", native.Object) { Provider = new Uia3Provider() };

        var items = await SelectionAction.GetSelectedItemsAsync(container);

        Assert.Equal("42.7", Assert.Single(items).Id);
    }
}
