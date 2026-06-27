// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Simulates click-and-drag mouse gestures by holding the left button while moving through waypoints.
public static class DragAction
{
    // Drags through the given physical-screen waypoints in one continuous press; minimum 2 points required.
    public static Task DragThroughAsync(IReadOnlyList<(int X, int Y)> waypoints, CancellationToken ct = default)
    {
        if (waypoints.Count < 2) throw new ArgumentException("At least 2 waypoints required.", nameof(waypoints));
        return Task.Run(() =>
        {
            // Move to the start without pressing so the cursor is in the right place before the button goes down.
            var (nx0, ny0) = Normalize(waypoints[0].X, waypoints[0].Y);
            Send(MouseInput(nx0, ny0, NativeMethods.MouseEventMove | NativeMethods.MouseEventAbsolute));
            Thread.Sleep(30);

            Send(MouseInput(nx0, ny0, NativeMethods.MouseEventLeftDown | NativeMethods.MouseEventAbsolute));
            Thread.Sleep(20);

            foreach (var (wx, wy) in waypoints)
            {
                var (nx, ny) = Normalize(wx, wy);
                Send(MouseInput(nx, ny, NativeMethods.MouseEventMove | NativeMethods.MouseEventAbsolute));
                Thread.Sleep(5);
            }

            var last = waypoints[waypoints.Count - 1];
            var (nxL, nyL) = Normalize(last.X, last.Y);
            Send(MouseInput(nxL, nyL, NativeMethods.MouseEventLeftUp | NativeMethods.MouseEventAbsolute));
        }, ct);
    }

    // Drags from (fromX, fromY) to (toX, toY) in a straight line with the given number of interpolated steps.
    public static Task DragAsync(int fromX, int fromY, int toX, int toY, int steps = 20, CancellationToken ct = default)
    {
        var waypoints = Enumerable.Range(0, steps + 1)
            .Select(i => (
                X: (int)(fromX + (toX - fromX) * (double)i / steps),
                Y: (int)(fromY + (toY - fromY) * (double)i / steps)))
            .ToList();
        return DragThroughAsync(waypoints, ct);
    }

    // Sends a single mouse INPUT event.
    private static void Send(NativeMethods.INPUT input) =>
        NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.INPUT>());

    // Builds an absolute-positioned mouse INPUT event.
    private static NativeMethods.INPUT MouseInput(int nx, int ny, uint flags) => new()
    {
        Type = NativeMethods.InputTypeMouse,
        Data = new NativeMethods.InputUnion
        {
            Mouse = new NativeMethods.MOUSEINPUT { Dx = nx, Dy = ny, Flags = flags },
        },
    };

    // Normalizes a physical screen point to SendInput's 0–65535 absolute coordinate space.
    private static (int X, int Y) Normalize(int x, int y)
    {
        var w = NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen);
        var h = NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen);
        return ((int)(x * 65536L / w), (int)(y * 65536L / h));
    }
}
