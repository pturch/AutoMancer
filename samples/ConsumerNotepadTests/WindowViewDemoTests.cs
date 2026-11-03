// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Drives Notepad's Open dialog through a window view: an App scoped to one top-level window of the same app, for dialogs and secondary windows.
[Collection("ConsumerNotepad")]
public sealed class WindowViewDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    private static readonly Locator FileNameBox = Locator.ByClassName("Edit");
    private static readonly Locator Cancel = Locator.ByName("Cancel");

    // WaitForWindowAsync returns a view rooted at the dialog, so its finds, actions and assertions stay inside it; Cancel closes the dialog, and the view can wait for that.
    [Fact]
    public async Task WaitForWindowAsync_DrivesTheOpenDialog()
    {
        await App.HotkeyAsync(new KeyModifiers(Control: true), [Key.O]);
        var dialog = await App.WaitForWindowAsync(new WindowMatchOptions { TitleContains = "Open" });

        await dialog.TypeAsync(FileNameBox, "notes.txt");
        await Assertions.Expect(dialog, FileNameBox).ToHaveValueAsync("notes.txt");
        await dialog.ClickAsync(Cancel);

        await dialog.WaitUntilGoneAsync(Cancel);
    }

    // A view shares the app's process rather than owning it, so disposing one (here with await using) leaves Notepad running.
    [Fact]
    public async Task DisposingAView_LeavesTheAppRunning()
    {
        await App.HotkeyAsync(new KeyModifiers(Control: true), [Key.O]);
        await using (var dialog = await App.WaitForWindowAsync(new WindowMatchOptions { TitleContains = "Open" }))
        {
            await dialog.PressKeyAsync(Key.Escape);
            await dialog.WaitUntilGoneAsync(Cancel);
        }

        await Expect(Locator.ByControlType("Document")).ToBeVisibleAsync();
    }
}
