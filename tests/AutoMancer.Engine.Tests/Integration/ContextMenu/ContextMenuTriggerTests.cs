// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Every trigger each test app supports opens its menu: right-click, Shift+F10 (WM_CONTEXTMENU from the keyboard), and left-click on a "⋯" button.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class ContextMenuTriggerTests(ContextMenuHostFixture host, WpfMenuHostFixture wpf) : IClassFixture<ContextMenuHostFixture>, IClassFixture<WpfMenuHostFixture>, IAsyncLifetime
{
    // Clicks the host's status label before each test: it resets the status, and a click elsewhere keeps this test's first right-click on the native area from pairing with the last test's into a double-click, which opens no menu.
    public Task InitializeAsync() => host.ResetStatusAsync();

    // Nothing to tear down per test; the fixtures own the apps.
    public Task DisposeAsync() => Task.CompletedTask;

    // The native #32768 menu, from its area by right-click or Shift+F10, and from the "⋯" button by left-click.
    [Theory]
    [InlineData("nativeArea", PopupTrigger.RightClick)]
    [InlineData("nativeArea", PopupTrigger.ShiftF10)]
    [InlineData("moreButton", PopupTrigger.LeftClick)]
    public Task NativeMenu_OpensFromTrigger(string automationId, PopupTrigger trigger) =>
        AssertOpensWithItemAsync(host.App, automationId, trigger, "Alpha");

    // WPF: the label's menu by right-click, the TextBox's built-in menu by right-click or Shift+F10, and the "⋯" button's menu by left-click.
    [Theory]
    [InlineData("menuLabel", PopupTrigger.RightClick, "Alpha")]
    [InlineData("editBox", PopupTrigger.RightClick, "Paste")]
    [InlineData("editBox", PopupTrigger.ShiftF10, "Paste")]
    [InlineData("moreButton", PopupTrigger.LeftClick, "More One")]
    public Task WpfMenu_OpensFromTrigger(string automationId, PopupTrigger trigger, string expectedItem) =>
        AssertOpensWithItemAsync(wpf.App, automationId, trigger, expectedItem);

    // Opens the menu of the element with automationId via trigger, checks it lists expectedItem, and closes it.
    private static async Task AssertOpensWithItemAsync(App app, string automationId, PopupTrigger trigger, string expectedItem)
    {
        var menu = await app.OpenContextMenuAsync(Locator.ByAutomationId(automationId), trigger);
        var items = await menu.FindAllAsync(Locator.ByControlType("MenuItem"));
        await menu.CloseAsync();

        Assert.Contains(items, item => item.Name == expectedItem);
    }
}
