// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Double-clicks an element or a screen point: a move, a short hover pause, then both clicks sent as one atomic SendInput batch.
public static class DoubleClickAction
{
    // No operator fast path — InvokePattern has no double-click equivalent, so this always goes through SendInput.

    // Lets the pointer register over the target before the press, which WinUI3 hit-testing needs.
    private const int HoverSettleMs = 60;

    // Double-clicks the element's center via SendInput; logs via whatever logger the element was resolved with.
    public static Task ExecuteAsync(ElementHandle element, CancellationToken ct = default)
        => ExecuteCoreAsync(element, element.Logger, ct);

    // Same as ExecuteAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task ExecuteCoreAsync(ElementHandle element, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        ElementInputHelpers.EnsureInteractable(element);
        ElementInputHelpers.EnsureForeground(element);
        var (x, y) = ElementInputHelpers.GetCenter(element);
        ExecuteAt(x, y, logger);
    }, ct);

    // Double-clicks a physical screen point; both presses go in one batch because newer Notepad only recognizes a double-click when the presses are nearly simultaneous, and a sleep between them is too long and jittery.
    internal static void ExecuteAt(int x, int y, IEngineLogger? logger)
    {
        var (normX, normY) = SendInputBuilders.Normalize(x, y);
        NativeMethods.SendInputs([SendInputBuilders.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.Move)], logger);
        Thread.Sleep(HoverSettleMs);
        NativeMethods.INPUT[] doubleClick =
        [
            SendInputBuilders.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.LeftDown),
            SendInputBuilders.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.LeftUp),
            SendInputBuilders.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.LeftDown),
            SendInputBuilders.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.LeftUp),
        ];
        NativeMethods.SendInputs(doubleClick, logger);
    }
}
