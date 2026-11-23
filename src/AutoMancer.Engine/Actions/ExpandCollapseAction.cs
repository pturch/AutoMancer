// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Automation;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Expands or collapses a resolved element via ExpandCollapsePattern; no-op when the pattern is unavailable.
public static class ExpandCollapseAction
{
    // Expands the element; no-op if ExpandCollapsePattern is not supported by this element.
    public static Task ExpandAsync(ElementHandle element, CancellationToken ct = default)
        => RunAsync(element, expand: true, element.Logger, ct);

    // Collapses the element; no-op if ExpandCollapsePattern is not supported by this element.
    public static Task CollapseAsync(ElementHandle element, CancellationToken ct = default)
        => RunAsync(element, expand: false, element.Logger, ct);

    // Looks up ExpandCollapsePattern and calls Expand or Collapse, logging a no-op when the pattern is missing.
    private static Task RunAsync(ElementHandle element, bool expand, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        var done = expand ? "expanded" : "collapsed";
        // Both the pattern lookup and the call are live UIA calls that fail if the element has left the tree; surface that as StaleElementError, let any other failure propagate as-is.
        try
        {
            Action? invoke = element.NativeHandle switch
            {
                IUIAutomationElement uia3 => uia3.GetCurrentPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId) is IUIAutomationExpandCollapsePattern pattern
                    ? (expand ? pattern.Expand : pattern.Collapse) : null,
                AutomationElement uia2 => uia2.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var pattern)
                    ? (expand ? ((ExpandCollapsePattern)pattern).Expand : ((ExpandCollapsePattern)pattern).Collapse) : null,
                _ => null, // Win32 handles have no ExpandCollapsePattern.
            };
            if (invoke is null)
            {
                logger?.Info($"ExpandCollapsePattern not supported, nothing {done}", new { elementId = element.Id, resolvedVia = element.ResolvedVia });
                return;
            }
            invoke();
        }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, element))
        {
            throw ElementInputHelpers.Stale(element, ex);
        }
        logger?.Info($"{char.ToUpperInvariant(done[0])}{done[1..]} via ExpandCollapsePattern", new { elementId = element.Id });
    }, ct);
}
