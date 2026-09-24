// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Demonstrates scoping a search to an element's children — MenuBar is resolved fresh from its Locator on every call below, rather than resolved once and reused.
[Collection("ConsumerNotepad")]
public sealed class ToolbarScopeDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    // Notepad's menu bar (AutomationId "MenuBar") contains File/Edit/View as MenuItem children; scoping to it isolates those three from any other MenuItem elsewhere in the window.
    private static readonly Locator MenuBar = Locator.ByAutomationId("MenuBar");

    [Fact]
    public async Task FindAllAsync_ScopedToMenuBar_EnumeratesFileEditView()
    {
        var menuItems = await App.FindAllScopedAsync(Locator.ByControlType("MenuItem"), MenuBar);

        var names = menuItems.Select(m => m.Name).ToList();
        Assert.Equal(["File", "Edit", "View"], names);
    }

    // Scoping also narrows a broad ControlType query ("any MenuItem") to just the ones inside a specific container, not just disambiguating identically-named elements.
    [Fact]
    public async Task FindAsync_ScopedToMenuBar_FindsEditMenuItemSpecifically()
    {
        var edit = await App.FindScopedAsync(Locator.ByName("Edit"), MenuBar);

        Assert.Equal("Edit", edit.AutomationId);
    }
}
