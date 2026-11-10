// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies locator suggestion for handles owned by UIA2 and Win32, so it doesn't work only for the primary provider.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class LocatorSuggestionProviderIntegrationTests(ContextMenuHostFixture host) : IClassFixture<ContextMenuHostFixture>
{
    // Suggests for every handle and returns the wrong answers (a suggestion that finds a different element) plus how many handles got one.
    private static async Task<(List<string> Wrong, int Suggested, int Total)> SweepAsync(App app, IElementIdentity owner, IReadOnlyList<ElementHandle> handles)
    {
        var wrong = new List<string>();
        var suggested = 0;
        foreach (var handle in handles)
        {
            var suggestion = await app.SuggestLocatorAsync(handle);
            if (suggestion is null)
                continue;
            suggested++;
            var found = await app.FindAsync(suggestion.Locator);
            if (!owner.IsSameElement(found, handle))
                wrong.Add($"{handle.ControlType} '{handle.Name}': {suggestion.Locator.Strategy}={suggestion.Locator.Value} resolved to a different element");
        }
        return (wrong, suggested, handles.Count);
    }

    // Every suggestion for a UIA2-resolved Notepad element finds that same element, and elements with an AutomationId unique in the tree always get one.
    [Fact]
    public async Task SuggestLocatorAsync_Uia2Handles_AreAccurate()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var uia2 = app.WithOptions(new AppOptions { ProviderChain = ["uia2"] });
            var tree = await uia2.SnapshotAsync() ?? [];
            var handles = new List<ElementHandle>();
            foreach (var type in tree.DescendantsAndSelf().Select(n => n.ControlType).Where(t => t is not null).Distinct())
                handles.AddRange(await uia2.FindAllAsync(Locator.ByControlType(type!)));

            var (wrong, suggested, total) = await SweepAsync(uia2, new Uia2Provider(), handles);

            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
            Assert.True(suggested > 0, $"None of {total} UIA2 handles got a suggestion.");
            var all = tree.DescendantsAndSelf().ToList();
            var uniqueIds = all.Where(n => !string.IsNullOrEmpty(n.AutomationId) && all.Count(m => m.AutomationId == n.AutomationId) == 1).Select(n => n.AutomationId).ToHashSet();
            foreach (var handle in handles.Where(h => h.AutomationId is not null && uniqueIds.Contains(h.AutomationId)))
                Assert.NotNull(await uia2.SuggestLocatorAsync(handle));
        }
        finally
        {
            await app.KillAsync();
            await app.DisposeAsync();
        }
    }

    // Every suggestion for a Win32-resolved WinForms window or control finds that same window, and the controls with a class name or title unique in the tree always get one.
    [Fact]
    public async Task SuggestLocatorAsync_Win32Handles_AreAccurate()
    {
        var win32 = host.App.WithOptions(new AppOptions { ProviderChain = ["win32"] });
        var tree = await win32.SnapshotAsync() ?? [];
        var handles = new List<ElementHandle>();
        foreach (var className in tree.DescendantsAndSelf().Select(n => n.ClassName).Where(c => !string.IsNullOrEmpty(c)).Distinct())
            handles.AddRange(await win32.FindAllAsync(Locator.ByClassName(className!)));

        var (wrong, suggested, total) = await SweepAsync(win32, new Win32Provider(), handles);

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        Assert.True(suggested > 0, $"None of {total} Win32 handles got a suggestion.");
    }

    // A handle whose window doesn't exist (as after it was destroyed) gets no suggestion rather than an error or a guess.
    [Fact]
    public async Task SuggestLocatorAsync_UnknownWin32Handle_ReturnsNull()
    {
        var win32 = host.App.WithOptions(new AppOptions { ProviderChain = ["win32"] });
        var gone = new ElementHandle("hwnd:1", "win32", (IntPtr)1);

        Assert.Null(await win32.SuggestLocatorAsync(gone));
    }

    // A token that is already cancelled stops the suggestion with OperationCanceledException instead of walking the tree.
    [Fact]
    public async Task SuggestLocatorAsync_CancelledToken_Throws()
    {
        var uia3 = host.App.WithOptions(new AppOptions { ProviderChain = ["uia3"] });
        var handle = (await uia3.FindAllAsync(Locator.ByControlType((await uia3.SnapshotAsync())![0].Children.First(c => c.ControlType is not null).ControlType!))).First();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uia3.SuggestLocatorAsync(handle, cts.Token));
    }

    // A handle from one provider is never treated as an element of another, so pinning the view to a different provider yields no suggestion.
    [Fact]
    public async Task SuggestLocatorAsync_HandleFromAnotherProvider_ReturnsNull()
    {
        var uia2 = host.App.WithOptions(new AppOptions { ProviderChain = ["uia2"] });
        var win32 = host.App.WithOptions(new AppOptions { ProviderChain = ["win32"] });
        var uia3 = host.App.WithOptions(new AppOptions { ProviderChain = ["uia3"] });
        var uia2Tree = await uia2.SnapshotAsync() ?? [];
        var uia2Handle = (await uia2.FindAllAsync(Locator.ByControlType(uia2Tree[0].Children.First(c => c.ControlType is not null).ControlType!))).First();
        var win32Tree = await win32.SnapshotAsync() ?? [];
        var win32Handle = (await win32.FindAllAsync(Locator.ByClassName(win32Tree.DescendantsAndSelf().Skip(1).First(n => !string.IsNullOrEmpty(n.ClassName)).ClassName!))).First();

        Assert.Null(await win32.SuggestLocatorAsync(uia2Handle));
        Assert.Null(await uia3.SuggestLocatorAsync(uia2Handle));
        Assert.Null(await uia2.SuggestLocatorAsync(win32Handle));
    }
}
