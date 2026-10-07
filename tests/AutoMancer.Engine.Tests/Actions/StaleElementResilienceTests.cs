// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Runtime.InteropServices;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Operators;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

// Proves App's stale-element pipeline end to end against fakes: a stale COM failure surfaces as StaleElementError, ReresolveOnStale re-runs the original locator (not the stale RuntimeId), the opt-out propagates, and an unrelated COM failure is still swallowed. 
// Fresh handles succeed via their native operator, so no test here drives real SendInput.
public sealed class StaleElementResilienceTests
{
    private const int UiaElementNotAvailable = unchecked((int)0x80040201);
    private static readonly Locator Target = Locator.ByAutomationId("SaveButton");

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

    private static App BuildApp(IElementProvider provider, bool reresolveOnStale, int implicitWaitMs = 1_000, int pollIntervalMs = 0) =>
        App.CreateForTesting(
            AppSession.CreateForTesting(ExitedProcess(), (IntPtr)42),
            [provider],
            new AppOptions { Logger = null, ProviderChain = ["fake"], ImplicitWaitMs = implicitWaitMs, PollIntervalMs = pollIntervalMs, ActionDelayMs = 0, ForegroundActivationTimeoutMs = 0, ReresolveOnStale = reresolveOnStale });

    // A fake provider answering whole-session finds via find and scoped finds via findScoped, so each test scripts exactly which handle each lookup returns.
    private sealed class DelegateProvider(Func<Locator, ElementHandle?> find, Func<ElementHandle, ElementHandle?>? findScoped = null) : IElementProvider
    {
        public string ProviderName => "fake";

        public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
            Task.FromResult(find(locator));

        public Task<ElementHandle?> FindScopedElementAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
            Task.FromResult(findScoped?.Invoke(scope));

        public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>());

        public Task<IReadOnlyList<ElementHandle>> FindScopedElementsAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>());

        public Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementSnapshot>)Array.Empty<ElementSnapshot>());
    }

    // A handle whose native operator declines (forcing the SendInput fallback) and whose live CurrentNativeWindowHandle read fails with hresult — UIA_E_ELEMENTNOTAVAILABLE for a genuinely gone element.
    private static ElementHandle FailingHandle(string id, int hresult, bool? isEnabled = null)
    {
        var native = new Mock<IUIAutomationElement>();
        native.SetupGet(e => e.CurrentNativeWindowHandle).Throws(new COMException("live read failed", hresult));
        return new ElementHandle(id, "uia3", native.Object) { Name = "Save", AutomationId = "SaveButton", IsEnabled = isEnabled, Operator = new Mock<IElementOperator>().Object };
    }

    // A live handle whose native click/set-value succeeds, so the action completes without ever reaching SendInput.
    private static (ElementHandle Handle, Mock<IElementOperator> Operator) FreshHandle(string id)
    {
        var op = new Mock<IElementOperator>();
        op.Setup(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        op.Setup(o => o.TrySetValueAsync(It.IsAny<ElementHandle>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return (new ElementHandle(id, "uia3", new object()) { Name = "Save", AutomationId = "SaveButton", Operator = op.Object }, op);
    }

    private static Task Run(App app, string action) => action switch
    {
        "click" => app.ClickAsync(Target),
        "type" => app.TypeAsync(Target, "hello"),
        "clear" => app.ClearAsync(Target),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    [Theory]
    [InlineData("click")]
    [InlineData("type")]
    [InlineData("clear")]
    public async Task StaleOnce_ReresolveOnStale_RerunsOriginalLocatorAndSucceeds(string action)
    {
        var stale = FailingHandle("42.1.1", UiaElementNotAvailable);
        // A recreated element gets a new RuntimeId — the retry must reach it via the original locator, not the stale id.
        var (fresh, freshOp) = FreshHandle("42.9.9");
        var finds = new List<Locator>();
        var app = BuildApp(new DelegateProvider(locator => { finds.Add(locator); return finds.Count == 1 ? stale : fresh; }), reresolveOnStale: true);

        await Run(app, action);

        Assert.Equal([Target, Target], finds);
        Assert.Single(freshOp.Invocations);
    }

    [Theory]
    [InlineData("click")]
    [InlineData("type")]
    [InlineData("clear")]
    public async Task StaleOnce_ReresolveOnStaleFalse_PropagatesWithoutRetry(string action)
    {
        var stale = FailingHandle("42.1.1", UiaElementNotAvailable);
        var (fresh, _) = FreshHandle("42.9.9");
        var findCalls = 0;
        var app = BuildApp(new DelegateProvider(_ => ++findCalls == 1 ? stale : fresh), reresolveOnStale: false);

        var ex = await Assert.ThrowsAsync<StaleElementError>(() => Run(app, action));

        Assert.Same(stale, ex.Element);
        Assert.Equal(1, findCalls);
    }

    // Control: a COM failure that isn't UIA_E_ELEMENTNOTAVAILABLE (the VLC split-button timeout) is still logged and skipped, either way the flag is set — reaching EnsureInteractable's disabled-element error proves EnsureForeground carried on rather than raising staleness, without sending any input.
    [Theory]
    [InlineData("click", true)]
    [InlineData("click", false)]
    [InlineData("type", true)]
    [InlineData("type", false)]
    [InlineData("clear", true)]
    [InlineData("clear", false)]
    public async Task NonStaleComException_StillSwallowed_NoRetry(string action, bool reresolveOnStale)
    {
        var timedOut = FailingHandle("42.1.1", unchecked((int)0x80131505), isEnabled: false);
        var findCalls = 0;
        var app = BuildApp(new DelegateProvider(_ => { findCalls++; return timedOut; }), reresolveOnStale);

        await Assert.ThrowsAsync<ElementNotInteractableError>(() => Run(app, action));

        Assert.Equal(1, findCalls);
    }

    // Retrying is bounded by ImplicitWaitMs — an element that stays stale eventually surfaces StaleElementError instead of looping forever.
    [Fact]
    public async Task AlwaysStale_ReresolveOnStale_GivesUpAfterImplicitWait()
    {
        var findCalls = 0;
        var app = BuildApp(new DelegateProvider(_ => FailingHandle($"42.1.{++findCalls}", UiaElementNotAvailable)), reresolveOnStale: true, implicitWaitMs: 200, pollIntervalMs: 20);

        await Assert.ThrowsAsync<StaleElementError>(() => app.ClickAsync(Target));

        Assert.True(findCalls > 1, $"expected at least one re-resolve before giving up, saw {findCalls} find(s)");
    }

    // End to end through the real ScrollWheelAction: the stale container's EnsureForeground raises StaleElementError, and FindByScrollingAsync re-finds the container by locator and carries on against the fresh one.
    [Fact]
    public async Task FindByScrollingAsync_ContainerGoesStaleMidScroll_ContinuesAgainstFreshContainer()
    {
        var containerLocator = Locator.ByAutomationId("FileList");
        var staleContainer = FailingHandle("42.1.1", UiaElementNotAvailable);
        var freshContainer = new ElementHandle("42.9.9", "uia3", new object()) { Name = "FileList" };
        var item = new ElementHandle("42.5.5", "uia3", new object()) { Name = "Row 500" };
        var containerFinds = 0;
        var app = BuildApp(
            new DelegateProvider(
                locator => locator.Strategy == LocatorStrategy.RuntimeId ? item : ++containerFinds == 1 ? staleContainer : freshContainer,
                // Only the fresh container has the item — the stale one's first pass misses, sending the loop to a scroll that hits the stale handle.
                scope => scope == freshContainer ? item : null),
            reresolveOnStale: true);

        var result = await app.FindByScrollingAsync(containerLocator, Locator.ByName("Row 500"));

        Assert.Same(item, result);
        Assert.Equal(2, containerFinds);
    }

    // No time window applies to a stale container — a real 20-notch scroll loop runs ~8s, past a 5s ImplicitWaitMs, so a container rebuilt late in it must still be re-found rather than propagate; maxScrolls is the bound instead.
    [Fact]
    public async Task FindByScrollingAsync_ContainerGoesStaleAfterImplicitWaitElapsed_StillContinues()
    {
        const int implicitWaitMs = 200;
        var staleContainer = FailingHandle("42.1.1", UiaElementNotAvailable);
        var freshContainer = new ElementHandle("42.9.9", "uia3", new object()) { Name = "FileList" };
        var item = new ElementHandle("42.5.5", "uia3", new object()) { Name = "Row 500" };
        var containerFinds = 0;
        var app = BuildApp(
            new DelegateProvider(
                locator => locator.Strategy == LocatorStrategy.RuntimeId ? item : ++containerFinds == 1 ? staleContainer : freshContainer,
                scope =>
                {
                    // The stale container's first pass outlasts ImplicitWaitMs before its scroll hits the stale handle, standing in for a long scroll loop.
                    if (scope == staleContainer) Thread.Sleep(implicitWaitMs + 150);
                    return scope == freshContainer ? item : null;
                }),
            reresolveOnStale: true,
            implicitWaitMs: implicitWaitMs);

        var result = await app.FindByScrollingAsync(Locator.ByAutomationId("FileList"), Locator.ByName("Row 500"));

        Assert.Same(item, result);
        Assert.Equal(2, containerFinds);
    }

    // A handle backed by the real Uia3Operator whose live pattern read fails with hresult, or (hresult null) returns a ValuePattern reporting value.
    private static ElementHandle ValueHandle(string id, int? hresult, string value = "")
    {
        var native = new Mock<IUIAutomationElement>();
        if (hresult is { } hr)
            native.Setup(e => e.GetCurrentPattern(It.IsAny<int>())).Throws(new COMException("live read failed", hr));
        else
        {
            var pattern = new Mock<IUIAutomationValuePattern>();
            pattern.SetupGet(p => p.CurrentValue).Returns(value);
            native.Setup(e => e.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId)).Returns(pattern.Object);
        }
        return new ElementHandle(id, "uia3", native.Object) { Name = "Document", Operator = new Uia3Operator() };
    }

    // GetValueAsync used to read a stale element as null — indistinguishable from "no value" — so it now raises staleness and retries like the actions do.
    [Fact]
    public async Task GetValueAsync_StaleOnce_ReresolveOnStale_ReadsFreshElementsValue()
    {
        var findCalls = 0;
        var app = BuildApp(new DelegateProvider(_ => ++findCalls == 1 ? ValueHandle("42.1.1", UiaElementNotAvailable) : ValueHandle("42.9.9", null, "fresh value")), reresolveOnStale: true);

        var value = await app.GetValueAsync(Target);

        Assert.Equal("fresh value", value);
        Assert.Equal(2, findCalls);
    }

    [Fact]
    public async Task GetValueAsync_Stale_ReresolveOnStaleFalse_ThrowsInsteadOfReturningNull()
    {
        var app = BuildApp(new DelegateProvider(_ => ValueHandle("42.1.1", UiaElementNotAvailable)), reresolveOnStale: false);

        await Assert.ThrowsAsync<StaleElementError>(() => app.GetValueAsync(Target));
    }

    // Control: a non-stale COM failure on the read still degrades to null, unchanged — only failures the shared stale definition recognizes are promoted to staleness.
    [Fact]
    public async Task GetValueAsync_NonStaleComException_StillReturnsNull_NoRetry()
    {
        var findCalls = 0;
        var app = BuildApp(new DelegateProvider(_ => { findCalls++; return ValueHandle("42.1.1", unchecked((int)0x80131505)); }), reresolveOnStale: true);

        var value = await app.GetValueAsync(Target);

        Assert.Null(value);
        Assert.Equal(1, findCalls);
    }
}
