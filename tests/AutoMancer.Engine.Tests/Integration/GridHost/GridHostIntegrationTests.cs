// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using static AutoMancer.Testing.Assertions;
using AutoMancer.Engine.Core;
using Interop.UIAutomationClient;
using static AutoMancer.Engine.Tests.Integration.GridHostFixture;

namespace AutoMancer.Engine.Tests.Integration;

// Live checks of the Stage 2.8 grid, selection, toggle, expand/collapse and range-value actions against the GridHost fixture app's DataGrid, ListBox, CheckBox, ComboBox, Slider and ProgressBar.
[Trait("Category", "Integration")]
public sealed class GridHostIntegrationTests(GridHostFixture fixture) : IClassFixture<GridHostFixture>
{
    private readonly App _app = fixture.App.WithOptions(new AppOptions { ProviderChain = ["uia3"] });

    // The grid reports its 4 data rows and 3 columns, through both UIA providers.
    [Theory]
    [InlineData("uia3")]
    [InlineData("uia2")]
    public async Task Grid_ReportsRowAndColumnCounts(string provider)
    {
        var app = _app.WithOptions(new AppOptions { ProviderChain = [provider] });

        Assert.Equal(4, await app.GetGridRowCountAsync(FruitGrid));
        Assert.Equal(3, await app.GetGridColumnCountAsync(FruitGrid));
    }

    // GetGridCellAsync returns a normal handle whose text is the cell's content, so the rest of the API works on it; checked through both UIA providers.
    [Theory]
    [InlineData("uia3")]
    [InlineData("uia2")]
    public async Task GetGridCellAsync_ReturnsCellWithItsText(string provider)
    {
        var app = _app.WithOptions(new AppOptions { ProviderChain = [provider] });

        var cell = await app.GetGridCellAsync(FruitGrid, row: 2, column: 1);

        Assert.Equal("Dark red", await app.GetValueAsync(Locator.ByRuntimeId(cell!.Id)));
    }

    // A control with no grid pattern reads as an empty grid: zero counts and no cell, not an exception.
    [Fact]
    public async Task NonGridControl_ReturnsZeroCountsAndNullCell()
    {
        Assert.Equal(0, await _app.GetGridRowCountAsync(GreekList));
        Assert.Equal(0, await _app.GetGridColumnCountAsync(GreekList));
        Assert.Null(await _app.GetGridCellAsync(GreekList, 0, 0));
    }

    // Select picks one item, AddToSelection extends it, RemoveFromSelection shrinks it, and GetSelectedItems/IsSelected agree at each step.
    [Theory]
    [InlineData("uia3")]
    [InlineData("uia2")]
    public async Task Selection_SelectAddRemove_TracksSelectedItems(string provider)
    {
        var app = _app.WithOptions(new AppOptions { ProviderChain = [provider] });
        var alpha = Locator.ByName("Alpha");
        var gamma = Locator.ByName("Gamma");

        await app.SelectAsync(alpha);
        Assert.True(await app.IsSelectedAsync(alpha));
        Assert.Equal(["Alpha"], Names(await app.GetSelectedItemsAsync(GreekList)));

        await app.AddToSelectionAsync(gamma);
        Assert.Equal(["Alpha", "Gamma"], Names(await app.GetSelectedItemsAsync(GreekList)).Order());

        await app.RemoveFromSelectionAsync(alpha);
        Assert.False(await app.IsSelectedAsync(alpha));
        Assert.Equal(["Gamma"], Names(await app.GetSelectedItemsAsync(GreekList)));

        // Select replaces the whole selection, unlike AddToSelection.
        await app.SelectAsync(alpha);
        Assert.Equal(["Alpha"], Names(await app.GetSelectedItemsAsync(GreekList)));
    }

    // ToggleAsync flips a real CheckBox's state, and toggling again restores it.
    [Theory]
    [InlineData("uia3")]
    [InlineData("uia2")]
    public async Task Toggle_CheckBox_FlipsState(string provider)
    {
        var app = _app.WithOptions(new AppOptions { ProviderChain = [provider] });
        var before = await ToggleStateAsync(app, LoggingCheck);

        await app.ToggleAsync(LoggingCheck);
        var after = await ToggleStateAsync(app, LoggingCheck);
        await app.ToggleAsync(LoggingCheck);

        Assert.NotEqual(before, after);
        Assert.Equal(before, await ToggleStateAsync(app, LoggingCheck));
    }

    // ExpandAsync drops the ComboBox's list open (its items become findable) and CollapseAsync closes it.
    [Theory]
    [InlineData("uia3")]
    [InlineData("uia2")]
    public async Task ExpandCollapse_ComboBox_ShowsAndHidesItems(string provider)
    {
        var app = _app.WithOptions(new AppOptions { ProviderChain = [provider] });
        await app.ExpandAsync(SizeCombo);
        await app.FindAsync(Locator.ByName("Large"));

        await app.CollapseAsync(SizeCombo);
        await app.WaitUntilGoneAsync(Locator.ByName("Large"));
    }

    // SetRangeValue moves the slider directly and GetRangeValue reads the new value back along with the declared 0-10 range.
    [Theory]
    [InlineData("uia3")]
    [InlineData("uia2")]
    public async Task RangeValue_Slider_SetThenGetRoundTrips(string provider)
    {
        var app = _app.WithOptions(new AppOptions { ProviderChain = [provider] });
        await app.SetRangeValueAsync(VolumeSlider, 7);

        Assert.Equal(new RangeValueInfo(7, 0, 10), await app.GetRangeValueAsync(VolumeSlider));
    }

    // A progress bar's value and range read back, and setting it throws because RangeValuePattern is read-only there.
    [Theory]
    [InlineData("uia3")]
    [InlineData("uia2")]
    public async Task RangeValue_ProgressBar_ReadsButRejectsWrites(string provider)
    {
        var app = _app.WithOptions(new AppOptions { ProviderChain = [provider] });
        Assert.Equal(new RangeValueInfo(40, 0, 100), await app.GetRangeValueAsync(LoadProgress));

        await Assert.ThrowsAnyAsync<Exception>(() => app.SetRangeValueAsync(LoadProgress, 80));
    }

    // A control with no range pattern reads as null.
    [Theory]
    [InlineData("uia3")]
    [InlineData("uia2")]
    public async Task RangeValue_NonRangeControl_ReturnsNull(string provider)
    {
        var app = _app.WithOptions(new AppOptions { ProviderChain = [provider] });
        Assert.Null(await app.GetRangeValueAsync(LoggingCheck));
    }

    // A throwaway instance (Notepad is single-instance and shared, so it can't be closed here) is running until its window is closed, after which ToHaveExited passes and ToBeRunning fails.
    [Fact]
    public async Task ToHaveExitedAsync_AfterClosingWindow_PassesAndRunningFails()
    {
        var disposable = await App.LaunchAsync(fixture.ExePath, windowMatch: new WindowMatchOptions { RequireNewWindow = true });
        try
        {
            await Expect(disposable).ToBeRunningAsync();
            await Assert.ThrowsAsync<AutoMancer.Testing.ExpectFailedError>(() => Expect(disposable).ToHaveExitedAsync(timeoutMs: 300));

            await disposable.CloseWindowAsync();

            await Expect(disposable).ToHaveExitedAsync(timeoutMs: 5_000);
            await Assert.ThrowsAsync<AutoMancer.Testing.ExpectFailedError>(() => Expect(disposable).ToBeRunningAsync(timeoutMs: 300));
        }
        finally
        {
            await disposable.KillAsync();
            await disposable.DisposeAsync();
        }
    }

    // Names of the handles, in the order returned.
    private static IEnumerable<string> Names(IEnumerable<ElementHandle> handles) => handles.Select(h => h.Name);

    // Reads the element's current TogglePattern state straight from UIA, since the engine has no read surface for it yet.
    private static async Task<int> ToggleStateAsync(App app, Locator locator)
    {
        var handle = await app.FindAsync(locator);
        return handle.NativeHandle switch
        {
            IUIAutomationElement uia3 => (int)((IUIAutomationTogglePattern)uia3.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId)).CurrentToggleState,
            System.Windows.Automation.AutomationElement uia2 => (int)((System.Windows.Automation.TogglePattern)uia2.GetCurrentPattern(System.Windows.Automation.TogglePattern.Pattern)).Current.ToggleState,
            _ => throw new InvalidOperationException("Unexpected native handle."),
        };
    }
}
