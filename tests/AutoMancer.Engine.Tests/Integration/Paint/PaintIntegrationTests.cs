// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Shared fixture: launches Paint once for all tests in this class, kills it on teardown.
public sealed class PaintFixture : IAsyncLifetime
{
    public App App { get; private set; } = null!;

    // Launches Paint and waits for the toolbar to be fully populated via UIA3, then dismisses the welcome popup.
    public async Task InitializeAsync()
    {
        App = await App.LaunchAsync("mspaint.exe");
        var warmup = App.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 10_000 });
        await warmup.FindAsync(Locator.ByAutomationId("PencilTool"));

        // The welcome popup's Close button is targeted via XPath to avoid matching the title-bar Close button, which shares AutomationId="Close".
        var quick = App.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 2_000 });
        try { await quick.ClickAsync(Locator.ByXPath("//Window[@Name='Popup']//Button[@Name='Close']")); }
        catch { /* popup absent — already dismissed on this Windows install */ }

        await Task.Delay(300);
    }

    // Kills Paint without triggering the save dialog (Kill bypasses it).
    public async Task DisposeAsync()
    {
        await App.KillAsync();
    }
}

// Comprehensive end-to-end workflow tests against Windows 11 Paint (mspaint.exe).
[Collection("Paint")]
[Trait("Category", "Integration")]
public sealed class PaintIntegrationTests(PaintFixture fixture) : IClassFixture<PaintFixture>
{
    private App App => fixture.App;
    private App Uia3 => App.WithOptions(new AppOptions { ProviderChain = ["uia3"] });

    // ── Element finding ─────────────────────────────────────────────────────────

    // Verifies the Pencil tool button is found by its AutomationId.
    [Fact]
    public async Task FindPencilTool_ByAutomationId_Succeeds()
    {
        var el = await Uia3.FindAsync(Locator.ByAutomationId("PencilTool"));

        Assert.Equal("Button", el.ControlType);
        Assert.Equal("Pencil", el.Name);
    }

    // Verifies the File menu item is found by name.
    [Fact]
    public async Task FindFileMenuItem_ByName_Succeeds()
    {
        var el = await Uia3.FindAsync(Locator.ByName("File"));

        Assert.Equal("MenuItem", el.ControlType);
    }

    // Verifies the canvas size status text is found by its AutomationId.
    [Fact]
    public async Task FindCanvasSizeText_ByAutomationId_Succeeds()
    {
        var el = await Uia3.FindAsync(Locator.ByAutomationId("CanvasSizeTextBlock"));

        Assert.Equal("Text", el.ControlType);
        Assert.NotEmpty(el.Name!);
    }

    // Verifies that FindAll returns multiple Button elements from the toolbar.
    [Fact]
    public async Task FindAllButtons_ReturnsMultiple()
    {
        var buttons = await Uia3.FindAllAsync(Locator.ByControlType("Button"));

        Assert.True(buttons.Count > 5, $"Expected more than 5 buttons, got {buttons.Count}.");
    }

    // ── Interactions ─────────────────────────────────────────────────────────────

    // Verifies that clicking the Pencil tool does not throw.
    [Fact]
    public async Task ClickPencilTool_Succeeds()
        => await App.ClickAsync(Locator.ByAutomationId("PencilTool"));

    // Save opens a Save As dialog on its own top-level window; dismissed via Escape sent to that dialog (via FindDialogAsync) since it would otherwise block later window-management tests.
    [Fact]
    public async Task ClickSaveButton_Succeeds()
    {
        await App.ClickAsync(Locator.ByName("Save"));

        var dialog = await AutoMancer.Engine.App.FindDialogAsync(App.ProcessId, "Save", timeoutMs: 2_000);
        if (dialog is not null)
            await dialog.PressKeyAsync(Key.Escape);
    }

    // ── Advanced locators ────────────────────────────────────────────────────────

    // Verifies that a previously found element can be re-found using its RuntimeId.
    [Fact]
    public async Task FindByRuntimeId_RefindsElement()
    {
        var pencil = await Uia3.FindAsync(Locator.ByAutomationId("PencilTool"));
        var refound = await Uia3.FindAsync(Locator.ByRuntimeId(pencil.Id));

        Assert.Equal(pencil.AutomationId, refound.AutomationId);
        Assert.Equal("Pencil", refound.Name);
    }

    // Verifies that XPath can find the Pencil tool by its AutomationId attribute.
    [Fact]
    public async Task XPathLocator_FindsPencilTool()
    {
        var el = await Uia3.FindAsync(Locator.ByXPath("//Button[@AutomationId='PencilTool']"));

        Assert.Equal("Pencil", el.Name);
        Assert.Equal("Button", el.ControlType);
    }

    // Verifies that XPath can navigate a parent–child path to find the File menu item.
    [Fact]
    public async Task XPathLocator_FindsFileMenuViaPath()
    {
        var el = await Uia3.FindAsync(Locator.ByXPath("//MenuBar//MenuItem[@Name='File']"));

        Assert.Equal("File", el.Name);
    }
}
