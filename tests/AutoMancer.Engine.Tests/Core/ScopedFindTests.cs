// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

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

        // Searches from the session's root as the real providers do: a popup view's RootElement once it's checked still showing, else the whole tree.
        public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default)
        {
            session.ThrowIfRootClosed();
            return Task.FromResult(Matching(locator, session.RootElement).FirstOrDefault());
        }

        public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default)
        {
            session.ThrowIfRootClosed();
            return Task.FromResult((IReadOnlyList<ElementHandle>)Matching(locator, session.RootElement).ToList());
        }

        public Task<ElementHandle?> FindScopedElementAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
            Task.FromResult(Matching(locator, scope).FirstOrDefault());

        public Task<IReadOnlyList<ElementHandle>> FindScopedElementsAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementHandle>)Matching(locator, scope).ToList());

        public Task<ElementHandle?> FindParentAsync(ElementHandle element, AppSession session, CancellationToken ct = default) =>
            Task.FromResult<ElementHandle?>(null);

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

    // ---- Popup: an App view whose session is rooted at the popup's surface ----

    // A main-window "Cancel" listed first and a popup surface holding a second one, so a search that leaked out of the popup would return the wrong one; showing false leaves the surface offscreen, like a closed pre-loaded WinUI3 popup.
    private static (FakeTreeProvider Provider, ElementHandle Surface, ElementHandle Outside, ElementHandle Inside) BuildPopupScenario(bool showing = true)
    {
        var provider = new FakeTreeProvider();
        var outside = new ElementHandle("cancel-main", "fake", new object()) { Name = "Cancel" };
        var surface = new ElementHandle("menu", "fake", new object()) { Name = "Menu", ControlType = "Menu", Provider = provider, BoundingRect = new Rect(0, 0, 100, 100), IsOffscreen = !showing };
        var inside = new ElementHandle("cancel-menu", "fake", new object()) { Name = "Cancel" };
        provider.Add(outside, null);
        provider.Add(surface, null);
        provider.Add(inside, "menu");
        return (provider, surface, outside, inside);
    }

    // Opens surface as a popup of an App over provider, the way App.OpenPopupCoreAsync does once detection has found it.
    private static Popup OpenPopup(FakeTreeProvider provider, ElementHandle surface, string[]? providerChain = null, int implicitWaitMs = 0) =>
        new(App.CreateForTesting(Session, [provider], new AppOptions { ProviderChain = providerChain ?? ["fake"], ImplicitWaitMs = implicitWaitMs }), surface, "context menu");

    [Fact]
    public async Task Popup_FindFindAllAndWaitFor_OnlySearchInsideThePopup_WhileTheAppStillSearchesTheSession()
    {
        var (provider, surface, outside, inside) = BuildPopupScenario();
        var app = App.CreateForTesting(Session, [provider], new AppOptions { ProviderChain = ["fake"], ImplicitWaitMs = 0 });
        var popup = new Popup(app, surface, "context menu");

        Assert.Same(outside, await app.FindAsync(Locator.ByName("Cancel")));
        Assert.Same(inside, await popup.FindAsync(Locator.ByName("Cancel")));
        Assert.Same(inside, Assert.Single(await popup.FindAllAsync(Locator.ByName("Cancel"))));
        Assert.Same(inside, await popup.WaitForAsync(Locator.ByName("Cancel"), _ => true));
    }

    // app.Force reuses its App's session, so a popup's Force view keeps the popup root without being told to.
    [Fact]
    public async Task Popup_ForceView_KeepsThePopupRoot()
    {
        var (provider, surface, _, inside) = BuildPopupScenario();

        Assert.Same(inside, await OpenPopup(provider, surface).Force.FindAsync(Locator.ByName("Cancel")));
    }

    [Fact]
    public async Task Popup_NoLongerShowing_FindThrowsStaleNamingThePopup()
    {
        var (provider, surface, _, _) = BuildPopupScenario(showing: false);

        var ex = await Assert.ThrowsAsync<StaleElementError>(() => OpenPopup(provider, surface, implicitWaitMs: 5_000).FindAsync(Locator.ByName("Cancel")));

        Assert.Same(surface, ex.Element);
        Assert.Contains("context menu has closed", ex.Message);
    }

    // A closed popup's elements are gone with it, which is exactly what WaitUntilGoneAsync waits for.
    [Fact]
    public async Task Popup_NoLongerShowing_WaitUntilGoneSucceeds()
    {
        var (provider, surface, _, _) = BuildPopupScenario(showing: false);

        await OpenPopup(provider, surface).WaitUntilGoneAsync(Locator.ByName("Cancel"));
    }

    // A popup's RootWindowHandle is the app's window, so window management through it would act on the app; it throws pointing at CloseAsync instead.
    [Fact]
    public async Task Popup_WindowManagement_ThrowsPointingAtCloseAsync()
    {
        var (provider, surface, _, _) = BuildPopupScenario();
        var popup = OpenPopup(provider, surface);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => popup.CloseWindowAsync());
        Assert.Contains("CloseAsync", ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => popup.MoveWindowAsync(0, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => popup.ResizeWindowAsync(100, 100));
        await Assert.ThrowsAsync<InvalidOperationException>(() => popup.SetWindowStateAsync(WindowState.Normal));
    }

    // A window view opened from a popup roots at that window, not inside the popup.
    [Fact]
    public void WithRootWindow_DropsAPopupRoot()
    {
        var (_, surface, _, _) = BuildPopupScenario();

        Assert.Null(Session.WithRootElement(surface, "context menu").WithRootWindow((IntPtr)43).RootElement);
    }

    // A native #32768 surface comes from Win32Provider, which the configured chain may leave out; the popup searches through its surface's provider regardless.
    [Fact]
    public async Task Popup_SurfaceProviderOutsideTheChain_StillSearchesThroughIt()
    {
        var (provider, surface, _, inside) = BuildPopupScenario();

        Assert.Same(inside, await OpenPopup(provider, surface, providerChain: ["uia3"]).FindAsync(Locator.ByName("Cancel")));
    }
}
