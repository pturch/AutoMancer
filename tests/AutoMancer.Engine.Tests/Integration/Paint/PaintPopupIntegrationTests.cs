// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// WinUI3 popups in Paint: an in-window dialog with no window of its own, which only popup detection's in-window step can find, and a toolbar dropdown flyout.
[Collection("Paint")]
[Trait("Category", "Integration")]
public sealed class PaintPopupIntegrationTests(PaintFixture fixture) : IClassFixture<PaintFixture>
{
    // "Resize and skew" opens a XAML dialog inside Paint's window; found through the in-window step (expect names something in it), its controls work, and Cancel closes it.
    [Fact]
    public async Task ResizeDialog_InWindow_ControlsWorkAndCancelCloses()
    {
        var dialog = await fixture.App.OpenPopupAsync(Locator.ByName("Resize and skew"), PopupTrigger.LeftClick, expect: Locator.ByName("Cancel"));

        await dialog.ClickAsync(Locator.ByName("Pixels"));
        await Expect(dialog, Locator.ByAutomationId("HorizontalResizeTextBox")).ToBeEnabledAsync();
        await dialog.ClickAsync(Locator.ByName("Cancel"));

        await dialog.WaitUntilClosedAsync();
    }

    // The Rotate dropdown is a WinUI3 popup window listing its commands; CloseAsync dismisses it.
    [Fact]
    public async Task RotateDropdown_ListsItsCommandsThenCloses()
    {
        var flyout = await fixture.App.OpenPopupAsync(Locator.ByAutomationId("RotateDropdown"), PopupTrigger.LeftClick, expect: Locator.ByControlType("MenuItem"));

        var items = await flyout.FindAllAsync(Locator.ByControlType("MenuItem"));
        await flyout.CloseAsync();

        // A prefix match: Paint writes "180º" with an ordinal indicator, not a degree sign.
        Assert.Contains(items, item => item.Name!.StartsWith("Rotate 180"));
    }
}
