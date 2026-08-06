// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Moves the mouse by a relative pixel offset via SendInput — no MouseEventAbsolute — for repositioning the cursor from wherever it currently is, rather than to a screen coordinate.
public static class MouseMoveAction
{
    // Moves the cursor by (dx, dy) pixels from its current position.
    public static Task MoveRelativeAsync(int dx, int dy, CancellationToken ct = default) => Task.Run(() =>
        NativeMethods.SendInputs(RelativeMoveInput(dx, dy)), ct);

    // Builds a relative-move mouse INPUT event; omits MouseEventAbsolute so Dx/Dy are read as a delta rather than a normalized screen coordinate.
    internal static NativeMethods.INPUT RelativeMoveInput(int dx, int dy) => new()
    {
        Type = NativeMethods.InputTypeMouse,
        Data = new NativeMethods.InputUnion
        {
            Mouse = new NativeMethods.MOUSEINPUT { Dx = dx, Dy = dy, Flags = NativeMethods.MouseEventMove },
        },
    };
}
