// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Exercises App's element-targeted action methods against a live Notepad Document control.
[Collection("ConsumerNotepad")]
public sealed class ActionsDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    private static readonly Locator Document = Locator.ByControlType("Document");
    private static readonly Locator FileMenu = Locator.ByName("File");

    // ClickAsync focuses the document, then TypeAsync and ClearAsync round-trip its content — read via GetValueAsync/ToHaveValueAsync since Document's accessibility Name is a fixed label, not live text.
    [Fact]
    public async Task TypeAsync_Then_ClearAsync_RoundTripsDocumentText()
    {
        await App.ClickAsync(Document);
        await App.ClearAsync(Document);
        await App.TypeAsync(Document, "Hello from ActionsDemoTests.");

        await Expect(Document).ToHaveValueAsync("Hello from ActionsDemoTests.");

        await App.ClearAsync(Document);

        await Expect(Document).ToHaveValueAsync("");
    }

    // SetFocusAsync moves keyboard focus to File without clicking; a follow-up ClickAsync back on Document proves the app is still responsive.
    [Fact]
    public async Task SetFocusAsync_MovesFocusWithoutClicking()
    {
        await App.ClickAsync(Document);
        await App.SetFocusAsync(FileMenu);
        await App.ClickAsync(Document);

        var document = await App.FindAsync(Document);
        Expect(document).ToBeVisible();
    }

    // HoverAsync moves the cursor over File without pressing a button — no visible Notepad state to assert, so this just proves the call completes against a live window.
    [Fact]
    public async Task HoverAsync_CompletesWithoutThrowing()
        => await App.HoverAsync(FileMenu);

    // DoubleClickAsync on the whole Document is a coarse smoke test — selecting a specific word needs a synthetic ElementHandle over a TextPattern range, which is an internal-test technique, not part of the public API this sample demonstrates.
    [Fact]
    public async Task DoubleClickAsync_CompletesWithoutThrowing()
    {
        await App.ClickAsync(Document);
        await App.ClearAsync(Document);
        await App.TypeAsync(Document, "DoubleClickTarget");
        await App.DoubleClickAsync(Document);
    }

    // RightClickAsync opens Notepad's context menu; PressKeyAsync(Escape) closes it again via the public API instead of a raw SendInput helper.
    [Fact]
    public async Task RightClickAsync_OpensContextMenu()
    {
        await App.ClickAsync(Document);
        await App.RightClickAsync(Document);
        await App.PressKeyAsync(Key.Escape);
    }

    // ClickAtAsync(Locator) clicks a resolved element's screen center — the WinUI3-safe path for controls where InvokePattern skips the pointer pipeline.
    [Fact]
    public async Task ClickAtAsync_Locator_ClicksElementCenter()
        => await App.ClickAtAsync(Document);

    // ClickAtAsync(x, y) clicks a raw physical screen coordinate with no element lookup — the path for targets with no accessible element at all, e.g. a paint tool's fill bucket.
    [Fact]
    public async Task ClickAtAsync_Coordinates_ClicksRawPoint()
    {
        var window = await App.FindAsync(Locator.ByControlType("Window"));
        var (x, y) = window.BoundingRect.Center;

        await App.ClickAtAsync((int)x, (int)y);
    }

    // TypeDirectAsync sends keystrokes to whatever currently has focus, bypassing locator resolution entirely.
    [Fact]
    public async Task TypeDirectAsync_TypesIntoFocusedElement()
    {
        await App.ClickAsync(Document);
        await App.ClearAsync(Document);

        await App.TypeDirectAsync("Direct input");

        await Expect(Document).ToHaveValueAsync("Direct input");
    }
}
