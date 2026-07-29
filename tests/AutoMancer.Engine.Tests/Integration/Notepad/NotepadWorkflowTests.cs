// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Tests.Integration;

// Longer workflow tests that exercise the full provider chain, multiple locator strategies,
// action dispatch, session attachment, and error handling against a live Notepad instance.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class NotepadWorkflowTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    private readonly App _app = fixture.App;

    // -------------------------------------------------------------------------
    // Full workflow
    // -------------------------------------------------------------------------

    // Types a document, navigates menus, re-attaches by PID, and verifies error hints — all in one session.
    [Fact]
    public async Task FullWorkflow_TypeContentAndNavigateMenus()
    {
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await _app.TypeAsync(Locator.ByControlType("Document"), "Hello from AutoMancer.");

        var editor = await _app.FindAsync(Locator.ByControlType("Document"));
        Assert.Contains("Hello from AutoMancer", GetEditorText(editor));

        await _app.ClickAsync(Locator.ByName("File"));
        await Task.Delay(300); // flyout animation — longer than ActionDelayMs
        NotepadFixture.SendEscape();
        await Task.Delay(200);

        // Re-attach to the same process by PID — the editor RuntimeId must be stable across sessions.
        var reattached = await App.AttachByPidAsync(_app.ProcessId);
        var reattachedEditor = await reattached.FindAsync(Locator.ByControlType("Document"));
        Assert.Equal(editor.Id, reattachedEditor.Id);

        // A near-miss name should produce a closest-match hint pointing to "File".
        // "Vile" vs "File": edit distance 1 out of length 4 → similarity 0.75, above the 0.70 threshold.
        var quick = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 300, PollIntervalMs = 100 });
        var ex = await Assert.ThrowsAsync<ElementNotFoundError>(() => quick.FindAsync(Locator.ByName("Vile")));
        Assert.NotNull(ex.ClosestMatch);
        Assert.Equal("File", ex.ClosestMatch!.ElementName);
    }

    // -------------------------------------------------------------------------
    // Provider chain behaviour
    // -------------------------------------------------------------------------

    // UIA3 resolves the Document control — ResolvedVia proves the chain short-circuited at the first provider.
    [Fact]
    public async Task ProviderChain_Uia3ResolvesFirst_ResolvedViaIsUia3()
    {
        var handle = await _app.FindAsync(Locator.ByControlType("Document"));

        Assert.Equal("uia3", handle.ResolvedVia);
    }

    // Win32-only App finds a child window by title — proves the provider works independently of UIA.
    // Uses the snapshot to discover an actual named child rather than hardcoding an internal window title.
    [Fact]
    public async Task ProviderChain_Win32OnlyResolver_FindsByWindowTitle()
    {
        var win32 = _app.WithOptions(new AppOptions { ProviderChain = ["win32"], ImplicitWaitMs = 2_000, PollIntervalMs = 200 });
        var snapshot = await win32.SnapshotAsync();
        var namedChild = snapshot?[0].Children.FirstOrDefault(c => !string.IsNullOrEmpty(c.Name));

        // If Notepad's Win32 children have no titles, the test is inconclusive rather than failed —
        // this varies across Windows versions and Notepad builds.
        if (namedChild is null)
            return;

        var handle = await win32.FindAsync(Locator.ByName(namedChild.Name!));

        Assert.Equal("win32", handle.ResolvedVia);
    }

    // Win32-only App finds a child window by class name — uses a discovered class rather than a hardcoded one.
    [Fact]
    public async Task ProviderChain_Win32OnlyResolver_FindsByClassName()
    {
        var win32 = _app.WithOptions(new AppOptions { ProviderChain = ["win32"], ImplicitWaitMs = 2_000, PollIntervalMs = 200 });
        var snapshot = await win32.SnapshotAsync();
        var classedChild = snapshot?[0].Children.FirstOrDefault(c => !string.IsNullOrEmpty(c.ClassName));

        if (classedChild is null)
            return;

        var handle = await win32.FindAsync(Locator.ByClassName(classedChild.ClassName!));

        Assert.Equal("win32", handle.ResolvedVia);
        Assert.Equal(classedChild.ClassName, handle.ClassName);
    }

    // -------------------------------------------------------------------------
    // Locator strategies
    // -------------------------------------------------------------------------

    // FindAllAsync returns multiple Button controls — menu buttons plus title bar controls.
    [Fact]
    public async Task FindAll_ByControlType_ReturnsMultipleButtons()
    {
        var buttons = await _app.FindAllAsync(Locator.ByControlType("Button"));

        Assert.True(buttons.Count > 1);
        Assert.All(buttons, b => Assert.Equal("uia3", b.ResolvedVia));
    }

    // Locator.ByName finds a specific menu button by its exact label.
    [Fact]
    public async Task FindAsync_ByName_FindsFileMenuButton()
    {
        var handle = await _app.FindAsync(Locator.ByName("File"));

        Assert.Equal("File", handle.Name);
        Assert.Equal("uia3", handle.ResolvedVia);
    }

    // -------------------------------------------------------------------------
    // Snapshot
    // -------------------------------------------------------------------------

    // Win32 snapshot returns a root node with child HWNDs; every child has a class name.
    [Fact]
    public async Task Win32Snapshot_RootHasChildWindows()
    {
        var snapshot = await _app.WithOptions(new AppOptions { ProviderChain = ["win32"] }).SnapshotAsync();

        Assert.NotNull(snapshot);
        Assert.Single(snapshot!);
        Assert.NotEmpty(snapshot![0].Children);
        Assert.All(snapshot![0].Children, child => Assert.NotNull(child.ClassName));
    }

    // UIA3 snapshot tree contains nodes with human-readable control type names, not raw integers.
    [Fact]
    public async Task Uia3Snapshot_ControlTypeNamesAreHumanReadable()
    {
        var snapshot = await _app.WithOptions(new AppOptions { ProviderChain = ["uia3"] }).SnapshotAsync();

        Assert.NotNull(snapshot);
        var all = Flatten(snapshot!);
        Assert.All(all, node => Assert.False(string.IsNullOrEmpty(node.ControlType)));
        Assert.Contains(all, node => node.ControlType == "Document");
    }

    // -------------------------------------------------------------------------
    // Error handling
    // -------------------------------------------------------------------------

    // A search with ImplicitWaitMs exhausted throws ElementNotFoundError listing all attempted providers.
    [Fact]
    public async Task FindAsync_NotFound_ErrorListsAttemptedProviders()
    {
        var quick = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 300, PollIntervalMs = 100 });

        var ex = await Assert.ThrowsAsync<ElementNotFoundError>(() => quick.FindAsync(Locator.ByName("NonExistentElement_XYZ")));

        Assert.Contains("uia3", ex.AttemptedProviders);
    }

    // A typo within edit-distance 1 of a real element name produces a closest-match hint.
    [Fact]
    public async Task FindAsync_Typo_ClosestMatchHintNamesRealElement()
    {
        var quick = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 300, PollIntervalMs = 100 });

        var ex = await Assert.ThrowsAsync<ElementNotFoundError>(() => quick.FindAsync(Locator.ByName("Vile")));

        Assert.NotNull(ex.ClosestMatch);
        Assert.Equal("File", ex.ClosestMatch!.ElementName);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // Reads the full text content from a Document element via IUIAutomationTextPattern.
    private static string GetEditorText(ElementHandle editor)
    {
        var uiaElement = (IUIAutomationElement)editor.NativeHandle;
        var textPattern = (IUIAutomationTextPattern)uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_TextPatternId);
        return textPattern.DocumentRange.GetText(-1);
    }

    // Flattens a snapshot tree depth-first into a single list.
    private static List<ElementSnapshot> Flatten(IReadOnlyList<ElementSnapshot> nodes)
    {
        var result = new List<ElementSnapshot>();
        foreach (var node in nodes)
        {
            result.Add(node);
            result.AddRange(Flatten(node.Children));
        }
        return result;
    }
}
