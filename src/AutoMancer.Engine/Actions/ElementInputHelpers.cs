// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Pre-flight checks and coordinate lookups shared by every action that targets a resolved element, via native pattern or synthesized SendInput.
internal static class ElementInputHelpers
{
    // UIA's fixed HRESULT for "element no longer in the tree"; unchecked because HRESULTs are unsigned but Exception.HResult is a signed int.
    private const int UIA_E_ELEMENTNOTAVAILABLE = unchecked((int)0x80040201);

    // COM's generic "unexpected failure"; UIA briefly returns it for ~30ms after an element's process exits, before switching to UIA_E_ELEMENTNOTAVAILABLE.
    private const int E_UNEXPECTED = unchecked((int)0x8000FFFF);

    // True when a live read's failure means element has left the UI tree: UIA_E_ELEMENTNOTAVAILABLE (UIA3's COMException, UIA2's ElementNotAvailableException), or E_UNEXPECTED once its process has exited — on a live process E_UNEXPECTED stays an ordinary failure.
    internal static bool IsStaleFailure(Exception ex, ElementHandle element) =>
        ex is ElementNotAvailableException
        || (ex is COMException && (ex.HResult == UIA_E_ELEMENTNOTAVAILABLE || (ex.HResult == E_UNEXPECTED && HasExited(element.ProcessId))));

    // True when the process with this id is gone; 0 (unknown, e.g. Win32 handles) never counts as exited.
    private static bool HasExited(int processId)
    {
        if (processId == 0) return false;
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited;
        }
        catch (ArgumentException) { return true; } // no process with this id is running any more
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; } // e.g. access denied to an elevated process — unknown counts as alive
    }

    // True when element still answers reads but has been detached from the UI tree — a closed XAML dialog's elements never throw, they just read back blank; every attached element has at least the desktop as its raw-view parent. A failed parent read isn't treated as detached.
    internal static bool IsDetached(ElementHandle element)
    {
        try
        {
            return element.NativeHandle switch
            {
                IUIAutomationElement uia3 => Uia3Provider.Automation.RawViewWalker.GetParentElement(uia3) is null,
                AutomationElement uia2 => TreeWalker.RawViewWalker.GetParent(uia2) is null,
                _ => false,
            };
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ElementNotAvailableException) { return false; }
    }

    // Builds the StaleElementError every stale-detection site raises for element; label names its role in the message (e.g. "Scope element").
    internal static StaleElementError Stale(ElementHandle element, Exception? inner, string label = "Element") =>
        new(element, $"{label} \"{element.Name}\" (AutomationId={element.AutomationId}) is no longer in the UI tree.", inner);

    // The scoped-search stale check: UIA's FindFirst/FindAll on a dead scope return null/empty instead of throwing, so after a null/empty result or a failed search, a live read decides whether scope itself is gone; no-op for a null (unscoped) or live scope.
    internal static void ThrowIfScopeGone(ElementHandle? scope, Exception? searchFailure)
    {
        if (scope is null) return;
        try
        {
            if (scope.NativeHandle is IUIAutomationElement uia3) _ = uia3.CurrentNativeWindowHandle;
            else if (scope.NativeHandle is AutomationElement uia2) _ = uia2.Current.NativeWindowHandle;
            else return; // Win32 hwnds are checked before the search instead (Win32Provider.ValidScopeHandle), since a recycled hwnd returns another window's matches, not empty.
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ElementNotAvailableException)
        {
            // Any other failure of the probe itself counts as not gone.
            if (IsStaleFailure(ex, scope)) throw Stale(scope, searchFailure, "Scope element");
            return;
        }
        if (IsDetached(scope)) throw Stale(scope, searchFailure, "Scope element");
    }

    // An element's IsEnabled/IsOffscreen/bounds as read live at action time.
    private readonly record struct ElementState(bool? IsEnabled, bool? IsOffscreen, Rect Bounds);

    // Reads the element's state live — one batched UIA call (a cache request), or user32 reads for a Win32 hwnd; throws StaleElementError if it's gone. Handles with nothing live to read, and a read that fails without the element being gone, get the find-time values.
    private static ElementState ReadState(ElementHandle element)
    {
        // Stale always throws here — AppOptions.ReresolveOnStale only controls whether App retries it, not whether it's raised.
        try
        {
            switch (element.NativeHandle)
            {
                case IUIAutomationElement uia3:
                    var request = Uia3Provider.Automation.CreateCacheRequest();
                    // Raw view, so an element outside the default control view (one the raw-view walkers can reach) still gets its own state cached.
                    request.TreeFilter = Uia3Provider.Automation.RawViewCondition;
                    request.AddProperty(UIA_PropertyIds.UIA_BoundingRectanglePropertyId);
                    request.AddProperty(UIA_PropertyIds.UIA_IsEnabledPropertyId);
                    request.AddProperty(UIA_PropertyIds.UIA_IsOffscreenPropertyId);
                    var uia3State = uia3.BuildUpdatedCache(request);
                    var r = uia3State.CachedBoundingRectangle;
                    return new(uia3State.CachedIsEnabled != 0, uia3State.CachedIsOffscreen != 0, new Rect(r.left, r.top, r.right - r.left, r.bottom - r.top));
                case AutomationElement uia2:
                    var request2 = new CacheRequest { TreeFilter = Automation.RawViewCondition };
                    request2.Add(AutomationElement.BoundingRectangleProperty);
                    request2.Add(AutomationElement.IsEnabledProperty);
                    request2.Add(AutomationElement.IsOffscreenProperty);
                    var uia2State = uia2.GetUpdatedCache(request2).Cached;
                    var b = uia2State.BoundingRectangle;
                    return new(uia2State.IsEnabled, uia2State.IsOffscreen, new Rect(b.X, b.Y, b.Width, b.Height));
                case IntPtr windowHandle:
                    // Plain user32 calls with no cross-process marshaling, so no batching is needed; a closed window's failed rect reads as not visible, as Win32Provider reports it.
                    var (rect, rectOk) = Win32Provider.GetRect(windowHandle);
                    return new(NativeMethods.IsWindowEnabled(windowHandle), !rectOk || !NativeMethods.IsWindowVisible(windowHandle), rect);
            }
        }
        catch (Exception ex) when (IsStaleFailure(ex, element))
        {
            throw Stale(element, ex);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // Some UIA elements (e.g. split-button MenuItems observed against VLC) throw or time out on reads even though they're still present; log and act on the find-time values rather than failing the action.
            element.Logger?.Warn("Live state read failed, using find-time state", new { elementId = element.Id, error = ex.Message });
        }
        return new(element.IsEnabled, element.IsOffscreen, element.BoundingRect);
    }

    // Brings the element's owning window to the foreground so synthesized input reaches it; throws StaleElementError if the element is gone.
    internal static void EnsureForeground(ElementHandle element)
    {
        IntPtr windowHandle;
        // Stale always throws here — AppOptions.ReresolveOnStale only controls whether App retries it, not whether it's raised.
        if (element.NativeHandle is IUIAutomationElement uiaElement)
        {
            try
            {
                windowHandle = uiaElement.CurrentNativeWindowHandle;
            }
            catch (Exception ex) when (IsStaleFailure(ex, element))
            {
                throw Stale(element, ex);
            }
            catch (COMException ex)
            {
                // Some UIA elements (e.g. split-button MenuItems observed against VLC) throw or time out here even though they're still present; log and move on rather than failing the click.
                element.Logger?.Warn("CurrentNativeWindowHandle failed", new { elementId = element.Id, error = ex.Message });
                return;
            }
        }
        else if (element.NativeHandle is AutomationElement managedElement)
        {
            // uia2's live read is what catches a gone element here — without it a stale handle falls through to a SendInput click at its old, cached BoundingRect.
            try
            {
                windowHandle = new IntPtr(managedElement.Current.NativeWindowHandle);
            }
            catch (Exception ex) when (IsStaleFailure(ex, element))
            {
                throw Stale(element, ex);
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or ElementNotAvailableException)
            {
                // Same tolerance as uia3 above for an element that fails this one read without being gone.
                element.Logger?.Warn("Current.NativeWindowHandle failed", new { elementId = element.Id, error = ex.Message });
                return;
            }
        }
        else
            return; // Non-UIA handles have no owning window to activate.

        // A successful read isn't proof of life: a closed XAML dialog's elements keep answering, detached, and a click would land wherever the element used to be.
        if (IsDetached(element))
            throw Stale(element, null);

        if (windowHandle != IntPtr.Zero && element.ForegroundActivationTimeoutMs > 0)
            NativeMethods.EnsureForegroundOrThrow(windowHandle, element.ForegroundActivationTimeoutMs);
    }

    // Throws ElementNotInteractableError unless the element is enabled and visible right now per WaitConditions' shared rules, or StaleElementError if it's gone; the guard every input action runs before any native-pattern or synthesized input.
    internal static void EnsureInteractable(ElementHandle element)
    {
        var state = ReadState(element);
        if (WaitConditions.Enabled(state.IsEnabled) && WaitConditions.Visible(state.IsOffscreen, state.Bounds))
            return;

        // A detached element (closed XAML dialog) reads back blank instead of throwing — report it as stale, not merely hidden.
        if (IsDetached(element))
            throw Stale(element, null);

        // Name the specific check that failed, so the error says why the element can't be used.
        string reason;
        if (!WaitConditions.Enabled(state.IsEnabled))
            reason = "is disabled (IsEnabled=false)";
        else if (state.IsOffscreen == true)
            reason = "is currently offscreen (IsOffscreen=true)";
        else
            reason = "has empty on-screen bounds";
        throw new ElementNotInteractableError(element, $"Element \"{element.Name}\" (AutomationId={element.AutomationId}) {reason} and cannot be interacted with.");
    }

    // The point synthesized pointer input should hit: the center of the element's live bounds (already physical pixels; see DpiAwareness). Independent of EnsureInteractable — it validates its own read, throwing ElementNotInteractableError when there's no on-screen area to aim at.
    internal static (int X, int Y) GetCenter(ElementHandle element)
    {
        var bounds = ReadState(element).Bounds;
        if (!(bounds.Width > 0 && bounds.Height > 0))
            throw new ElementNotInteractableError(element, $"Element \"{element.Name}\" (AutomationId={element.AutomationId}) has empty on-screen bounds, so there is no point to aim synthesized input at.");
        var (x, y) = bounds.Center;
        return ((int)x, (int)y);
    }
}
