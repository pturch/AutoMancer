// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Moves the mouse cursor to a resolved element's center without pressing any button, to trigger hover states/tooltips.
public static class HoverAction
{
    // Sends a single absolute MOUSEEVENTF_MOVE to the element's center; logs via whatever logger the element was resolved with.
    public static Task ExecuteAsync(ElementHandle element, CancellationToken ct = default)
        => ExecuteCoreAsync(element, element.Logger, ct);

    // Same as ExecuteAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task ExecuteCoreAsync(ElementHandle element, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        ElementInputHelpers.EnsureForeground(element);

        var (x, y) = ElementInputHelpers.GetCenter(element);
        var input = SendInputBuilders.MouseInput(x, y, NativeMethods.MouseEventFlags.Move);
        NativeMethods.SendInputs([input], logger);
    }, ct);
}
