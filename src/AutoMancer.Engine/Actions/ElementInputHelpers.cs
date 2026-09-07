// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Pre-flight checks and coordinate lookups shared by every action that falls back to synthesized SendInput against a resolved element.
internal static class ElementInputHelpers
{
    // Brings the element's owning window to the foreground so synthesized input reaches it; no-op for non-UIA handles.
    internal static void EnsureForeground(ElementHandle element)
    {
        if (element.NativeHandle is not IUIAutomationElement uiaElement)
            return;

        IntPtr windowHandle;
        try
        {
            windowHandle = uiaElement.CurrentNativeWindowHandle;
        }
        catch (COMException ex)
        {
            // Some UIA elements (e.g. split-button MenuItems observed against VLC) throw or time out here; log and move on rather than failing the click.
            element.Logger?.Warn("CurrentNativeWindowHandle failed", new { elementId = element.Id, error = ex.Message });
            return;
        }

        if (windowHandle != IntPtr.Zero)
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
