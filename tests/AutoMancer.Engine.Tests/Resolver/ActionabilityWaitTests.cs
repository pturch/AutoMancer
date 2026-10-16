// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Runtime.InteropServices;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Resolver;

// App's wait on not-yet-usable elements and app.Force's opt-out, against a fake provider; no test sends real input.
public sealed class ActionabilityWaitTests
{
    private const int UiaElementNotAvailable = unchecked((int)0x80040201);
    private static readonly Locator Target = Locator.ByAutomationId("SaveButton");

    private static Process ExitedProcess()
    {
        var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { UseShellExecute = false, CreateNoWindow = true })!;
        process.WaitForExit(2000);
        return process;
    }

    private static App BuildApp(Func<Locator, ElementHandle?> find, int implicitWaitMs = 5_000, int pollIntervalMs = 20, bool reresolveOnStale = true, Process? process = null) =>
        App.CreateForTesting(
            AppSession.CreateForTesting(process ?? ExitedProcess(), (IntPtr)42),
            [new DelegateProvider(find)],
            new AppOptions { Logger = null, ProviderChain = ["fake"], ImplicitWaitMs = implicitWaitMs, PollIntervalMs = pollIntervalMs, ActionDelayMs = 0, ForegroundActivationTimeoutMs = 0, ReresolveOnStale = reresolveOnStale });

    // A fake provider answering whole-session finds via find; everything else comes back empty.
    private sealed class DelegateProvider(Func<Locator, ElementHandle?> find) : IElementProvider
    {
        public string ProviderName => "fake";

        public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
            Task.FromResult(find(locator));

        public Task<ElementHandle?> FindScopedElementAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
            Task.FromResult<ElementHandle?>(null);

        public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>());

        public Task<IReadOnlyList<ElementHandle>> FindScopedElementsAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>());

        public Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ElementSnapshot>)Array.Empty<ElementSnapshot>());
    }

    // A non-UIA handle (so its find-time state is what the gate judges) whose native click succeeds.
    private static (ElementHandle Handle, Mock<IElementOperator> Operator) Button(bool isEnabled, Rect? bounds = null)
    {
        var op = new Mock<IElementOperator>();
        op.Setup(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return (new ElementHandle("42.1.1", "uia3", new object()) { Name = "Save", AutomationId = "SaveButton", IsEnabled = isEnabled, BoundingRect = bounds ?? new Rect(0, 0, 100, 30), Operator = op.Object }, op);
    }

    // ---- The wait ----

    // A button still enabling: re-found and re-checked every poll until it's usable, then clicked once.
    [Fact]
    public async Task NotYetInteractable_PollsUntilInteractable_ThenActs()
    {
        var finds = 0;
        var (disabled, disabledOp) = Button(isEnabled: false);
        var (enabled, enabledOp) = Button(isEnabled: true);
        var app = BuildApp(_ => ++finds < 3 ? disabled : enabled);

        await app.ClickAsync(Target);

        Assert.Equal(3, finds);
        disabledOp.Verify(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>()), Times.Never);
        enabledOp.Verify(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NeverInteractable_ThrowsAfterImplicitWait()
    {
        var finds = 0;
        var (disabled, _) = Button(isEnabled: false);
        var app = BuildApp(_ => { finds++; return disabled; }, implicitWaitMs: 200);

        await Assert.ThrowsAsync<ElementNotInteractableError>(() => app.ClickAsync(Target));

        Assert.True(finds > 1, $"expected at least one re-check before giving up, saw {finds} find(s)");
    }

    // ---- app.Force ----

    [Fact]
    public async Task Force_DisabledElement_ActedOnImmediatelyWithoutWaiting()
    {
        var finds = 0;
        var (disabled, op) = Button(isEnabled: false);
        var app = BuildApp(_ => { finds++; return disabled; });

        await app.Force.ClickAsync(Target);

        Assert.Equal(1, finds);
        op.Verify(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // A UIA3 handle whose live read fails as gone.
    private static ElementHandle StaleHandle()
    {
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.BuildUpdatedCache(It.IsAny<IUIAutomationCacheRequest>())).Throws(new COMException("gone", UiaElementNotAvailable));
        return new ElementHandle("42.1.1", "uia3", native.Object) { Name = "Save", AutomationId = "SaveButton", Operator = new Mock<IElementOperator>().Object };
    }

    [Fact]
    public async Task Force_StaleElement_StillThrowsStaleElementError()
    {
        var app = BuildApp(_ => StaleHandle(), reresolveOnStale: false);

        await Assert.ThrowsAsync<StaleElementError>(() => app.Force.ClickAsync(Target));
    }

    [Fact]
    public async Task Force_StaleOnce_StillReresolvesViaLocator()
    {
        var finds = 0;
        var (fresh, op) = Button(isEnabled: false);
        var app = BuildApp(_ => ++finds == 1 ? StaleHandle() : fresh);

        await app.Force.ClickAsync(Target);

        Assert.Equal(2, finds);
        op.Verify(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // With no native operator the click is synthesized and needs a point to aim at — Force skips the wait, not that.
    [Fact]
    public async Task Force_SynthesizedClickWithEmptyBounds_ThrowsWithoutWaiting()
    {
        var finds = 0;
        var element = new ElementHandle("42.1.1", "uia3", new object()) { Name = "Save", IsEnabled = true, BoundingRect = new Rect(10, 10, 0, 0) };
        var app = BuildApp(_ => { finds++; return element; });

        await Assert.ThrowsAsync<ElementNotInteractableError>(() => app.Force.ClickAsync(Target));

        Assert.Equal(1, finds);
    }

    // Synthesized typing into a UIA3 element that never reports keyboard focus: Force types anyway, on the first find (empty text, so no real keystroke is sent).
    [Fact]
    public async Task Force_SynthesizedTypeWithoutFocus_ProceedsWithoutWaiting()
    {
        var finds = 0;
        var live = new Mock<IUIAutomationElement>();
        live.SetupGet(e => e.CachedIsEnabled).Returns(1);
        live.SetupGet(e => e.CachedBoundingRectangle).Returns(new tagRECT { left = 0, top = 0, right = 100, bottom = 30 });
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.BuildUpdatedCache(It.IsAny<IUIAutomationCacheRequest>())).Returns(live.Object);
        native.SetupGet(e => e.CurrentNativeWindowHandle).Returns(IntPtr.Zero);
        var element = new ElementHandle("42.1.1", "uia3", native.Object) { Name = "Search", BoundingRect = new Rect(0, 0, 100, 30) };
        var app = BuildApp(_ => { finds++; return element; });

        await app.Force.TypeAsync(Target, "");

        native.Verify(e => e.SetFocus(), Times.Once);
        Assert.Equal(1, finds);
    }

    [Fact]
    public void Force_ReturnsSameCachedInstance()
    {
        var app = BuildApp(_ => null);

        Assert.Same(app.Force, app.Force);
        Assert.Same(app.Force, app.Force.Force);
        Assert.NotSame(app, app.Force);
    }

    // Disposing the view must never close the app under test.
    [Fact]
    public async Task DisposingForce_LeavesSessionAndProcessAlive()
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 30 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        try
        {
            var app = BuildApp(_ => null, process: process);

            await app.Force.DisposeAsync();

            Assert.False(process.HasExited);
            Assert.Equal(process.Id, app.ProcessId);
        }
        finally
        {
            process.Kill(entireProcessTree: true);
        }
    }

    // Input held through the view is tracked by the parent, so disposing the parent releases it.
    [Fact]
    public void Force_SharesParentsHeldInputTracking()
    {
        var app = BuildApp(_ => null);

        app.Force.HeldKeysForTesting.Add(Key.W);

        Assert.Same(app.HeldMouseButtonsForTesting, app.Force.HeldMouseButtonsForTesting);
        Assert.Equal([Key.W], app.HeldKeysForTesting.DrainHeld());
    }
}
