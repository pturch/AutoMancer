// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Core;

// Proves FindByScrollingCoreAsync's loop logic — reveal-after-N-scrolls and stall-based early termination — against a fake provider and a fake scroll step, so the loop is exercised with no real ScrollWheelAction/SendInput call (see ScrollWheelActionTests/DoubleClickActionTests: this codebase never drives the real SendInput happy path from a unit test).
public sealed class FindByScrollingTests
{
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

    private static App BuildApp(FakeRevealProvider provider) =>
        App.CreateForTesting(
            AppSession.CreateForTesting(ExitedProcess(), (IntPtr)42),
            [provider],
            new AppOptions { Logger = null, ProviderChain = ["fake"], ImplicitWaitMs = 0, PollIntervalMs = 0, ActionDelayMs = 0, ForegroundActivationTimeoutMs = 0 });

    // A fake provider whose FindScopedElementAsync only starts returning the target item once it's been called more than revealAfterScrolls times — simulating a virtualized row that isn't realized in the tree until enough scrolling has happened.
    private sealed class FakeRevealProvider : IElementProvider
    {
        private readonly ElementHandle _item;
        private readonly int _revealAfterScrolls;
        public int FindAttempts { get; private set; }

        public FakeRevealProvider(ElementHandle item, int revealAfterScrolls)
        {
            _item = item;
            _revealAfterScrolls = revealAfterScrolls;
        }

        public string ProviderName => "fake";

        public Task<ElementHandle?> FindScopedElementAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default)
        {
            FindAttempts++;
            // The Nth find attempt happens after N-1 scrolls — reveal once enough scrolls have happened.
            return Task.FromResult(FindAttempts - 1 >= _revealAfterScrolls ? _item : null);
        }

        // Backs FindByScrollingCoreAsync's post-find RuntimeId re-resolve (ScrollIntoView needs fresh IsOffscreen/BoundingRect off a re-resolved handle, not the stale one FindScopedElementAsync returned).
        public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
            Task.FromResult(locator.Strategy == LocatorStrategy.RuntimeId && locator.Value == _item.Id ? _item : null);

        public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>());

        public Task<IReadOnlyList<ElementHandle>> FindScopedElementsAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>());

        public Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementSnapshot>)Array.Empty<ElementSnapshot>());
    }

    [Fact]
    public async Task FindByScrollingCoreAsync_ItemRevealedAfterThreeScrolls_FindsItAndStopsScrolling()
    {
        var item = new ElementHandle("row-500", "fake", new object()) { Name = "Row 500" };
        var provider = new FakeRevealProvider(item, revealAfterScrolls: 3);
        var app = BuildApp(provider);
        var container = new ElementHandle("list", "fake", new object()) { Name = "List" };
        var scrollCalls = 0;

        var result = await app.FindByScrollingCoreAsync(container, Locator.ByName("Row 500"), maxScrolls: 10, _ => { scrollCalls++; return Task.CompletedTask; }, CancellationToken.None);

        Assert.Same(item, result);
        Assert.Equal(3, scrollCalls);
    }

    [Fact]
    public async Task FindByScrollingCoreAsync_ItemNeverRevealed_ExhaustsMaxScrollsAndThrows()
    {
        var provider = new FakeRevealProvider(new ElementHandle("row-x", "fake", new object()), revealAfterScrolls: 999);
        var app = BuildApp(provider);
        var container = new ElementHandle("list", "fake", new object()) { Name = "List" };
        var scrollCalls = 0;

        await Assert.ThrowsAsync<ElementNotFoundError>(() =>
            app.FindByScrollingCoreAsync(container, Locator.ByName("Row X"), maxScrolls: 5, _ => { scrollCalls++; return Task.CompletedTask; }, CancellationToken.None));

        Assert.Equal(5, scrollCalls);
    }

    // Control test: when the container's scroll position stalls (CurrentVerticalScrollPercent unchanged across two consecutive scrolls), the loop must throw immediately rather than burning through the rest of maxScrolls.
    [Fact]
    public async Task FindByScrollingCoreAsync_ScrollPercentStalls_ThrowsBeforeExhaustingMaxScrolls()
    {
        var provider = new FakeRevealProvider(new ElementHandle("row-x", "fake", new object()), revealAfterScrolls: 999);
        var app = BuildApp(provider);

        var pattern = new Mock<IUIAutomationScrollPattern>();
        pattern.Setup(p => p.CurrentVerticalScrollPercent).Returns(50.0);
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ScrollPatternId)).Returns(pattern.Object);
        var container = new ElementHandle("list", "fake", native.Object) { Name = "List" };
        var scrollCalls = 0;

        await Assert.ThrowsAsync<ElementNotFoundError>(() =>
            app.FindByScrollingCoreAsync(container, Locator.ByName("Row X"), maxScrolls: 20, _ => { scrollCalls++; return Task.CompletedTask; }, CancellationToken.None));

        Assert.Equal(2, scrollCalls);
        Assert.NotEqual(20, provider.FindAttempts);
    }
}
