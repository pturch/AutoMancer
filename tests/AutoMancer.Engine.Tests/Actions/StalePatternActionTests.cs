// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

// Every Stage 2.8 pattern action reports an element that has left the UI tree as StaleElementError, so App's re-resolve-on-stale can retry it.
public sealed class StalePatternActionTests
{
    private const int UiaElementNotAvailable = unchecked((int)0x80040201);

    // One row per action: its name and a call that runs it against the given handle.
    public static TheoryData<string, Func<ElementHandle, Task>> Actions => new()
    {
        { "Toggle", e => ToggleAction.ExecuteAsync(e) },
        { "Expand", e => ExpandCollapseAction.ExpandAsync(e) },
        { "Collapse", e => ExpandCollapseAction.CollapseAsync(e) },
        { "Select", e => SelectionAction.SelectAsync(e) },
        { "AddToSelection", e => SelectionAction.AddToSelectionAsync(e) },
        { "RemoveFromSelection", e => SelectionAction.RemoveFromSelectionAsync(e) },
        { "IsSelected", e => SelectionAction.IsSelectedAsync(e) },
        { "GetSelectedItems", e => SelectionAction.GetSelectedItemsAsync(e) },
        { "GridRowCount", e => GridAction.GetRowCountAsync(e) },
        { "GridColumnCount", e => GridAction.GetColumnCountAsync(e) },
        { "GridCell", e => GridAction.GetCellAsync(e, 0, 0) },
        { "SetRangeValue", e => RangeValueAction.SetValueAsync(e, 5) },
        { "GetRangeValue", e => RangeValueAction.GetValueAsync(e) },
    };

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task ElementGoneDuringPatternLookup_ThrowsStaleElementError(string action, Func<ElementHandle, Task> run)
    {
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(It.IsAny<int>())).Throws(new COMException("gone", UiaElementNotAvailable));
        var element = new ElementHandle("1", "uia3", native.Object) { Provider = new Uia3Provider() };

        var error = await Assert.ThrowsAsync<StaleElementError>(() => run(element));

        Assert.Same(element, error.Element);
        Assert.NotNull(action);
    }

    // A failure that isn't "element gone" must come through untouched, not be reported as stale.
    [Fact]
    public async Task UnrelatedComFailure_PropagatesUnchanged()
    {
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(It.IsAny<int>())).Throws(new COMException("something else", unchecked((int)0x80004005)));
        var element = new ElementHandle("1", "uia3", native.Object) { Provider = new Uia3Provider() };

        await Assert.ThrowsAsync<COMException>(() => ToggleAction.ExecuteAsync(element));
    }
}
