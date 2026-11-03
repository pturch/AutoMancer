// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Notepad's WinUI3 context menu, a real-app popup window: opened by right-click and Shift+F10, its items read with their enabled state, and an item clicked for real.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class NotepadContextMenuIntegrationTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    private static readonly Locator Document = Locator.ByControlType("Document");

    // On an empty document Undo is listed but disabled, whichever trigger opened the menu.
    [Theory]
    [InlineData(PopupTrigger.RightClick)]
    [InlineData(PopupTrigger.ShiftF10)]
    public async Task OpenContextMenuAsync_ListsUndoAsDisabledOnEmptyDocument(PopupTrigger trigger)
    {
        await fixture.App.ClearAsync(Document);

        var menu = await fixture.App.OpenContextMenuAsync(Document, trigger);
        var items = await menu.FindAllAsync(Locator.ByControlType("MenuItem"));
        await menu.CloseAsync();

        Assert.Contains(items, item => item.Name == "Undo" && item.IsEnabled == false);
    }

    // Undo from the context menu removes typed text; TypeDirectAsync sends real keystrokes, since TypeAsync's set-value leaves nothing to undo.
    [Fact]
    public async Task ClickContextMenuItemAsync_Undo_RemovesTypedText()
    {
        await fixture.App.ClearAsync(Document);
        await fixture.App.ClickAsync(Document);
        await fixture.App.TypeDirectAsync("undo me");

        await fixture.App.ClickContextMenuItemAsync(Document, "Undo");

        await Expect(fixture.App, Document).ToHaveValueAsync("");
    }
}
