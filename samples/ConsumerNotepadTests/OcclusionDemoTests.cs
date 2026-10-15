// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Demonstrates IsUnobscuredAsync, an opt-in check of whether a click at an element's center would actually reach it right now.
[Collection("ConsumerNotepad")]
public sealed class OcclusionDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    private static readonly Locator Settings = Locator.ByAutomationId("SettingsButton");

    // The Save-changes dialog covers the whole window, so Settings reports obscured while it's up and clear once it's dismissed; the dialog's own buttons stay clear throughout.
    [Fact]
    public async Task IsUnobscuredAsync_FalseWhileSaveChangesDialogCoversButton()
    {
        // IsUnobscuredAsync never foregrounds, so bring Notepad forward first, as any action would.
        await App.ClickAsync(Locator.ByControlType("Document"));
        Assert.True(await App.IsUnobscuredAsync(Settings));

        // A second tab with unsaved text, so Ctrl+W raises the dialog and leaves the shared window open.
        await App.PressKeyAsync(Key.N, new KeyModifiers(Control: true));
        await App.TypeAsync(Locator.ByControlType("Document"), "occlusion demo");
        await App.PressKeyAsync(Key.W, new KeyModifiers(Control: true));
        var dontSave = Locator.ByAutomationId("SecondaryButton");
        await App.FindAsync(dontSave);

        Assert.False(await App.IsUnobscuredAsync(Settings));
        Assert.True(await App.IsUnobscuredAsync(dontSave));

        await App.ClickAsync(dontSave);
        await App.WaitUntilGoneAsync(dontSave);
        Assert.True(await App.IsUnobscuredAsync(Settings));
    }
}
