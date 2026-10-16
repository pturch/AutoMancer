// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Demonstrates what happens when a held ElementHandle outlives its element: a typed StaleElementError instead of input sent to wherever it used to be.
[Collection("ConsumerNotepad")]
public sealed class StaleHandleDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    private static readonly Locator DontSave = Locator.ByAutomationId("SecondaryButton");

    // A handle held across the Save-changes dialog closing raises StaleElementError, carrying the handle; re-finding by locator is the recovery. App's locator-based methods do that re-find for you (AppOptions.ReresolveOnStale, on by default).
    [Fact]
    public async Task HeldHandle_AfterDialogCloses_ThrowsStaleElementError()
    {
        // A second tab with unsaved text, so Ctrl+W raises the dialog and leaves the shared window open.
        await App.PressKeyAsync(Key.N, new KeyModifiers(Control: true));
        await App.TypeAsync(Locator.ByControlType("Document"), "stale demo");
        await App.PressKeyAsync(Key.W, new KeyModifiers(Control: true));

        var held = await App.FindAsync(DontSave);
        await App.ClickAsync(DontSave);
        // Gone from search isn't gone yet: the button leaves search results partway through the close animation, a moment before it detaches, and until then the held handle still acts.
        await App.WaitUntilGoneAsync(DontSave);
        await WaitUntilStaleAsync(held);

        var error = await Assert.ThrowsAsync<StaleElementError>(() => ClickAction.ExecuteAsync(held));
        Assert.Same(held, error.Element);
    }

    // Waits for a held handle's element to really be gone, the handle counterpart of WaitUntilGoneAsync: a hover is harmless, and like every action it raises StaleElementError once the element is gone.
    private async Task WaitUntilStaleAsync(ElementHandle handle)
        => await Expect(async () =>
        {
            try { await HoverAction.ExecuteAsync(handle); return false; }
            catch (ElementNotInteractableError) { return false; } // still there mid-animation, just not usable
            catch (StaleElementError) { return true; }
        }).ToSatisfyAsync(stale => stale, "the held handle to report its element stale");
}
