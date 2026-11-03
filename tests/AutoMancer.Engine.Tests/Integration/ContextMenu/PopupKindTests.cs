// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Popups other than context menus, through the generic Popup handle: ComboBox dropdowns, tooltips, and a flyout with controls inside.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class PopupKindTests(ContextMenuHostFixture host, WpfMenuHostFixture wpf) : IClassFixture<ContextMenuHostFixture>, IClassFixture<WpfMenuHostFixture>, IAsyncLifetime
{
    private static readonly Locator ListItems = Locator.ByControlType("ListItem");

    // Resets both apps' status before each test, so a test's status assertion can't pass on an earlier test's pick.
    public async Task InitializeAsync()
    {
        await host.ResetStatusAsync();
        await wpf.ResetStatusAsync();
    }

    // Nothing to tear down per test; the fixtures own the apps.
    public Task DisposeAsync() => Task.CompletedTask;

    // A WPF ComboBox's dropdown is its own Popup window: opened by Alt+Down or a click, an item clicked inside it is selected and the dropdown closes.
    [Theory]
    [InlineData(PopupTrigger.AltDown, "Banana")]
    [InlineData(PopupTrigger.LeftClick, "Cherry")]
    public async Task WpfComboDropdown_ClickedItemIsSelected(PopupTrigger trigger, string item)
    {
        var dropdown = await wpf.App.OpenPopupAsync(TestAppFixture.FruitCombo, trigger, expect: ListItems);

        await dropdown.ClickAsync(Locator.ByName(item));

        await dropdown.WaitUntilClosedAsync();
        await Expect(wpf.App, TestAppFixture.Status).ToHaveNameAsync($"fruit {item}");
    }

    // A WinForms ComboBox's dropdown is its ComboLBox list window. Its items report a successful UIA Invoke that does nothing, so the item is picked with the keyboard: its first letter, then Enter.
    [Theory]
    [InlineData(PopupTrigger.AltDown, "Banana")]
    [InlineData(PopupTrigger.LeftClick, "Cherry")]
    public async Task WinFormsComboDropdown_ItemPickedWithKeyboardIsSelected(PopupTrigger trigger, string item)
    {
        var dropdown = await host.App.OpenPopupAsync(TestAppFixture.FruitCombo, trigger, expect: ListItems);
        Assert.Equal(["Apple", "Banana", "Cherry"], (await dropdown.FindAllAsync(ListItems)).Select(i => i.Name));

        await dropdown.TypeDirectAsync(item[..1]);
        await dropdown.PressKeyAsync(Key.Enter);

        await dropdown.WaitUntilClosedAsync();
        await Expect(host.App, TestAppFixture.Status).ToHaveNameAsync($"fruit {item}");
    }

    // A hovered label's tooltip is found with or without expect, its text read, and it closes once the pointer moves away; Escape doesn't close a tooltip, so CloseAsync can't.
    [Theory]
    [InlineData("winforms", true)]
    [InlineData("winforms", false)]
    [InlineData("wpf", true)]
    [InlineData("wpf", false)]
    public async Task Tooltip_TextIsReadThenClosesWhenPointerLeaves(string appName, bool withExpect)
    {
        var app = appName == "wpf" ? wpf.App : host.App;

        var tooltip = await app.OpenPopupAsync(TestAppFixture.TipLabel, PopupTrigger.Hover, expect: withExpect ? Locator.ByControlType("ToolTip") : null);
        var snapshot = await tooltip.SnapshotAsync();

        Assert.Contains(snapshot[0].DescendantsAndSelf(), element => element.Name == "Helpful tip");
        await app.HoverAsync(TestAppFixture.Status);
        await tooltip.WaitUntilClosedAsync();
    }

    // A flyout opened by a button holds real controls: text typed into it and a button clicked inside it reach the app, and clicking the button closes it.
    [Fact]
    public async Task WpfFlyout_ControlsInsideItWork()
    {
        var flyout = await wpf.App.OpenPopupAsync(WpfMenuHostFixture.FilterButton, PopupTrigger.LeftClick, expect: Locator.ByName("Apply"));

        await flyout.TypeAsync(WpfMenuHostFixture.FilterText, "abc");
        await flyout.ClickAsync(Locator.ByName("Apply"));

        await flyout.WaitUntilClosedAsync();
        await Expect(wpf.App, TestAppFixture.Status).ToHaveNameAsync("applied abc");
    }
}
