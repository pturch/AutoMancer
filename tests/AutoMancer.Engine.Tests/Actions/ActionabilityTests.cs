// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

// Action-level actionability checks, app.Force, and keyboard focus against mocked UIA3 elements; no test sends real input.
public sealed class ActionabilityTests
{
    private const int UiaElementNotAvailable = unchecked((int)0x80040201);

    // A UIA3 element whose one batched live read reports these values; its zero window handle skips EnsureForeground's SetForegroundWindow, so nothing touches the real desktop.
    private static Mock<IUIAutomationElement> LiveNative(bool isEnabled = true, bool isOffscreen = false, tagRECT? bounds = null)
    {
        var live = new Mock<IUIAutomationElement>();
        live.SetupGet(e => e.CachedIsEnabled).Returns(isEnabled ? 1 : 0);
        live.SetupGet(e => e.CachedIsOffscreen).Returns(isOffscreen ? 1 : 0);
        live.SetupGet(e => e.CachedBoundingRectangle).Returns(bounds ?? new tagRECT { left = 0, top = 0, right = 100, bottom = 30 });
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.BuildUpdatedCache(It.IsAny<IUIAutomationCacheRequest>())).Returns(live.Object);
        native.SetupGet(e => e.CurrentNativeWindowHandle).Returns(IntPtr.Zero);
        return native;
    }

    // A handle over native whose find-time state says enabled and visible unless overridden.
    private static ElementHandle Handle(Mock<IUIAutomationElement> native, IElementOperator? op = null, bool foundOffscreen = false, bool force = false) =>
        new("1", "uia3", native.Object) { Name = "Save", AutomationId = "SaveButton", IsEnabled = true, IsOffscreen = foundOffscreen, BoundingRect = new Rect(0, 0, 100, 30), Operator = op, Force = force };

    // An operator whose native click and set-value both succeed, so a passing gate ends in the native path with no SendInput.
    private static Mock<IElementOperator> NativeOperator()
    {
        var op = new Mock<IElementOperator>();
        op.Setup(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        op.Setup(o => o.TrySetValueAsync(It.IsAny<ElementHandle>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return op;
    }

    // ---- Live state, and the gate before native patterns ----

    // Found visible, since hidden (a held menu item after its menu closed): the live read wins and the native Invoke never runs.
    [Fact]
    public async Task Click_FoundVisibleNowHidden_RejectedBeforeNativeInvoke()
    {
        var op = NativeOperator();

        var ex = await Assert.ThrowsAsync<ElementNotInteractableError>(() => ClickAction.ExecuteAsync(Handle(LiveNative(isOffscreen: true), op.Object)));

        Assert.Contains("IsOffscreen=true", ex.Message);
        op.Verify(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Found offscreen, since scrolled into view: the live read wins the other way too.
    [Fact]
    public async Task Click_FoundOffscreenNowVisible_Allowed()
    {
        var op = NativeOperator();

        await ClickAction.ExecuteAsync(Handle(LiveNative(), op.Object, foundOffscreen: true));

        op.Verify(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false, false, "IsEnabled=false")]
    [InlineData(true, true, "IsOffscreen=true")]
    public async Task TypeAndClear_HiddenOrDisabled_RejectedBeforeNativeSetValue(bool isEnabled, bool isOffscreen, string reason)
    {
        var op = NativeOperator();
        var element = Handle(LiveNative(isEnabled, isOffscreen), op.Object);

        Assert.Contains(reason, (await Assert.ThrowsAsync<ElementNotInteractableError>(() => TypeAction.ExecuteAsync(element, "hi"))).Message);
        Assert.Contains(reason, (await Assert.ThrowsAsync<ElementNotInteractableError>(() => ClearAction.ExecuteAsync(element))).Message);
        op.Verify(o => o.TrySetValueAsync(It.IsAny<ElementHandle>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Not reported offscreen, but with no on-screen area — still nothing a user could reach.
    [Fact]
    public async Task Click_EmptyBoundsNotOffscreen_Rejected()
    {
        var op = NativeOperator();

        var ex = await Assert.ThrowsAsync<ElementNotInteractableError>(() => ClickAction.ExecuteAsync(Handle(LiveNative(bounds: new tagRECT { left = 10, top = 10, right = 10, bottom = 10 }), op.Object)));

        Assert.Contains("empty on-screen bounds", ex.Message);
        op.Verify(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Force ----

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Click_Forced_HiddenOrDisabled_RunsNativeInvoke(bool isEnabled, bool isOffscreen)
    {
        var op = NativeOperator();

        await ClickAction.ExecuteAsync(Handle(LiveNative(isEnabled, isOffscreen), op.Object, force: true));

        op.Verify(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Force skips the gate, not the need for a real point: a synthesized click with empty bounds has nowhere to aim.
    [Fact]
    public async Task Click_Forced_SynthesizedWithEmptyBounds_StillThrows()
    {
        var element = Handle(LiveNative(bounds: new tagRECT { left = 10, top = 10, right = 10, bottom = 10 }), force: true);

        var ex = await Assert.ThrowsAsync<ElementNotInteractableError>(() => ClickAction.ExecuteAsync(element));

        Assert.Contains("no point to aim", ex.Message);
    }

    [Fact]
    public async Task Click_Forced_StaleElement_StillThrowsStaleElementError()
    {
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.BuildUpdatedCache(It.IsAny<IUIAutomationCacheRequest>())).Throws(new COMException("gone", UiaElementNotAvailable));
        var op = NativeOperator();

        await Assert.ThrowsAsync<StaleElementError>(() => ClickAction.ExecuteAsync(Handle(native, op.Object, force: true)));

        op.Verify(o => o.TryClickAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Keyboard focus on the synthesized typing/clearing path ----

    // SetFocus runs first, then the live focus read decides; empty text means a passing check sends no real keystrokes.
    [Fact]
    public async Task Type_Synthesized_SetsFocusThenConfirmsIt()
    {
        var calls = new List<string>();
        var native = LiveNative();
        native.Setup(e => e.SetFocus()).Callback(() => calls.Add("SetFocus"));
        native.SetupGet(e => e.CurrentHasKeyboardFocus).Returns(() => { calls.Add("HasKeyboardFocus"); return 1; });

        await TypeAction.ExecuteAsync(Handle(native), "");

        Assert.Equal(["SetFocus", "HasKeyboardFocus"], calls);
    }

    // Before this check, the keystrokes went to whatever control already had focus.
    [Fact]
    public async Task TypeAndClear_Synthesized_FocusNotTaken_ThrowBeforeAnyKeystroke()
    {
        var element = Handle(LiveNative());

        Assert.Contains("did not take keyboard focus", (await Assert.ThrowsAsync<ElementNotInteractableError>(() => TypeAction.ExecuteAsync(element, "hello"))).Message);
        Assert.Contains("did not take keyboard focus", (await Assert.ThrowsAsync<ElementNotInteractableError>(() => ClearAction.ExecuteAsync(element))).Message);
    }

    // A combo box's inner edit, say: focus on a descendant counts as the element having it.
    [Fact]
    public async Task Type_Synthesized_FocusOnDescendant_Accepted()
    {
        var native = LiveNative();
        native.Setup(e => e.FindFirst(It.IsAny<Interop.UIAutomationClient.TreeScope>(), It.IsAny<IUIAutomationCondition>())).Returns(new Mock<IUIAutomationElement>().Object);

        await TypeAction.ExecuteAsync(Handle(native), "");

        native.Verify(e => e.FindFirst(Interop.UIAutomationClient.TreeScope.TreeScope_Descendants, It.IsAny<IUIAutomationCondition>()), Times.Once);
    }

    // Force skips the focus check too: SetFocus is still attempted, but typing goes ahead when focus didn't land (empty text, so no real keystroke is sent).
    [Fact]
    public async Task Type_Synthesized_Forced_FocusNotTaken_ProceedsAfterTryingSetFocus()
    {
        var native = LiveNative(isEnabled: false);
        var element = Handle(native, force: true);

        await TypeAction.ExecuteAsync(element, "");

        native.Verify(e => e.SetFocus(), Times.Once);
    }

    // An element that rejects SetFocus but already holds focus is fine — the read decides, not SetFocus's outcome.
    [Fact]
    public void EnsureKeyboardFocus_SetFocusFailsButFocused_Passes()
    {
        var native = LiveNative();
        native.Setup(e => e.SetFocus()).Throws(new COMException("not focusable", unchecked((int)0x80131505)));
        native.SetupGet(e => e.CurrentHasKeyboardFocus).Returns(1);

        ElementInputHelpers.EnsureKeyboardFocus(Handle(native));
    }

    [Fact]
    public void EnsureKeyboardFocus_StaleSetFocus_ThrowsStaleElementError()
    {
        var native = LiveNative();
        native.Setup(e => e.SetFocus()).Throws(new COMException("gone", UiaElementNotAvailable));
        var element = Handle(native);

        Assert.Same(element, Assert.Throws<StaleElementError>(() => ElementInputHelpers.EnsureKeyboardFocus(element)).Element);
    }

    // Non-UIA stand-ins have nothing to focus, so they keep typing into the foregrounded window; Win32 hwnds are covered in ElementInputHelpersTests against real windows.
    [Fact]
    public void EnsureKeyboardFocus_NonUiaStandIn_SkipsFocusStep()
        => ElementInputHelpers.EnsureKeyboardFocus(new ElementHandle("1", "test", new object()));
}
