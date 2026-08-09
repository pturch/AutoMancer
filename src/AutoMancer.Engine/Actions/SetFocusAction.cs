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
        ClickAction.EnsureForeground(element);
        uiaElement.SetFocus();
        logger?.Info("Focused via SetFocus", new { elementId = element.Id });
    }, ct);
}
