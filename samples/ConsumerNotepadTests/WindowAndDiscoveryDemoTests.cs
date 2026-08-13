// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Exercises App's window management, discovery, and session-attachment methods against a live Notepad instance.
[Collection("ConsumerNotepad")]
public sealed class WindowAndDiscoveryDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture), IClassFixture<NotepadFixture>
{
    // GetWindowSizeAsync, MoveWindowAsync, and ResizeWindowAsync round-trip the window back to where they found it.
    [Fact]
    public async Task MoveWindowAsync_And_ResizeWindowAsync_RoundTripWindowBounds()
    {
        var original = await App.GetWindowSizeAsync();

        await App.MoveWindowAsync((int)original.X + 20, (int)original.Y + 20);
        await App.ResizeWindowAsync((int)original.Width + 30, (int)original.Height + 30);

        var moved = await App.GetWindowSizeAsync();
        Assert.NotEqual(original, moved);

        await App.MoveWindowAsync((int)original.X, (int)original.Y);
        await App.ResizeWindowAsync((int)original.Width, (int)original.Height);
    }

    // SetWindowStateAsync maximizes then restores — leaves the window Normal for any later test in this class.
    [Fact]
    public async Task SetWindowStateAsync_MaximizesThenRestores()
    {
        await App.SetWindowStateAsync(WindowState.Maximized);
        await App.SetWindowStateAsync(WindowState.Normal);
    }

    // FindAllAsync returns every Button in the tree — menu buttons plus title bar controls — proving it doesn't stop at the first match like FindAsync.
    [Fact]
    public async Task FindAllAsync_ReturnsMultipleButtons()
    {
        var buttons = await App.FindAllAsync(Locator.ByControlType("Button"));
        Assert.True(buttons.Count > 1);
    }

    // SnapshotAsync walks the whole element tree in one call — used here to confirm the Document control is reachable from the root.
    [Fact]
    public async Task SnapshotAsync_TreeContainsDocumentControl()
    {
        var snapshot = await App.SnapshotAsync();
        Assert.NotNull(snapshot);
        Assert.Contains(Flatten(snapshot!), node => node.ControlType == "Document");
    }

    // ScreenshotAsync captures the window's current on-screen pixels as a PNG byte array.
    [Fact]
    public async Task ScreenshotAsync_ReturnsNonEmptyPng()
    {
        var png = await App.ScreenshotAsync();
        Assert.NotEmpty(png);
    }

    // WaitForAsync polls a raw synchronous predicate over ElementHandle (Name/BoundingRect/etc.) — for live element *value*, GetValueAsync/ToHaveValueAsync poll instead, since reading a value is an async operator call.
    [Fact]
    public async Task WaitForAsync_PollsUntilConditionHolds()
    {
        var document = Locator.ByControlType("Document");
        await App.WaitForAsync(document, e => e.BoundingRect is { Width: > 0, Height: > 0 });
    }

    // WaitUntilGoneAsync resolves immediately when every provider already returns nothing for the locator.
    [Fact]
    public async Task WaitUntilGoneAsync_ResolvesImmediatelyForAbsentLocator()
        => await App.WaitUntilGoneAsync(Locator.ByAutomationId("ThisElementDoesNotExistInNotepad"));

    // AttachByPidAsync wraps the same running process by PID — a fresh App façade over the same window, proving RuntimeId-backed handles stay reachable across sessions.
    [Fact]
    public async Task AttachByPidAsync_ReattachesToSameProcess()
    {
        var reattached = await AutoMancer.Engine.App.AttachByPidAsync(App.ProcessId);
        var document = await reattached.FindAsync(Locator.ByControlType("Document"));

        Assert.NotNull(document);
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
