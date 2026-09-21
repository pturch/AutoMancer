// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Covers Locator.ByProperty(UiaProperty, ...) resolving a live app's built-in UIA property via the named enum.
// Custom properties (RegisterCustomPropertyAsync) aren't covered here — no fixture app in this repo registers one, since that needs raw IRawElementProviderSimple COM interop, not just AutomationPeer overrides.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class PropertyLocatorIntegrationTests
{
    // Notepad's own WinUI3 window has nothing with HelpText set — confirmed by walking its live tree, not assumed. Its native Open dialog (a classic Win32 common item dialog) does: HelpButton's "Get help." tooltip text, also confirmed live rather than guessed.
    [Fact]
    public async Task ByProperty_WithHelpTextEnum_FindsElementInNativeDialog()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            await app.FindAsync(Locator.ByControlType("Document"));
            await app.HotkeyAsync(new KeyModifiers(Control: true), [Key.O]);

            var dialog = await App.FindDialogAsync(app.ProcessId, "Open");
            Assert.NotNull(dialog);

            var helpButton = await dialog!.FindAsync(Locator.ByProperty(UiaProperty.HelpText, "Get help."));

            Assert.Equal("HelpButton", helpButton.AutomationId);
        }
        finally
        {
            await app.KillAsync();
        }
    }
}
