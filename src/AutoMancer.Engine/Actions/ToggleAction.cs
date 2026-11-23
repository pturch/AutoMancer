// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Automation;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Cycles a resolved element's toggle state via TogglePattern.Toggle(); no-op when the pattern is unavailable.
public static class ToggleAction
{
    // Toggles the element; no-op if TogglePattern is not supported by this element.
    public static Task ExecuteAsync(ElementHandle element, CancellationToken ct = default)
        => ExecuteCoreAsync(element, element.Logger, ct);

    // Same as ExecuteAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task ExecuteCoreAsync(ElementHandle element, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        // Both the pattern lookup and Toggle are live UIA calls that fail if the element has left the tree; surface that as StaleElementError, let any other failure propagate as-is.
        try
        {
            Action? toggle = element.NativeHandle switch
            {
                IUIAutomationElement uia3 => uia3.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId) is IUIAutomationTogglePattern pattern ? pattern.Toggle : null,
                AutomationElement uia2 => uia2.TryGetCurrentPattern(TogglePattern.Pattern, out var pattern) ? ((TogglePattern)pattern).Toggle : null,
                _ => null, // Win32 handles have no TogglePattern.
            };
            if (toggle is null)
            {
                logger?.Info("TogglePattern not supported, nothing toggled", new { elementId = element.Id, resolvedVia = element.ResolvedVia });
                return;
            }
            toggle();
        }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, element))
        {
            throw ElementInputHelpers.Stale(element, ex);
        }
        logger?.Info("Toggled via TogglePattern", new { elementId = element.Id });
    }, ct);
}
