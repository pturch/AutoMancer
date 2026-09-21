// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Integration;

// Covers FindScopedAsync/FindAllScopedAsync end to end against live Notepad: a ContentDialog scope, a MenuBar scope, and scope actually excluding matches outside it.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class ScopedFindIntegrationTests
{
    [Fact]
    public async Task FindScopedAsync_ByName_ScopedToTheSaveChangesDialog_FindsDontSaveButton()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            await app.PressKeyAsync(Key.N, new KeyModifiers(Control: true));
            await app.ClickAsync(Locator.ByControlType("Document"));
            await app.TypeAsync(Locator.ByControlType("Document"), "scoped find test");
            await app.PressKeyAsync(Key.W, new KeyModifiers(Control: true));

            // Confirmed live (not assumed): two ClassName="Popup" windows exist at this point, neither with an AutomationId; Name="Notepad" is unique to the dialog.
            var dontSave = await app.FindScopedAsync(
                Locator.ByName("Don't save"),
                Locator.ByName("Notepad"));

            Assert.Equal("SecondaryButton", dontSave.AutomationId);
            await app.ClickAsync(Locator.ByAutomationId("SecondaryButton"));
        }
        finally
        {
            await app.KillAsync();
        }
    }

    [Fact]
    public async Task FindAllScopedAsync_ScopedToMenuBar_EnumeratesFileEditView()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var menuItems = await app.FindAllScopedAsync(Locator.ByControlType("MenuItem"), Locator.ByAutomationId("MenuBar"));

            Assert.Equal(["File", "Edit", "View"], menuItems.Select(m => m.Name).ToList());
        }
        finally
        {
            await app.KillAsync();
        }
    }

    [Fact]
    public async Task FindScopedAsync_ScopedToMenuBar_FindsEditMenuItemSpecifically()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var edit = await app.FindScopedAsync(Locator.ByName("Edit"), Locator.ByAutomationId("MenuBar"));

            Assert.Equal("Edit", edit.AutomationId);
        }
        finally
        {
            await app.KillAsync();
        }
    }

    // Proves scope actually restricts the search rather than just being a starting point for a wider walk — the Document editor is a real element in the window, just not under MenuBar.
    [Fact]
    public async Task FindScopedAsync_ScopedToMenuBar_LocatorMatchingOnlyOutsideScope_ThrowsElementNotFoundError()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            await Assert.ThrowsAsync<ElementNotFoundError>(() =>
                app.FindScopedAsync(Locator.ByControlType("Document"), Locator.ByAutomationId("MenuBar")));
        }
        finally
        {
            await app.KillAsync();
        }
    }
}
