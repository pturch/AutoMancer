// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Runtime.InteropServices;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Core;

// Proves FindByScrollingCoreAsync's loop logic — reveal-after-N-scrolls, stall-based early termination, and stale-container re-resolve — against a fake provider and a fake scroll step, so the loop is exercised with no real ScrollWheelAction/SendInput call (see ScrollWheelActionTests/DoubleClickActionTests: this codebase never drives the real SendInput happy path from a unit test).
public sealed class FindByScrollingTests
{
    private static readonly Locator ListLocator = Locator.ByName("List");

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

    private static App BuildApp(FakeRevealProvider provider, bool reresolveOnStale = true) =>
        App.CreateForTesting(
            AppSession.CreateForTesting(ExitedProcess(), (IntPtr)42),
            [provider],
            new AppOptions { Logger = null, ProviderChain = ["fake"], ImplicitWaitMs = 0, PollIntervalMs = 0, ActionDelayMs = 0, ForegroundActivationTimeoutMs = 0, ReresolveOnStale = reresolveOnStale });

    // A fake provider whose FindScopedElementAsync only starts returning the target item once it's been called more than revealAfterScrolls times — simulating a virtualized row that isn't realized in the tree until enough scrolling has happened.
    private sealed class FakeRevealProvider : IElementProvider
    {
        private readonly ElementHandle _item;
        private readonly int _revealAfterScrolls;
        public int FindAttempts { get; private set; }
        public int ContainerFinds { get; private set; }

        // Answers the container locator; called once per (re-)resolve, so a test can hand back a stale container first and a rebuilt one after.
        public Func<ElementHandle> NextContainer { get; set; } = () => throw new InvalidOperationException("NextContainer not set");

        // A scope whose scoped find raises StaleElementError, as a real provider's liveness probe does for a gone scope.
        public ElementHandle? DeadScope { get; set; }

        // Builds the provider around the item it eventually reveals.
        public FakeRevealProvider(ElementHandle item, int revealAfterScrolls)
        {
            _item = item;
            _revealAfterScrolls = revealAfterScrolls;
        }

        public string ProviderName => "fake";

        public Task<ElementHandle?> FindScopedElementAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default)
        {
            if (scope == DeadScope)
                throw new StaleElementError(scope, "scope gone");
            FindAttempts++;
            // The Nth find attempt happens after N-1 scrolls — reveal once enough scrolls have happened.
            return Task.FromResult(FindAttempts - 1 >= _revealAfterScrolls ? _item : null);
        }

        // Backs the container resolve and FindByScrollingCoreAsync's post-find RuntimeId re-resolve (ScrollIntoView needs fresh IsOffscreen/BoundingRect off a re-resolved handle, not the stale one FindScopedElementAsync returned).
        public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default)
        {
            if (locator.Strategy == LocatorStrategy.RuntimeId)
                return Task.FromResult(locator.Value == _item.Id ? _item : null);
            ContainerFinds++;
            return Task.FromResult<ElementHandle?>(NextContainer());
        }

        public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>());

        public Task<IReadOnlyList<ElementHandle>> FindScopedElementsAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>());

        public Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementSnapshot>)Array.Empty<ElementSnapshot>());
    }

    // A container whose ScrollPattern always reports the same percentage, so two consecutive scrolls read as a stall.
    private static ElementHandle StalledContainer()
    {
        var pattern = new Mock<IUIAutomationScrollPattern>();
        pattern.Setup(p => p.CurrentVerticalScrollPercent).Returns(50.0);
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ScrollPatternId)).Returns(pattern.Object);
        return new ElementHandle("list", "fake", native.Object) { Name = "List" };
    }

    [Fact]
    public async Task FindByScrollingCoreAsync_ItemRevealedAfterThreeScrolls_FindsItAndStopsScrolling()
    {
        var item = new ElementHandle("row-500", "fake", new object()) { Name = "Row 500" };
        var provider = new FakeRevealProvider(item, revealAfterScrolls: 3);
        var container = new ElementHandle("list", "fake", new object()) { Name = "List" };
        provider.NextContainer = () => container;
        var app = BuildApp(provider);
        var scrollCalls = 0;

        var result = await app.FindByScrollingCoreAsync(ListLocator, Locator.ByName("Row 500"), maxScrolls: 10, (_, _) => { scrollCalls++; return Task.CompletedTask; }, CancellationToken.None);

        Assert.Same(item, result);
        Assert.Equal(3, scrollCalls);
    }

    [Fact]
    public async Task FindByScrollingCoreAsync_ItemNeverRevealed_ExhaustsMaxScrollsAndThrows()
    {
        var provider = new FakeRevealProvider(new ElementHandle("row-x", "fake", new object()), revealAfterScrolls: 999);
        var container = new ElementHandle("list", "fake", new object()) { Name = "List" };
        provider.NextContainer = () => container;
        var app = BuildApp(provider);
        var scrollCalls = 0;

        await Assert.ThrowsAsync<ElementNotFoundError>(() =>
            app.FindByScrollingCoreAsync(ListLocator, Locator.ByName("Row X"), maxScrolls: 5, (_, _) => { scrollCalls++; return Task.CompletedTask; }, CancellationToken.None));

        Assert.Equal(5, scrollCalls);
    }

    // Control test: when the container's scroll position stalls (CurrentVerticalScrollPercent unchanged across two consecutive scrolls), the loop must stop scrolling and fall through to its final attempt rather than burning through the rest of maxScrolls.
    [Fact]
    public async Task FindByScrollingCoreAsync_ScrollPercentStalls_ThrowsBeforeExhaustingMaxScrolls()
    {
        var provider = new FakeRevealProvider(new ElementHandle("row-x", "fake", new object()), revealAfterScrolls: 999);
        var container = StalledContainer();
        provider.NextContainer = () => container;
        var app = BuildApp(provider);
        var scrollCalls = 0;

        await Assert.ThrowsAsync<ElementNotFoundError>(() =>
            app.FindByScrollingCoreAsync(ListLocator, Locator.ByName("Row X"), maxScrolls: 20, (_, _) => { scrollCalls++; return Task.CompletedTask; }, CancellationToken.None));

        Assert.Equal(2, scrollCalls);
        Assert.NotEqual(20, provider.FindAttempts);
    }

    // Proves a stall falls through to the final attempt instead of throwing outright, so an item that only appears then (a slow last screen, a list still loading) is still found.
    [Fact]
    public async Task FindByScrollingCoreAsync_ScrollPercentStalls_ItemAppearsOnFinalAttempt_FindsIt()
    {
        var item = new ElementHandle("row-500", "fake", new object()) { Name = "Row 500" };
        // Attempts 1 and 2 run before scrolls 1 and 2; the stall after scroll 2 leaves only the final attempt (3) to reveal it.
        var provider = new FakeRevealProvider(item, revealAfterScrolls: 2);
        var container = StalledContainer();
        provider.NextContainer = () => container;
        var app = BuildApp(provider);
        var scrollCalls = 0;

        var result = await app.FindByScrollingCoreAsync(ListLocator, Locator.ByName("Row 500"), maxScrolls: 20, (_, _) => { scrollCalls++; return Task.CompletedTask; }, CancellationToken.None);

        Assert.Same(item, result);
        Assert.Equal(2, scrollCalls);
    }

    // Proves each scroll tick checks exactly once rather than polling for ImplicitWaitMs — only the final attempt after the loop gets the full poll.
    [Fact]
    public async Task FindByScrollingCoreAsync_EachScrollChecksOnce_OnlyFinalAttemptPolls()
    {
        var provider = new FakeRevealProvider(new ElementHandle("row-x", "fake", new object()), revealAfterScrolls: 999);
        var container = new ElementHandle("list", "fake", new object()) { Name = "List" };
        provider.NextContainer = () => container;
        var app = App.CreateForTesting(
            AppSession.CreateForTesting(ExitedProcess(), (IntPtr)42),
            [provider],
            new AppOptions { Logger = null, ProviderChain = ["fake"], ImplicitWaitMs = 300, PollIntervalMs = 10, ActionDelayMs = 0, ForegroundActivationTimeoutMs = 0 });
        var attemptsAtEachScroll = new List<int>();

        await Assert.ThrowsAsync<ElementNotFoundError>(() =>
            app.FindByScrollingCoreAsync(ListLocator, Locator.ByName("Row X"), maxScrolls: 3, (_, _) => { attemptsAtEachScroll.Add(provider.FindAttempts); return Task.CompletedTask; }, CancellationToken.None));

        Assert.Equal([1, 2, 3], attemptsAtEachScroll);
        Assert.True(provider.FindAttempts > 4, $"final attempt should poll repeatedly within ImplicitWaitMs, saw {provider.FindAttempts - 3} attempt(s)");
    }

    // Proves an item recycled between its find and ScrollIntoView is treated as not-yet-realized — scroll and look again — rather than escaping as a StaleElementError.
    [Fact]
    public async Task FindByScrollingCoreAsync_ItemGoesStaleBeforeScrollIntoView_ScrollsAndFindsItAgain()
    {
        var native = new Mock<IUIAutomationElement>();
        native.SetupSequence(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ScrollItemPatternId))
            .Throws(new COMException("element not available", unchecked((int)0x80040201)))
            .Returns((object)null!);
        var item = new ElementHandle("row-500", "fake", native.Object) { Name = "Row 500" };
        var provider = new FakeRevealProvider(item, revealAfterScrolls: 0);
        var container = new ElementHandle("list", "fake", new object()) { Name = "List" };
        provider.NextContainer = () => container;
        var app = BuildApp(provider);
        var scrollCalls = 0;

        var result = await app.FindByScrollingCoreAsync(ListLocator, Locator.ByName("Row 500"), maxScrolls: 10, (_, _) => { scrollCalls++; return Task.CompletedTask; }, CancellationToken.None);

        Assert.Same(item, result);
        Assert.Equal(1, scrollCalls);
        Assert.Equal(1, provider.ContainerFinds);
    }

    // A container rebuilt mid-loop (its scroll hits the dead handle) is re-found by locator and the search carries on against the new one — no restart, and the failed iteration still counts toward maxScrolls.
    [Fact]
    public async Task FindByScrollingCoreAsync_ContainerGoesStaleOnScroll_RefindsAndContinues()
    {
        var item = new ElementHandle("row-500", "fake", new object()) { Name = "Row 500" };
        var provider = new FakeRevealProvider(item, revealAfterScrolls: 3);
        var stale = new ElementHandle("list-old", "fake", new object()) { Name = "List" };
        var fresh = new ElementHandle("list-new", "fake", new object()) { Name = "List" };
        provider.NextContainer = () => provider.ContainerFinds == 1 ? stale : fresh;
        var app = BuildApp(provider);
        var scrolledContainers = new List<ElementHandle>();

        var result = await app.FindByScrollingCoreAsync(ListLocator, Locator.ByName("Row 500"), maxScrolls: 10, (c, _) =>
        {
            if (c == stale) throw new StaleElementError(c, "gone");
            scrolledContainers.Add(c);
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Same(item, result);
        Assert.Equal(2, provider.ContainerFinds);
        // Iteration 1's scroll hit the stale container; iterations 2 and 3 scrolled the fresh one; iteration 4's find reveals the item.
        Assert.Equal([fresh, fresh], scrolledContainers);
    }

    // A container rebuilt on every iteration can't loop forever: each re-find uses up an iteration, so maxScrolls ends it with the same ElementNotFoundError as an item that isn't there.
    [Fact]
    public async Task FindByScrollingCoreAsync_ContainerStaleEveryIteration_EndsAfterMaxScrolls()
    {
        var provider = new FakeRevealProvider(new ElementHandle("row-x", "fake", new object()), revealAfterScrolls: 999);
        provider.NextContainer = () => new ElementHandle($"list-{provider.ContainerFinds}", "fake", new object()) { Name = "List" };
        var app = BuildApp(provider);

        await Assert.ThrowsAsync<ElementNotFoundError>(() =>
            app.FindByScrollingCoreAsync(ListLocator, Locator.ByName("Row X"), maxScrolls: 5, (c, _) => throw new StaleElementError(c, "gone"), CancellationToken.None));

        // The initial resolve plus one re-find per iteration.
        Assert.Equal(6, provider.ContainerFinds);
    }

    // A container that goes stale during the final, full-wait attempt gets one re-find and a last search of the rebuilt container.
    [Fact]
    public async Task FindByScrollingCoreAsync_ContainerGoesStaleOnFinalAttempt_RefindsOnceAndFindsIt()
    {
        var item = new ElementHandle("row-500", "fake", new object()) { Name = "Row 500" };
        var provider = new FakeRevealProvider(item, revealAfterScrolls: 0);
        var stale = new ElementHandle("list-old", "fake", new object()) { Name = "List" };
        var fresh = new ElementHandle("list-new", "fake", new object()) { Name = "List" };
        provider.NextContainer = () => provider.ContainerFinds == 1 ? stale : fresh;
        provider.DeadScope = stale;
        var app = BuildApp(provider);

        var result = await app.FindByScrollingCoreAsync(ListLocator, Locator.ByName("Row 500"), maxScrolls: 0, (_, _) => Task.CompletedTask, CancellationToken.None);

        Assert.Same(item, result);
        Assert.Equal(2, provider.ContainerFinds);
    }

    // ReresolveOnStale=false leaves recovery to the caller: the first stale container propagates, carrying the dead handle.
    [Fact]
    public async Task FindByScrollingCoreAsync_ContainerGoesStale_ReresolveOnStaleFalse_Propagates()
    {
        var provider = new FakeRevealProvider(new ElementHandle("row-x", "fake", new object()), revealAfterScrolls: 999);
        var container = new ElementHandle("list", "fake", new object()) { Name = "List" };
        provider.NextContainer = () => container;
        var app = BuildApp(provider, reresolveOnStale: false);

        var ex = await Assert.ThrowsAsync<StaleElementError>(() =>
            app.FindByScrollingCoreAsync(ListLocator, Locator.ByName("Row X"), maxScrolls: 5, (c, _) => throw new StaleElementError(c, "gone"), CancellationToken.None));

        Assert.Same(container, ex.Element);
        Assert.Equal(1, provider.ContainerFinds);
    }
}
