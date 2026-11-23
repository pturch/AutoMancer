// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.RegularExpressions;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Shows Expect()'s window-level assertions (title, state, bounds, dialogs, process and resource checks) against a live Notepad window.
[Collection("ConsumerNotepad")]
public sealed class AppExpectDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    // The window's title is checked by regex (or exact string), and a wrong expectation fails with the title it actually had.
    [Fact]
    public async Task ToHaveTitleAsync_MatchesByRegex_AndReportsTheActualTitleOnFailure()
    {
        await Expect().ToHaveTitleAsync(new Regex("Notepad"));

        var failure = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect().ToHaveTitleAsync("Wrong title", timeoutMs: 300));
        Assert.Contains("the title was", failure.Message);
    }

    // SetWindowStateAsync changes the window and the matching assertion retries until it sees the change, with no sleep in between.
    [Fact]
    public async Task ToBeMaximizedAsync_FollowsSetWindowStateAsync()
    {
        try
        {
            await App.SetWindowStateAsync(WindowState.Maximized);
            await Expect().ToBeMaximizedAsync();
        }
        finally
        {
            await App.SetWindowStateAsync(WindowState.Normal);
        }
    }

    // Ctrl+O opens the Open dialog, which ToHaveDialogAsync finds by title and ToHaveNoDialogAsync sees disappear once it is dismissed.
    [Fact]
    public async Task ToHaveDialogAsync_ThenToHaveNoDialogAsync_AroundTheOpenDialog()
    {
        await Expect().ToHaveNoDialogAsync();

        await App.HotkeyAsync(new KeyModifiers(Control: true), [Key.O]);
        var dialog = await App.WaitForWindowAsync(new WindowMatchOptions { TitleContains = "Open" });
        try
        {
            await Expect().ToHaveDialogAsync(new WindowMatchOptions { TitleContains = "Open" });
        }
        finally
        {
            await dialog.PressKeyAsync(Key.Escape);
        }
        await Expect().ToHaveNoDialogAsync();
    }

    // End-of-test health checks: the app still responds, its window has stopped changing, and its memory and handle use stay under loose ceilings that would catch a runaway leak.
    [Fact]
    public async Task EndOfTestChecks_RespondingStableAndWithinResourceBounds()
    {
        await Expect().ToBeRespondingAsync();
        await Expect().ToBeVisuallyStableAsync();
        await Expect().ToHaveMemoryBelowAsync(2L * 1024 * 1024 * 1024);
        await Expect().ToHaveHandleCountBelowAsync(20_000);
    }
}
