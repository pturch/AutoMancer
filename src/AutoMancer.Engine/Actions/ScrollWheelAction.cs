// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Scrolls under a resolved element: moves the cursor to its center (so hit-testing routes the wheel event correctly), then sends deltaY/deltaX notches via MOUSEEVENTF_WHEEL/HWHEEL; either delta may be zero.
public static class ScrollWheelAction
{
    // Executes the wheel scroll; logs via whatever logger the element was resolved with (see ElementHandle.Logger).
    public static Task ExecuteAsync(ElementHandle element, int deltaX, int deltaY, CancellationToken ct = default)
        => ExecuteCoreAsync(element, deltaX, deltaY, element.Logger, ct);

    // Same as ExecuteAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task ExecuteCoreAsync(ElementHandle element, int deltaX, int deltaY, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        ClickAction.EnsureForeground(element);

        var (x, y) = ClickAction.GetCenter(element);
        var (normX, normY) = ClickAction.Normalize(x, y);
        var inputs = new List<NativeMethods.INPUT>
        {
            ClickAction.MouseInputAt(normX, normY, NativeMethods.MouseEventMove),
        };

        if (deltaY != 0)
            inputs.Add(ClickAction.MouseInputAt(normX, normY, NativeMethods.MouseEventWheel, unchecked((uint)(deltaY * NativeMethods.WheelDelta))));
        if (deltaX != 0)
            inputs.Add(ClickAction.MouseInputAt(normX, normY, NativeMethods.MouseEventHWheel, unchecked((uint)(deltaX * NativeMethods.WheelDelta))));

        NativeMethods.SendInputs(inputs.ToArray(), logger);
    }, ct);
}
