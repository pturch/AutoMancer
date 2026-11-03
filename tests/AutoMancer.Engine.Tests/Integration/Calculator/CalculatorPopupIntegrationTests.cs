// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// WinUI3 popups in Calculator: a converter's unit ComboBox dropdown and a date picker's calendar flyout. Calculator relaunches in its last mode, so every test switches back to Standard, which the other Calculator tests expect.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class CalculatorPopupIntegrationTests : IAsyncLifetime
{
    private const string CalculatorAumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
    private static readonly Locator InputUnit = Locator.ByAutomationId("Units1");
    private static readonly Locator InputValue = Locator.ByAutomationId("Value1");

    private App? _app;

    // Launches Calculator and warms up on its navigation button, which every mode has.
    public async Task InitializeAsync()
    {
        _app = await App.LaunchPackagedAsync(CalculatorAumid);
        await _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 8_000 }).FindAsync(Locator.ByAutomationId("TogglePaneButton"));
    }

    // Switches back to Standard mode, so the next launch starts there, then kills Calculator; no-ops if InitializeAsync failed to launch.
    public async Task DisposeAsync()
    {
        if (_app is null) return;
        await SwitchModeAsync("Standard");
        await _app.FindAsync(Locator.ByAutomationId("CalculatorResults"));
        await _app.KillAsync();
    }

    // The unit ComboBox's dropdown is a WinUI3 windowed popup (Xaml_WindowedPopupClass): opened by a click or Alt+Down, a clicked unit is selected, which the input value's text shows. Ends on Inches, Calculator's remembered unit.
    [Fact]
    public async Task UnitComboDropdown_ClickedUnitIsSelected()
    {
        await SwitchModeAsync("Length");

        var byClick = await _app!.OpenPopupAsync(InputUnit, PopupTrigger.LeftClick, expect: Locator.ByControlType("ListItem"));
        await byClick.ClickAsync(Locator.ByName("Centimeters"));
        await byClick.WaitUntilClosedAsync();
        await Expect(_app, InputValue).ToContainTextAsync("Centimeters");

        var byAltDown = await _app.OpenPopupAsync(InputUnit, PopupTrigger.AltDown, expect: Locator.ByControlType("ListItem"));
        await byAltDown.ClickAsync(Locator.ByName("Inches"));
        await byAltDown.WaitUntilClosedAsync();
        await Expect(_app, InputValue).ToContainTextAsync("Inches");
    }

    // A date picker's calendar is a flyout with controls in its own popup window: clicking a day sets the date and closes it. The first DateText is the From date's.
    [Fact]
    public async Task DatePickerCalendar_ClickedDaySetsTheDate()
    {
        await SwitchModeAsync("Date");

        var calendar = await _app!.OpenPopupAsync(Locator.ByAutomationId("DateDiff_FromDate"), PopupTrigger.LeftClick, expect: Locator.ByControlType("DataItem"));
        await calendar.ClickAsync(Locator.ByName("15"));

        await calendar.WaitUntilClosedAsync();
        await Expect(_app, Locator.ByAutomationId("DateText")).ToContainTextAsync("15");
    }

    // Opens the navigation pane, picks the mode whose list item has automationId, and waits for the pane to close, so it can't take the next click.
    private async Task SwitchModeAsync(string automationId)
    {
        var mode = Locator.ByAutomationId(automationId);
        await _app!.ClickAsync(Locator.ByAutomationId("TogglePaneButton"));
        await _app.ClickAsync(mode);
        await _app.WaitForAsync(mode, WaitConditions.Not(WaitConditions.IsVisible()));
    }
}
