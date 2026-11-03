// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Drives Notepad's right-click menu: one-call item clicks, inspecting the menu before acting, and opening it from the keyboard.
[Collection("ConsumerNotepad")]
public sealed class ContextMenuDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    private static readonly Locator Document = Locator.ByControlType("Document");

    // The one-call shortcut: type, then undo it from the context menu instead of Ctrl+Z; TypeDirectAsync sends real keystrokes, since TypeAsync's set-value leaves nothing to undo.
    [Fact]
    public async Task ClickContextMenuItemAsync_UndoesTyping()
    {
        await App.ClearAsync(Document);
        await App.ClickAsync(Document);
        await App.TypeDirectAsync("undo me");

        await App.ClickContextMenuItemAsync(Document, "Undo");

        await Expect(Document).ToHaveValueAsync("");
    }

    // Open, inspect, then decide: on an empty document Undo is listed but disabled, so the test checks that and closes without clicking.
    [Fact]
    public async Task OpenContextMenuAsync_ReportsUndoDisabledOnEmptyDocument()
    {
        await App.ClearAsync(Document);

        var menu = await App.OpenContextMenuAsync(Document);
        var items = await menu.FindAllAsync(Locator.ByControlType("MenuItem"));
        await menu.CloseAsync();

        Assert.Contains(items, item => item.Name == "Undo" && item.IsEnabled == false);
    }

    // A popup is an App scoped to the menu, so App's retrying assertions and actions work inside it unchanged: wait for Undo to be enabled, then click it.
    [Fact]
    public async Task OpenContextMenuAsync_AssertsAndClicksInsideTheMenu()
    {
        var undo = Locator.ByName("Undo");
        await App.ClearAsync(Document);
        await App.ClickAsync(Document);
        await App.TypeDirectAsync("undo me");

        var menu = await App.OpenContextMenuAsync(Document);
        await Assertions.Expect(menu, undo).ToBeEnabledAsync();
        await menu.ClickAsync(undo);

        await Expect(Document).ToHaveValueAsync("");
    }

    // Shift+F10 opens the same menu from the keyboard, the way a keyboard-only user would.
    [Fact]
    public async Task OpenContextMenuAsync_ShiftF10_OpensSameMenu()
    {
        var menu = await App.OpenContextMenuAsync(Document, PopupTrigger.ShiftF10);
        var items = await menu.FindAllAsync(Locator.ByControlType("MenuItem"));
        await menu.CloseAsync();

        Assert.Contains(items, item => item.Name == "Undo");
    }
}
