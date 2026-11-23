// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Tests.Integration;

// Live checks of the Stage 2.8 pattern and clipboard actions against Notepad.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class ExtendedPatternIntegrationTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    private readonly App _app = fixture.App.WithOptions(new AppOptions { ProviderChain = ["uia3"] });

    // Text written through SetClipboardTextAsync reads back unchanged, including non-ASCII characters.
    [Fact]
    public async Task Clipboard_SetThenGet_RoundTrips()
    {
        const string text = "AutoMancer clipboard — ünïcode ✓";

        await _app.SetClipboardTextAsync(text);

        Assert.Equal(text, await _app.GetClipboardTextAsync());
    }

    // The File menu button opens on Expand and closes on Collapse, observed through its menu items appearing and going away.
    [Fact]
    public async Task ExpandCollapse_FileMenu_OpensAndCloses()
    {
        await _app.ExpandAsync(Locator.ByName("File"));
        await _app.FindAsync(Locator.ByName("New tab"));

        await _app.CollapseAsync(Locator.ByName("File"));
        await _app.WaitUntilGoneAsync(Locator.ByName("New tab"));
    }

    // Toggling the View menu's Status bar item flips its toggle state, and toggling again restores it.
    [Fact]
    public async Task Toggle_StatusBarMenuItem_FlipsState()
    {
        await _app.ClickAsync(Locator.ByName("View"));
        var before = await ToggleStateAsync(Locator.ByName("Status bar"));
        try
        {
            await _app.ToggleAsync(Locator.ByName("Status bar"));
            await _app.ClickAsync(Locator.ByName("View"));
            Assert.NotEqual(before, await ToggleStateAsync(Locator.ByName("Status bar")));
        }
        finally
        {
            await _app.ToggleAsync(Locator.ByName("Status bar"));
        }
    }

    // Reads the element's current TogglePattern state straight from UIA, since the engine has no read surface for it yet.
    private async Task<int> ToggleStateAsync(Locator locator)
    {
        var handle = await _app.FindAsync(locator);
        var pattern = (IUIAutomationTogglePattern)((IUIAutomationElement)handle.NativeHandle).GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId);
        return (int)pattern.CurrentToggleState;
    }
}
