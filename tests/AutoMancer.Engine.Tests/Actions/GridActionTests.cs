// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

// Covers GridAction: zero counts and a null cell when unsupported, counts from GridPattern, and cells wrapped as handles.
public sealed class GridActionTests
{
    // Builds a uia3 handle whose GridPattern lookup returns pattern (null = unsupported).
    private static ElementHandle Uia3Grid(object? pattern)
    {
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_GridPatternId)).Returns(pattern!);
        return new ElementHandle("1", "uia3", native.Object) { Provider = new Uia3Provider() };
    }

    [Fact]
    public async Task NonUiaHandle_ReturnsZeroCountsAndNullCell()
    {
        var grid = new ElementHandle("1", "win32", new object());

        Assert.Equal(0, await GridAction.GetRowCountAsync(grid));
        Assert.Equal(0, await GridAction.GetColumnCountAsync(grid));
        Assert.Null(await GridAction.GetCellAsync(grid, 0, 0));
    }

    [Fact]
    public async Task PatternNotSupported_ReturnsZeroCountsAndNullCell()
    {
        var grid = Uia3Grid(null);

        Assert.Equal(0, await GridAction.GetRowCountAsync(grid));
        Assert.Equal(0, await GridAction.GetColumnCountAsync(grid));
        Assert.Null(await GridAction.GetCellAsync(grid, 0, 0));
    }

    [Fact]
    public async Task Counts_ReadFromGridPattern()
    {
        var pattern = new Mock<IUIAutomationGridPattern>();
        pattern.SetupGet(p => p.CurrentRowCount).Returns(5);
        pattern.SetupGet(p => p.CurrentColumnCount).Returns(3);
        var grid = Uia3Grid(pattern.Object);

        Assert.Equal(5, await GridAction.GetRowCountAsync(grid));
        Assert.Equal(3, await GridAction.GetColumnCountAsync(grid));
    }

    [Fact]
    public async Task GetCellAsync_WrapsTheElementAtRowAndColumn()
    {
        var cell = new Mock<IUIAutomationElement>();
        cell.Setup(e => e.GetRuntimeId()).Returns([9, 4]);
        var pattern = new Mock<IUIAutomationGridPattern>();
        pattern.Setup(p => p.GetItem(2, 1)).Returns(cell.Object);

        var result = await GridAction.GetCellAsync(Uia3Grid(pattern.Object), 2, 1);

        Assert.Equal("9.4", result!.Id);
    }

    [Fact]
    public async Task GetCellAsync_NoElementAtPosition_ReturnsNull()
    {
        var pattern = new Mock<IUIAutomationGridPattern>();
        pattern.Setup(p => p.GetItem(0, 0)).Returns((IUIAutomationElement)null!);

        Assert.Null(await GridAction.GetCellAsync(Uia3Grid(pattern.Object), 0, 0));
    }
}
