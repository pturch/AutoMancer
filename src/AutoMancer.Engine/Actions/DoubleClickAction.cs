// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Double-clicks a resolved element via two SendInput clicks with a short gap between them.
public static class DoubleClickAction
{
    // No operator fast path — InvokePattern has no double-click equivalent, so this always goes through SendInput.

    // Real gap between presses — instantaneous double-clicks aren't reliably recognized (WinUI3's gesture recognizer included).
    private const int BetweenClicksDelayMs = 50;

    // Builds one move+down+up click batch, then sends it twice with a gap in between.
    public static Task ExecuteAsync(ElementHandle element, CancellationToken ct = default) => Task.Run(() =>
    {
        ClickAction.EnsureForeground(element);

        // Leading move required — see NativeMethods.SendMouseClick's comment on WinUI3 hit-testing.
        var (x, y) = ClickAction.GetCenter(element);
        var (normX, normY) = ClickAction.Normalize(x, y);
        NativeMethods.INPUT[] click =
        [
            ClickAction.MouseInputAt(normX, normY, NativeMethods.MouseEventMove),
            ClickAction.MouseInputAt(normX, normY, NativeMethods.MouseEventLeftDown),
            ClickAction.MouseInputAt(normX, normY, NativeMethods.MouseEventLeftUp),
        ];

        NativeMethods.SendInputs(click);
        Thread.Sleep(BetweenClicksDelayMs);
        NativeMethods.SendInputs(click);
    }, ct);
}
