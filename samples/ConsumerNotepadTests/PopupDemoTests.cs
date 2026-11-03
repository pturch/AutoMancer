// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Opens a popup that isn't a context menu: Notepad's File menu, reached through the click that opens it.
[Collection("ConsumerNotepad")]
public sealed class PopupDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    // OpenPopupAsync clicks File and waits for the new surface containing expect; the popup is an App scoped to the menu, so its assertions see only the menu's items, and CloseAsync dismisses it.
    [Fact]
    public async Task OpenPopupAsync_FileMenu_ListsItsCommands()
    {
        var fileMenu = await App.OpenPopupAsync(Locator.ByName("File"), PopupTrigger.LeftClick, expect: Locator.ByName("Save as"));

        await Assertions.Expect(fileMenu, Locator.ByName("New tab")).ToBeVisibleAsync();
        await fileMenu.CloseAsync();

        Assert.False(await fileMenu.IsOpenAsync());
    }
}
