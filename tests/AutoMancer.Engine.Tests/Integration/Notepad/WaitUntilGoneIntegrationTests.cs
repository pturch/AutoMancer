// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies App.WaitUntilGoneAsync against Notepad's "Save changes?" prompt, a ContentDialog rendered inside the main window (not a separate top-level HWND) when closing a tab with unsaved edits.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class WaitUntilGoneIntegrationTests
{
    [Fact]
    public async Task WaitUntilGoneAsync_ResolvesAfterSaveDialogDismissed()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            // Open a second tab so closing it leaves the window (and process) alive regardless of any pre-existing tabs.
            await app.PressKeyAsync(Key.N, new KeyModifiers(Control: true));
            await app.ClickAsync(Locator.ByControlType("Document"));
            await app.TypeAsync(Locator.ByControlType("Document"), "AutoMancer wait-until-gone test");

            await app.PressKeyAsync(Key.W, new KeyModifiers(Control: true));

            var dontSave = Locator.ByAutomationId("SecondaryButton");
            var dialogButton = await app.FindAsync(dontSave);
            Assert.Equal("Don't save", dialogButton.Name);

            await app.ClickAsync(dontSave);

            await app.WaitUntilGoneAsync(dontSave);
        }
        finally
        {
            app.Kill();
            await Task.Delay(500);
        }
    }
}
