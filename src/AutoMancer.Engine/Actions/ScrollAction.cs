// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Automation;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Scrolls a resolved element into view via ScrollItemPattern.ScrollIntoView(); no-op when the pattern is unavailable.
public static class ScrollAction
{
    // Scrolls the element into view; no-op if ScrollItemPattern is not supported by this element.
    public static Task ExecuteAsync(ElementHandle element, CancellationToken ct = default)
        => ExecuteCoreAsync(element, element.Logger, ct);

    // Same as ExecuteAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task ExecuteCoreAsync(ElementHandle element, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        // Both the pattern lookup and ScrollIntoView are live UIA calls that fail if the element has left the tree; surface that as StaleElementError, let any other failure propagate as-is.
        try
        {
            Action? scrollIntoView = element.NativeHandle switch
            {
                IUIAutomationElement uia3 => uia3.GetCurrentPattern(UIA_PatternIds.UIA_ScrollItemPatternId) is IUIAutomationScrollItemPattern pattern ? pattern.ScrollIntoView : null,
                AutomationElement uia2 => uia2.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var pattern) ? ((ScrollItemPattern)pattern).ScrollIntoView : null,
                _ => null, // Win32 handles have no ScrollItemPattern.
            };
            if (scrollIntoView is null)
            {
                // Info, not Warn: plenty of elements aren't in a scrollable container, and FindByScrollingAsync calls this on every item it finds.
                logger?.Info("ScrollItemPattern not supported, nothing scrolled", new { elementId = element.Id, resolvedVia = element.ResolvedVia });
                return;
            }
            // A detached element (e.g. from a closed XAML dialog) accepts ScrollIntoView as a silent no-op, so check before reporting success.
            if (ElementInputHelpers.IsDetached(element))
                throw ElementInputHelpers.Stale(element, null);
            scrollIntoView();
        }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, element))
        {
            throw ElementInputHelpers.Stale(element, ex);
        }
        logger?.Info("Scrolled into view via ScrollItemPattern", new { elementId = element.Id });
    }, ct);
}
