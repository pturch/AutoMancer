// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Moves the mouse cursor to a resolved element's center without pressing any button, to trigger hover states/tooltips.
public static class HoverAction
{
    // Sends a single absolute MOUSEEVENTF_MOVE to the element's center; no button events.
    public static Task ExecuteAsync(ElementHandle element, CancellationToken ct = default) => Task.Run(() =>
    {
        ClickAction.EnsureForeground(element);

        var (x, y) = ClickAction.GetCenter(element);
        var input = ClickAction.MouseInput(x, y, NativeMethods.MouseEventMove);
        NativeMethods.SendInputs(input);
    }, ct);
}
