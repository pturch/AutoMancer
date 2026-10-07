// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Pre-flight checks and coordinate lookups shared by every action that falls back to synthesized SendInput against a resolved element.
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

    // Throws ElementNotInteractableError when IsEnabled/IsOffscreen say a synthesized action can't land meaningfully; shared by every action that falls back to SendInput (click, type, clear), not just click.
    internal static void EnsureInteractable(ElementHandle element)
    {
        if (element.IsEnabled == false)
            throw new ElementNotInteractableError(element, $"Element \"{element.Name}\" (AutomationId={element.AutomationId}) is disabled (IsEnabled=false) and cannot be interacted with.");
        if (element.IsOffscreen == true)
            throw new ElementNotInteractableError(element, $"Element \"{element.Name}\" (AutomationId={element.AutomationId}) is currently offscreen (IsOffscreen=true) and cannot be interacted with.");
    }

    // Computes the absolute physical-screen point at the center of the element's bounding rect (already physical pixels; see DpiAwareness)
    internal static (int X, int Y) GetCenter(ElementHandle element)
    {
        EnsureInteractable(element);
        var (x, y) = element.BoundingRect.Center;
        return ((int)x, (int)y);
    }
}
