// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Core;

// A popup opened by App.OpenPopupAsync or OpenContextMenuAsync: a view of its App whose session is rooted at the popup's surface (AppSession.WithRootElement), so finds, waits and actions run inside it only, through the surface's own provider (UIA3, or Win32Provider reading a #32768's HMENU); it's never reopened, so a popup that closes stays closed.
public sealed class Popup : App
{
    // The popup's surface: a UIA3 element (an in-window popup, or a popup window), or a native #32768 wrapped by Win32Provider; its Provider searches under it.
    private readonly ElementHandle _surface;

    // Built only by App's popup methods, around the surface PopupAction detected; opener is the App (or its app.Force view) that opened it, so found elements get its settings.
    internal Popup(App opener, ElementHandle surface, string label) : base(opener, surface, label) => _surface = surface;

    // True while the popup is still showing (ElementInputHelpers.IsShowing): attached, on screen, and in a visible window.
    public Task<bool> IsOpenAsync(CancellationToken ct = default) => Task.Run(() => ElementInputHelpers.IsShowing(_surface), ct);

    // Dismisses the popup with Escape and waits up to ImplicitWaitMs for it to close; no-op if it's already closed, so a stray Escape never reaches the app. A tooltip ignores Escape (it closes when the pointer leaves), so hover elsewhere and use WaitUntilClosedAsync instead.
    public async Task CloseAsync(CancellationToken ct = default)
    {
        if (!await IsOpenAsync(ct).ConfigureAwait(false))
            return;
        // Not foregrounding anything: the popup holds input, and activating a window would dismiss it some other way.
        await KeyboardAction.PressKeyCoreAsync(Key.Escape, KeyModifiers.None, Logger, ct).ConfigureAwait(false);
        try { await WaitUntilClosedAsync(ct).ConfigureAwait(false); }
        catch (ElementStillPresentError ex)
        {
            throw new ElementStillPresentError(ex.Locator, ex.ElapsedMs, "Escape closes one popup level at a time, so a submenu or other popup opened from this one may still be open; close it first.");
        }
    }

    // Waits up to ImplicitWaitMs for the popup to close on its own (a tooltip timing out, a menu dismissed by a click); ElementStillPresentError if it doesn't.
    public Task WaitUntilClosedAsync(CancellationToken ct = default) =>
        _resolver.PollUntilGoneOutsideChainAsync(Locator.ByControlType(_surface.ControlType ?? "Window"), IsOpenAsync, ct);
}
