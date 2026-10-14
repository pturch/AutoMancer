// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Automation;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Sets keyboard focus to a resolved element via UIA's SetFocus (UIA3 or UIA2) or Win32's; no-op for non-UIA stand-ins.
public static class SetFocusAction
{
    // Sets focus to the element; no-op for non-UIA stand-ins with nothing to focus.
    public static Task ExecuteAsync(ElementHandle element, CancellationToken ct = default)
        => ExecuteCoreAsync(element, element.Logger, ct);

    // Same as ExecuteAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task ExecuteCoreAsync(ElementHandle element, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        Action? setFocus = element.NativeHandle switch
        {
            IUIAutomationElement uia3 => uia3.SetFocus,
            AutomationElement uia2 => uia2.SetFocus,
            IntPtr win32 => () => ElementInputHelpers.SetWin32Focus(element, win32),
            _ => null, // Non-UIA stand-ins have nothing to focus.
        };
        if (setFocus is null)
        {
            logger?.Info("Nothing to focus, focus unchanged", new { elementId = element.Id });
            return;
        }
        ElementInputHelpers.EnsureForeground(element);
        // SetFocus is a live UIA call that fails if the element has left the tree; surface that as StaleElementError, let any other failure propagate as-is.
        try
        {
            setFocus();
        }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, element))
        {
            throw ElementInputHelpers.Stale(element, ex);
        }
        logger?.Info("Focused via SetFocus", new { elementId = element.Id });
    }, ct);
}
