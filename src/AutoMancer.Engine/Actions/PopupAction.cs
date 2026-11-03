// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// How App.OpenPopupAsync/OpenContextMenuAsync open a popup from its target: a right-click, a left-click ("⋯"/split buttons, combo arrows), Shift+F10 or Alt+Down after focusing it (context menu, combo dropdown), or a hover (tooltip).
public enum PopupTrigger { RightClick, LeftClick, ShiftF10, Hover, AltDown }

// What detection needs: the target, what the popup must be or contain (null: any new popup surface), target's top-level window as the in-window scope, the processes whose popups count, and what was on screen before the trigger.
internal sealed record PopupBaseline(ElementHandle Target, AppSession Session, Locator? Expect, ElementHandle? WindowScope, HashSet<int> ProcessIds, HashSet<string> ShowingBefore, HashSet<IntPtr> WindowsBefore);

// Opens a popup from a target and finds the new surface it opened that is, or contains, an element matching expect — through UIA3, or Win32Provider when UIA3 can't see inside it (a native #32768 menu's HMENU items).
internal static class PopupAction
{
    // Detection looks through UIA3 first, whichever provider found the target: it sees every popup tried (UIA2 sees a #32768 only as an empty Pane).
    private static readonly Uia3Provider Uia3 = new();

    // What a popup itself looks like to the in-window step: without expect, the candidates it looks for; with expect, whether a match is the popup itself or something inside one.
    private static readonly string[] PopupControlTypes = ["Menu", "List", "ToolTip", "Window"];

    // Snapshots what's on screen around target, then fires trigger on it; the returned baseline is what TryDetectAsync diffs against.
    internal static async Task<PopupBaseline> OpenAsync(ElementHandle target, AppSession session, PopupTrigger trigger, Locator? expect, CancellationToken ct = default)
    {
        // Foregrounded before the snapshot, not by the trigger after it: activating a window changes what reads as on screen (a WinForms title bar's System MenuItem appears), which would look new.
        await Task.Run(() => ElementInputHelpers.EnsureForeground(target), ct).ConfigureAwait(false);
        var (windowScope, processIds) = await LocateTargetAsync(target, session, ct).ConfigureAwait(false);
        // Snapshotting first keeps an already-open popup, or a reused pre-loaded one still offscreen, from being mistaken for this trigger's.
        var showingBefore = (await FindOnScreenCandidatesAsync(session, expect, windowScope, ct).ConfigureAwait(false)).Select(e => e.Id).ToHashSet();
        var windowsBefore = (await Task.Run(() => NativeMethods.GetVisibleTopLevelWindows(processIds), ct).ConfigureAwait(false)).ToHashSet();
        await FireTriggerAsync(target, trigger, ct).ConfigureAwait(false);
        return new PopupBaseline(target, session, expect, windowScope, processIds, showingBefore, windowsBefore);
    }

    // One detection pass: a new top-level window that is or contains expect, else a newly showing in-window candidate; null if none yet — the caller owns the polling.
    internal static async Task<ElementHandle?> TryDetectAsync(PopupBaseline baseline, CancellationToken ct = default)
    {
        // Windows first: UIA nests some popup windows' contents under the app window too (a WPF ComboBox's items, a WinForms tooltip), where the in-window step would take the combo or the whole window for the popup.
        var newWindows = (await Task.Run(() => NativeMethods.GetVisibleTopLevelWindows(baseline.ProcessIds), ct).ConfigureAwait(false))
            .Where(w => !baseline.WindowsBefore.Contains(w)).ToList();
        if (await FindNewPopupWindowAsync(baseline, newWindows, ct).ConfigureAwait(false) is { } window)
            return window;
        return await FindNewInWindowSurfaceAsync(baseline, ct).ConfigureAwait(false);
    }

    // ---- Private: locating and triggering ----

    // Resolves where detection should look: target's top-level window wrapped for UIA3 (the in-window scope), and every process whose popups count — the session's plus target's own.
    private static Task<(ElementHandle? WindowScope, HashSet<int> ProcessIds)> LocateTargetAsync(ElementHandle target, AppSession session, CancellationToken ct) => Task.Run(() =>
    {
        // Scoped to target's own window and processes, not just the session's: target may sit in a secondary window, or a process other than the session's.
        var containing = ElementInputHelpers.GetContainingWindow(target);
        var windowScope = Uia3.TryWrapWindow(containing != IntPtr.Zero ? NativeMethods.GetTopLevelWindow(containing) : session.RootWindowHandle);
        var processIds = ElementInputHelpers.GetOwningProcessIds(target);
        processIds.Add(session.ProcessId);
        return (windowScope, processIds);
    }, ct);

    // Fires trigger on target through the existing input actions; Shift+F10 and Alt+Down focus target first, since both open the focused element's popup.
    private static async Task FireTriggerAsync(ElementHandle target, PopupTrigger trigger, CancellationToken ct)
    {
        switch (trigger)
        {
            case PopupTrigger.RightClick:
                await ClickAction.ExecuteCoreAsync(target, MouseButton.Right, KeyModifiers.None, target.Logger, ct).ConfigureAwait(false);
                break;
            case PopupTrigger.LeftClick:
                await ClickAction.ExecuteCoreAsync(target, MouseButton.Left, KeyModifiers.None, target.Logger, ct).ConfigureAwait(false);
                break;
            case PopupTrigger.ShiftF10:
                await SetFocusAction.ExecuteCoreAsync(target, target.Logger, ct).ConfigureAwait(false);
                await KeyboardAction.PressKeyCoreAsync(Key.F10, new KeyModifiers(Shift: true), target.Logger, ct).ConfigureAwait(false);
                break;
            case PopupTrigger.AltDown:
                await SetFocusAction.ExecuteCoreAsync(target, target.Logger, ct).ConfigureAwait(false);
                await KeyboardAction.PressKeyCoreAsync(Key.Down, new KeyModifiers(Alt: true), target.Logger, ct).ConfigureAwait(false);
                break;
            case PopupTrigger.Hover:
                await HoverAction.ExecuteCoreAsync(target, target.Logger, ct).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Unhandled popup trigger.");
        }
    }

    // ---- Private: the two detection steps ----

    // Step 2, the fallback for a popup with no window of its own: a candidate inside target's window that wasn't showing before and is now — the popup is the match itself when it's popup-shaped, else its parent (a MenuItem's menu).
    private static async Task<ElementHandle?> FindNewInWindowSurfaceAsync(PopupBaseline baseline, CancellationToken ct)
    {
        var candidates = await FindOnScreenCandidatesAsync(baseline.Session, baseline.Expect, baseline.WindowScope, ct).ConfigureAwait(false);
        // Re-checked live: a pre-loaded WinUI3 MenuFlyout can flash on screen while another menu closes, then read offscreen a millisecond later.
        var match = await Task.Run(() => candidates.FirstOrDefault(c => !baseline.ShowingBefore.Contains(c.Id) && ElementInputHelpers.IsShowing(c)), ct).ConfigureAwait(false);
        if (match is null)
            return null;

        // A popup-shaped match (always, without expect; with expect, when it names the popup itself, like a ToolTip) is the popup; anything else is inside one (a MenuItem), so step up to its container.
        var surface = match;
        if (!PopupControlTypes.Contains(match.ControlType))
        {
            surface = await match.Provider!.FindParentAsync(match, baseline.Session, ct).ConfigureAwait(false);
            // A popup never contains what opened it: a parent that is target or its ancestor means UIA nested a popup window's contents under the app's own UI (a WPF ComboBox's items, a WinForms tooltip), so leave it to the window step.
            if (surface is null || await IsSelfOrAncestorAsync(surface, baseline.Target, baseline.Session, ct).ConfigureAwait(false))
                return null;
        }

        baseline.Target.Logger?.Info("Popup found in window", new { elementId = baseline.Target.Id, surface.ControlType, via = surface.ResolvedVia });
        return surface;
    }

    // Step 1: the first new top-level window that is or contains expect, through UIA3 and then Win32Provider (a UIA-invisible #32768's items); without expect, the first new showing window that isn't a drop shadow.
    private static async Task<ElementHandle?> FindNewPopupWindowAsync(PopupBaseline baseline, List<IntPtr> newWindows, CancellationToken ct)
    {
        foreach (var window in newWindows)
        {
            if (await TryAsPopupAsync(window, baseline, ct).ConfigureAwait(false) is not { } popup)
                continue;
            baseline.Target.Logger?.Info("Popup found as popup window", new { elementId = baseline.Target.Id, popup = window.ToInt64(), via = popup.ResolvedVia });
            return popup;
        }
        return null;
    }

    // ---- Private: element lookups the detection steps share ----

    // Wraps window as the popup if it qualifies: through UIA3 first, then (with expect) Win32Provider, whose scoped search reads a UIA-invisible #32768's items; null if neither accepts it.
    private static async Task<ElementHandle?> TryAsPopupAsync(IntPtr window, PopupBaseline baseline, CancellationToken ct)
    {
        if (await Task.Run(() => Uia3.TryWrapWindow(window), ct).ConfigureAwait(false) is { } uia3Window && await AcceptsAsync(uia3Window, baseline, window, ct).ConfigureAwait(false))
            return uia3Window;
        if (baseline.Expect is null)
            return null;
        var win32Window = App.Win32.WrapWindowHandle(window);
        return await ContainsAsync(App.Win32, win32Window, baseline.Expect, baseline.Session, ct).ConfigureAwait(false) ? win32Window : null;
    }

    // On-screen candidates inside windowScope (target's top-level window), via UIA3: matches of expect, or without it elements of the popup-shaped control types; empty if UIA3 couldn't resolve the window.
    private static async Task<IEnumerable<ElementHandle>> FindOnScreenCandidatesAsync(AppSession session, Locator? expect, ElementHandle? windowScope, CancellationToken ct)
    {
        if (windowScope is null)
            return [];
        try
        {
            var found = new List<ElementHandle>();
            foreach (var locator in expect is not null ? [expect] : PopupControlTypes.Select(Locator.ByControlType))
                found.AddRange(await Uia3.FindScopedElementsAsync(locator, session, windowScope, ct).ConfigureAwait(false));
            return found.Where(e => e.IsOffscreen != true);
        }
        // The trigger itself can close target's window (a "⋯" button in a dialog that dismisses); its popups are still found by the window diff.
        catch (StaleElementError) { return []; }
    }

    // Whether a new UIA3-wrapped window counts as the popup: it is or contains expect, or without expect it's showing and isn't a drop-shadow companion.
    private static async Task<bool> AcceptsAsync(ElementHandle wrapped, PopupBaseline baseline, IntPtr window, CancellationToken ct)
    {
        try
        {
            if (baseline.Expect is { } expect)
            {
                if (MatchesItself(wrapped, expect))
                    return true;
                return await ContainsAsync(Uia3, wrapped, expect, baseline.Session, ct).ConfigureAwait(false);
            }

            // Without expect, take any showing window except the drop shadow some popups draw as a separate window.
            if (NativeMethods.GetWindowClassName(window) == "SysShadow")
                return false;
            return await Task.Run(() => ElementInputHelpers.IsShowing(wrapped), ct).ConfigureAwait(false);
        }
        // Popups come with short-lived companions (e.g. a SysShadow) that can close between enumeration and search.
        catch (StaleElementError) { return false; }
    }

    // True when element is target or one of its ancestors, walking up through target's provider; always false when the two came from different providers, whose Ids never match.
    private static async Task<bool> IsSelfOrAncestorAsync(ElementHandle element, ElementHandle target, AppSession session, CancellationToken ct)
    {
        for (var current = target; current is not null; current = await current.Provider!.FindParentAsync(current, session, ct).ConfigureAwait(false))
            if (current.Id == element.Id)
                return true;
        return false;
    }

    // True when provider finds an element matching locator under scope; a gone scope counts as no.
    private static async Task<bool> ContainsAsync(IElementProvider provider, ElementHandle scope, Locator locator, AppSession session, CancellationToken ct)
    {
        try { return await provider.FindScopedElementAsync(locator, session, scope, ct).ConfigureAwait(false) is not null; }
        catch (StaleElementError) { return false; }
    }

    // True when the window element itself matches locator by the handle's own properties (a tooltip window that is the ToolTip expect names); only the simple property strategies can match this way.
    private static bool MatchesItself(ElementHandle element, Locator locator) => locator.Strategy switch
    {
        LocatorStrategy.ControlType => element.ControlType == locator.Value,
        LocatorStrategy.Name => element.Name == locator.Value,
        LocatorStrategy.AutomationId => element.AutomationId == locator.Value,
        LocatorStrategy.ClassName => element.ClassName == locator.Value,
        _ => false,
    };
}
