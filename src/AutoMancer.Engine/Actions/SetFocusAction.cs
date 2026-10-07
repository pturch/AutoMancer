// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Sets keyboard focus to a resolved element via IUIAutomationElement.SetFocus(); no-op when the handle isn't a UIA element.
public static class SetFocusAction
{
    // Sets focus to the element; no-op if this handle wasn't resolved by a UIA provider.
    public static Task ExecuteAsync(ElementHandle element, CancellationToken ct = default)
        => ExecuteCoreAsync(element, element.Logger, ct);

    // Same as ExecuteAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task ExecuteCoreAsync(ElementHandle element, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        if (element.NativeHandle is not IUIAutomationElement uiaElement)
            return;
        ElementInputHelpers.EnsureForeground(element);
        // SetFocus is a live UIA call that fails if the element has left the tree; surface that as StaleElementError, let any other failure propagate as-is.
        try
        {
            uiaElement.SetFocus();
        }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, element))
        {
            throw ElementInputHelpers.Stale(element, ex);
        }
        logger?.Info("Focused via SetFocus", new { elementId = element.Id });
    }, ct);
}
