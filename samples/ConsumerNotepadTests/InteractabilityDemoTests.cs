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
            await Task.Delay(200);
        }
    }
}
