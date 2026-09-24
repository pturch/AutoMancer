// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Core;

// Proves scoped find restricts results to the given element's descendants, using two identically-matched "Cancel" buttons in different places.
// Uses a small fake provider with a real parent/child structure rather than a Moq, since this needs genuine tree-filtering behavior.
public sealed class ScopedFindTests
{
    private static readonly AppSession Session = AppSession.CreateForTesting(ExitedProcess(), (IntPtr)42);

    private static Process ExitedProcess()
    {
        var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        process.WaitForExit(2000);
        return process;
    }

    // A fake provider with an in-memory parent/child structure, so scope-restriction semantics can be tested without touching real COM.
    private sealed class FakeTreeProvider : IElementProvider
    {
        public string ProviderName => "fake";
        private readonly Dictionary<string, string?> _parentById = new();
        private readonly List<ElementHandle> _elements = [];

        public void Add(ElementHandle element, string? parentId)
        {
            _elements.Add(element);
            _parentById[element.Id] = parentId;
        }

        // Removes an element and its tracked parent link — used to simulate a dialog closing (and a replacement one opening as a genuinely different element), not just mutating one in place.
        public void Remove(ElementHandle element)
        {
            _elements.RemoveAll(e => e.Id == element.Id);
            _parentById.Remove(element.Id);
        }

        private bool IsDescendantOf(string id, string ancestorId)
        {
            var current = _parentById.GetValueOrDefault(id);
            while (current is not null)
            {
                if (current == ancestorId) return true;
                current = _parentById.GetValueOrDefault(current);
            }
            return false;
        }

        private IEnumerable<ElementHandle> Matching(Locator locator, ElementHandle? scope)
        {
            var matches = _elements.Where(e => e.Name == locator.Value);
            return scope is null ? matches : matches.Where(e => IsDescendantOf(e.Id, scope.Id));
        }

        public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
            Task.FromResult(Matching(locator, null).FirstOrDefault());

        public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementHandle>)Matching(locator, null).ToList());

        public Task<ElementHandle?> FindScopedElementAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
            Task.FromResult(Matching(locator, scope).FirstOrDefault());

        public Task<IReadOnlyList<ElementHandle>> FindScopedElementsAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementHandle>)Matching(locator, scope).ToList());

        public Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementSnapshot>)Array.Empty<ElementSnapshot>());
    }

    private static (FakeTreeProvider Provider, ElementHandle Dialog1, ElementHandle Dialog2, ElementHandle Cancel1, ElementHandle Cancel2) BuildTwoDialogsScenario()
    {
        var provider = new FakeTreeProvider();
        var dialog1 = new ElementHandle("dialog1", "fake", new object()) { Name = "Dialog" };
        var dialog2 = new ElementHandle("dialog2", "fake", new object()) { Name = "Dialog" };
        var cancel1 = new ElementHandle("cancel1", "fake", new object()) { Name = "Cancel" };
        var cancel2 = new ElementHandle("cancel2", "fake", new object()) { Name = "Cancel" };
        provider.Add(dialog1, null);
        provider.Add(dialog2, null);
        provider.Add(cancel1, "dialog1");
        provider.Add(cancel2, "dialog2");
        return (provider, dialog1, dialog2, cancel1, cancel2);
    }

    [Fact]
    public async Task FindAsync_WithScope_OnlyMatchesTheScopedDialogsCancelButton()
    {
        var (provider, dialog1, _, cancel1, _) = BuildTwoDialogsScenario();
        var resolver = new ElementResolver([provider], new ElementProviderOptions { ProviderChain = ["fake"] });

        var result = await resolver.FindScopedAsync(Locator.ByName("Cancel"), Session, dialog1);

        Assert.Same(cancel1, result);
    }

    [Fact]
    public async Task FindAllAsync_WithoutScope_MatchesBothDialogsCancelButtons_WithScope_MatchesOnlyOne()
    {
        var (provider, dialog1, _, cancel1, _) = BuildTwoDialogsScenario();
        var resolver = new ElementResolver([provider], new ElementProviderOptions { ProviderChain = ["fake"] });

        var unscoped = await resolver.FindAllAsync(Locator.ByName("Cancel"), Session);
        var scoped = await resolver.FindAllScopedAsync(Locator.ByName("Cancel"), Session, dialog1);

        Assert.Equal(2, unscoped.Count);
        Assert.Same(cancel1, Assert.Single(scoped));
    }

    [Fact]
    public async Task FindAsync_ScopedToTheOtherDialog_DoesNotFindTheFirstDialogsCancelButton()
    {
        var (provider, _, dialog2, cancel1, cancel2) = BuildTwoDialogsScenario();
        var resolver = new ElementResolver([provider], new ElementProviderOptions { ProviderChain = ["fake"], ImplicitWaitMs = 0 });

        var result = await resolver.FindAllScopedAsync(Locator.ByName("Cancel"), Session, dialog2);

        var found = Assert.Single(result);
        Assert.Same(cancel2, found);
        Assert.NotSame(cancel1, found);
    }

    // A held ElementHandle scope goes stale if the element is replaced; the Locator overload re-resolves scope fresh each call.
    // This is the actual point of the Locator-scope overload: it survives a dialog closing and reopening as a new COM object.
    [Fact]
    public async Task FindAsync_LocatorScope_ReResolvesFreshEachCall_UnlikeAHeldElementHandle()
    {
        var provider = new FakeTreeProvider();
        var dialogV1 = new ElementHandle("dialog-v1", "fake", new object()) { Name = "Dialog" };
        var cancelV1 = new ElementHandle("cancel-v1", "fake", new object()) { Name = "Cancel" };
        provider.Add(dialogV1, null);
        provider.Add(cancelV1, "dialog-v1");
        var resolver = new ElementResolver([provider], new ElementProviderOptions { ProviderChain = ["fake"], ImplicitWaitMs = 0 });

        // Caller resolves scope once and holds onto the handle, simulating time passing before it's used.
        var heldScope = await resolver.FindAsync(Locator.ByName("Dialog"), Session);
        Assert.Equal("dialog-v1", heldScope.Id);

        // The dialog closes and a brand-new instance opens in its place — a different underlying element, not a mutation of the old one.
        provider.Remove(dialogV1);
        provider.Remove(cancelV1);
        var dialogV2 = new ElementHandle("dialog-v2", "fake", new object()) { Name = "Dialog" };
        var cancelV2 = new ElementHandle("cancel-v2", "fake", new object()) { Name = "Cancel" };
        provider.Add(dialogV2, null);
        provider.Add(cancelV2, "dialog-v2");

        // The held handle now points at a dead element — correctly reports "not found" rather than a wrong match, but can't recover.
        var viaHeldHandle = await resolver.FindAllScopedAsync(Locator.ByName("Cancel"), Session, heldScope);
        Assert.Empty(viaHeldHandle);

        // The Locator-scope overload re-resolves "Dialog" fresh as part of this call, so it finds the new dialog's Cancel button instead of failing.
        var viaFreshScope = await resolver.FindScopedAsync(Locator.ByName("Cancel"), Session, Locator.ByName("Dialog"));
        Assert.Equal("cancel-v2", viaFreshScope.Id);
    }
}
