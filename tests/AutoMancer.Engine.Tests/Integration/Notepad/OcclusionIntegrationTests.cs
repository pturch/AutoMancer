// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Live coverage for App.IsUnobscuredAsync: Notepad's Settings button is clear by default, and covered while the Save-changes ContentDialog (and its full-window smoke layer) is up.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class OcclusionIntegrationTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    private static readonly Locator Settings = Locator.ByAutomationId("SettingsButton");
    private readonly App _app = fixture.App.WithOptions(new AppOptions { ProviderChain = ["uia3"] });

    // Nothing covers the button, so the hit-test at its center lands on it.
    [Fact]
    public async Task IsUnobscuredAsync_OnScreenButton_ReturnsTrue()
    {
        Assert.True(await _app.IsUnobscuredAsync(Settings));
    }

    // Regression: once UIA2 loads in-process, UIA3's hit-test stops at the XAML island's host window, which used to report every XAML element covered for the rest of the process.
    [Fact]
    public async Task IsUnobscuredAsync_AfterUia2Find_OnScreenButtonStillReturnsTrue()
    {
        await _app.WithOptions(new AppOptions { ProviderChain = ["uia2"] }).FindAsync(Settings);

        Assert.True(await _app.IsUnobscuredAsync(Settings));
    }

    // The dialog's own buttons stay clear while everything behind its smoke layer is covered, and the button clears again once it's dismissed.
    [Fact]
    public async Task IsUnobscuredAsync_ButtonUnderSaveChangesDialog_ReturnsFalseUntilDismissed()
    {
        // A second tab, so closing it leaves the shared window alive.
        await _app.PressKeyAsync(Key.N, new KeyModifiers(Control: true));
        await _app.TypeAsync(Locator.ByControlType("Document"), "occlusion test");
        await _app.PressKeyAsync(Key.W, new KeyModifiers(Control: true));
        var dontSave = Locator.ByAutomationId("SecondaryButton");
        await _app.FindAsync(dontSave);

        Assert.False(await _app.IsUnobscuredAsync(Settings));
        Assert.True(await _app.IsUnobscuredAsync(dontSave));

        await _app.ClickAsync(dontSave);
        await _app.WaitUntilGoneAsync(dontSave);
        Assert.True(await _app.IsUnobscuredAsync(Settings));
    }
}
