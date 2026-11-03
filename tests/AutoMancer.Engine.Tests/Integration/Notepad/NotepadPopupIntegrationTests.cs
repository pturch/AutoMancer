// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// WinUI3 popups in Notepad other than its context menu.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class NotepadPopupIntegrationTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    // A toolbar button's tooltip is a WinUI3 popup window (PopupWindowSiteBridge): found with or without expect, its text read, and closed once the pointer moves away.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SettingsTooltip_TextIsReadThenClosesWhenPointerLeaves(bool withExpect)
    {
        var tooltip = await fixture.App.OpenPopupAsync(Locator.ByName("Settings"), PopupTrigger.Hover, expect: withExpect ? Locator.ByControlType("ToolTip") : null);
        var snapshot = await tooltip.SnapshotAsync();

        Assert.Contains(snapshot[0].DescendantsAndSelf(), element => element.ControlType == "ToolTip" && element.Name == "Settings");
        await fixture.App.HoverAsync(Locator.ByControlType("Document"));
        await tooltip.WaitUntilClosedAsync();
    }
}
