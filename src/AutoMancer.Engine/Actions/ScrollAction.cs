// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
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
        if (element.NativeHandle is not IUIAutomationElement uiaElement)
            return;
        // Both GetCurrentPattern and ScrollIntoView are live UIA calls that fail if the element has left the tree; surface that as StaleElementError, let any other failure propagate as-is.
        try
        {
            if (uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_ScrollItemPatternId) is not IUIAutomationScrollItemPattern scrollItem)
                return;
            // A detached element (e.g. from a closed XAML dialog) accepts ScrollIntoView as a silent no-op, so check before reporting success.
            if (ElementInputHelpers.IsDetached(element))
                throw ElementInputHelpers.Stale(element, null);
            scrollItem.ScrollIntoView();
        }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, element))
        {
            throw ElementInputHelpers.Stale(element, ex);
        }
        logger?.Info("Scrolled into view via ScrollItemPattern", new { elementId = element.Id });
    }, ct);
}
