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
        ElementInputHelpers.EnsureForeground(element);

        var (x, y) = ElementInputHelpers.GetCenter(element);
        var (normX, normY) = SendInputBuilders.Normalize(x, y);
        var inputs = new List<NativeMethods.INPUT>
        {
            SendInputBuilders.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.Move),
        };

        if (deltaY != 0)
            inputs.Add(SendInputBuilders.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.Wheel, unchecked((uint)(deltaY * NativeMethods.WheelDelta))));
        if (deltaX != 0)
            inputs.Add(SendInputBuilders.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.HWheel, unchecked((uint)(deltaX * NativeMethods.WheelDelta))));

        NativeMethods.SendInputs(inputs.ToArray(), logger);
    }, ct);
}
