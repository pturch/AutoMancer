// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Double-clicks a resolved element via two SendInput clicks with a short gap between them.
public static class DoubleClickAction
{
    // No operator fast path — InvokePattern has no double-click equivalent, so this always goes through SendInput.

    // Real gap between presses — instantaneous double-clicks aren't reliably recognized (WinUI3's gesture recognizer included).
    private const int BetweenClicksDelayMs = 50;

    // Builds one move+down+up click batch, then sends it twice with a gap in between; logs via whatever logger the element was resolved with.
    public static Task ExecuteAsync(ElementHandle element, CancellationToken ct = default)
        => ExecuteCoreAsync(element, element.Logger, ct);

    // Same as ExecuteAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task ExecuteCoreAsync(ElementHandle element, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        ClickAction.EnsureForeground(element);

        // Leading move required — see NativeMethods.SendMouseClick's comment on WinUI3 hit-testing.
        var (x, y) = ClickAction.GetCenter(element);
        var (normX, normY) = ClickAction.Normalize(x, y);
        NativeMethods.INPUT[] click =
        [
            ClickAction.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.Move),
            ClickAction.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.LeftDown),
            ClickAction.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.LeftUp),
        ];

        NativeMethods.SendInputs(click, logger);
        Thread.Sleep(BetweenClicksDelayMs);
        NativeMethods.SendInputs(click, logger);
    }, ct);
}
