// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Demonstrates the interactability signals a resolved ElementHandle carries, and the typed errors App throws instead of silently sending input nowhere.
[Collection("ConsumerNotepad")]
public sealed class InteractabilityDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    // IsEnabled/IsOffscreen are populated on every resolved element (null only when a provider can't determine them, e.g. Win32); a normal, visible, enabled control reads true/not-true respectively.
    [Fact]
    public async Task FindAsync_DocumentControl_IsEnabledAndOnscreen()
    {
        var document = await App.FindAsync(Locator.ByControlType("Document"));

        Assert.True(document.IsEnabled);
        Assert.NotEqual(true, document.IsOffscreen);
    }

    // A minimized target window throws WindowMinimizedError instead of silently sending input nowhere; the window is always restored afterward so later tests in this class aren't affected.
    [Fact]
    public async Task ClickAsync_OnMinimizedWindow_ThrowsWindowMinimizedError()
    {
        await App.SetWindowStateAsync(WindowState.Minimized);
        try
        {
            await Assert.ThrowsAsync<WindowMinimizedError>(() => App.ClickAsync(Locator.ByControlType("Document")));
        }
        finally
        {
            await App.SetWindowStateAsync(WindowState.Normal);
            // A minimized window's elements read offscreen, so this waits out the restore before the next test acts.
            await App.WaitForAsync(Locator.ByControlType("Document"), WaitConditions.IsVisible());
        }
    }

    // Every action re-checks enabled/visible live, so a handle held after its menu closed is rejected rather than invoking a command no user could reach; App.Force is the per-call opt-out.
    [Fact]
    public async Task HeldMenuItem_AfterMenuCloses_RejectedByDefault_InvokedThroughForce()
    {
        var tabs = Locator.ByControlType("TabItem");
        var tabsBefore = (await App.FindAllAsync(tabs)).Count;
        // Both handles are found while the menu is open, since no locator can reach the item once it's closed.
        await App.ClickAsync(Locator.ByName("File"));
        var held = await App.FindAsync(Locator.ByName("New tab"));
        var forced = await App.Force.FindAsync(Locator.ByName("New tab"));
        await App.PressKeyAsync(Key.Escape);
        // A closed menu's items drop out of search, so waiting for one to go waits for the menu to close.
        await App.WaitUntilGoneAsync(Locator.ByName("New tab"));

        try
        {
            await Assert.ThrowsAsync<ElementNotInteractableError>(() => ClickAction.ExecuteAsync(held));

            await ClickAction.ExecuteAsync(forced);
            await Expect(tabs).ToHaveCountAsync(tabsBefore + 1);
        }
        finally
        {
            // Close the empty tab the forced click opened; an empty tab closes without a save prompt.
            if ((await App.FindAllAsync(tabs)).Count > tabsBefore)
                await App.PressKeyAsync(Key.W, new KeyModifiers(Control: true));
        }
    }

    // TypeAsync lands in its target even when keyboard focus is elsewhere: when it has to fall back to keystrokes, it focuses the target first and waits until focus lands.
    [Fact]
    public async Task TypeAsync_FocusElsewhere_LandsInTargetElement()
    {
        var document = Locator.ByControlType("Document");
        await App.ClearAsync(document);
        await App.SetFocusAsync(Locator.ByName("File"));

        await App.TypeAsync(document, "focus demo");

        await Expect(document).ToHaveValueAsync("focus demo");
        await App.ClearAsync(document);
    }
}
