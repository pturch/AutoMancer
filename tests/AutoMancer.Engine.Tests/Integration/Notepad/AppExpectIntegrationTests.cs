// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.RegularExpressions;
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Testing;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Live checks of AppExpect's window-level assertions against Notepad, using the Stage 2.8 clipboard and pattern actions for setup and teardown.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class AppExpectIntegrationTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    private static readonly Locator Document = Locator.ByControlType("Document");
    private readonly App _app = fixture.App.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 3_000 });

    // The window title names Notepad, and a wrong title fails with the title it actually had.
    [Fact]
    public async Task ToHaveTitleAsync_NotepadWindow_MatchesAndRejects()
    {
        await Expect(_app).ToHaveTitleAsync(new Regex("Notepad"));

        var error = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(_app).ToHaveTitleAsync("Definitely Not The Title", timeoutMs: 300));
        Assert.Contains("the title was", error.Message);
    }

    // Pasting clipboard text (set through the clipboard action) into the Document, then clearing it, round-trips through the app; the title reflects the unsaved edit.
    [Fact]
    public async Task ToHaveTitleAsync_AfterDocumentChange_ReflectsUnsavedEdit()
    {
        await _app.SetClipboardTextAsync("edited via clipboard");
        try
        {
            await _app.ClickAsync(Document);
            await _app.PressKeyAsync(Key.V, new KeyModifiers(Control: true));
            await Expect(_app, Document).ToHaveValueAsync("edited via clipboard");

            await Expect(_app).ToHaveTitleAsync(new Regex(@"\*|edited"));
        }
        finally
        {
            await _app.ClearAsync(Document);
        }
    }

    // Maximize then minimize through WindowAction are each seen by their assertion, a wrong state fails, and the window is restored for the other tests.
    [Fact]
    public async Task ToBeMaximizedAsync_And_ToBeMinimizedAsync_FollowWindowState()
    {
        try
        {
            await _app.SetWindowStateAsync(WindowState.Maximized);
            await Expect(_app).ToBeMaximizedAsync();
            await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(_app).ToBeMinimizedAsync(timeoutMs: 300));

            await _app.SetWindowStateAsync(WindowState.Minimized);
            await Expect(_app).ToBeMinimizedAsync();
            await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(_app).ToBeMaximizedAsync(timeoutMs: 300));
        }
        finally
        {
            await _app.SetWindowStateAsync(WindowState.Normal);
            // A minimized window's elements read offscreen, so this waits out the restore before the next test acts on the shared window.
            await _app.WaitForAsync(Document, WaitConditions.IsVisible());
        }
        await Expect(_app).ToBeForegroundAsync();
        await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(_app).ToBeMaximizedAsync(timeoutMs: 300));
    }

    // The window's own bounds satisfy ToHaveBounds, a window 100px off does not, and resizing is picked up.
    [Fact]
    public async Task ToHaveBoundsAsync_CurrentBoundsPass_OffByHundredFails_ResizeFollowed()
    {
        var bounds = await _app.GetWindowSizeAsync();
        await Expect(_app).ToHaveBoundsAsync(bounds);

        var off = bounds with { Width = bounds.Width + 100 };
        await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(_app).ToHaveBoundsAsync(off, timeoutMs: 300));

        try
        {
            await _app.ResizeWindowAsync((int)bounds.Width - 80, (int)bounds.Height - 40);
            await Expect(_app).ToHaveBoundsAsync(new Rect(bounds.X, bounds.Y, bounds.Width - 80, bounds.Height - 40), tolerancePx: 4);
        }
        finally
        {
            await _app.ResizeWindowAsync((int)bounds.Width, (int)bounds.Height);
        }
    }

    // The File > Open dialog is seen as a dialog and as one more window, and both clear once it is dismissed.
    [Fact]
    public async Task OpenDialog_IsSeenAsDialogAndExtraWindow_ThenGone()
    {
        await Expect(_app).ToHaveNoDialogAsync();
        await Expect(_app).ToHaveWindowCountAsync(1);

        await _app.HotkeyAsync(new KeyModifiers(Control: true), [Key.O]);
        var dialog = await _app.WaitForWindowAsync(new WindowMatchOptions { TitleContains = "Open" });
        try
        {
            await Expect(_app).ToHaveDialogAsync(new WindowMatchOptions { TitleContains = "Open" });
            await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(_app).ToHaveNoDialogAsync(timeoutMs: 300));
            await Expect(_app).ToHaveWindowCountAsync(2);
        }
        finally
        {
            // Escape goes through the dialog's own view, which focuses it first.
            await dialog.PressKeyAsync(Key.Escape);
        }
        await Expect(_app).ToHaveNoDialogAsync();
    }

    // Notepad is responsive and its window stops changing once idle.
    [Fact]
    public async Task ToBeRespondingAsync_And_ToBeVisuallyStableAsync_IdleNotepad_Pass()
    {
        await Expect(_app).ToBeRespondingAsync();
        await Expect(_app).ToBeRunningAsync();
        await Expect(_app).ToBeVisuallyStableAsync();
    }

    // A window that never settles (an impossible tolerance stands in for endless animation) fails with ExpectFailedError, not a raw TimeoutException.
    [Fact]
    public async Task ToBeVisuallyStableAsync_NeverSettles_FailsWithExpectFailedError()
    {
        var error = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(_app).ToBeVisuallyStableAsync(maxDifferenceRatio: -1, timeoutMs: 500));

        Assert.Contains("to be visually stable", error.Message);
    }

    // Loose resource bounds pass for a healthy Notepad, and an impossible one fails with the measured value.
    [Fact]
    public async Task ResourceAssertions_LooseBoundsPass_ImpossibleBoundsFail()
    {
        await Expect(_app).ToHaveMemoryBelowAsync(2L * 1024 * 1024 * 1024);
        await Expect(_app).ToHaveHandleCountBelowAsync(20_000);

        var error = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(_app).ToHaveHandleCountBelowAsync(1, timeoutMs: 300));
        Assert.Contains("it had", error.Message);
    }
}
