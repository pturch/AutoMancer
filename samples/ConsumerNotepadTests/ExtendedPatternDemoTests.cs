// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Demonstrates the UIA-pattern actions (expand/collapse, toggle) and clipboard access against live Notepad.
[Collection("ConsumerNotepad")]
public sealed class ExtendedPatternDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    private static readonly Locator FileMenu = Locator.ByName("File");
    private static readonly Locator NewTabItem = Locator.ByName("New tab");

    // SetClipboardTextAsync writes the system clipboard and GetClipboardTextAsync reads it back, Unicode included.
    [Fact]
    public async Task SetClipboardTextAsync_Then_GetClipboardTextAsync_RoundTrips()
    {
        await App.SetClipboardTextAsync("Hello from ExtendedPatternDemoTests — ünïcode ✓");

        Assert.Equal("Hello from ExtendedPatternDemoTests — ünïcode ✓", await App.GetClipboardTextAsync());
    }

    // ExpandAsync opens the File menu without a click and CollapseAsync closes it; the "New tab" item's presence is the observable state.
    [Fact]
    public async Task ExpandAsync_Then_CollapseAsync_OpensAndClosesFileMenu()
    {
        await App.ExpandAsync(FileMenu);

        await Expect(NewTabItem).ToBeVisibleAsync();

        await App.CollapseAsync(FileMenu);
        await App.WaitUntilGoneAsync(NewTabItem);
    }

    // ToggleAsync flips View > Status bar; toggling twice restores it. There is no toggle-state assertion yet, so this only proves the pattern call completes.
    [Fact]
    public async Task ToggleAsync_StatusBarMenuItem_CompletesAndRestores()
    {
        var statusBar = Locator.ByName("Status bar");
        await App.ClickAsync(Locator.ByName("View"));
        await App.ToggleAsync(statusBar);
        await App.ClickAsync(Locator.ByName("View"));
        await App.ToggleAsync(statusBar);
    }
}
