// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
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
            var (nx0, ny0) = ClickAction.Normalize(waypoints[0].X, waypoints[0].Y);
            NativeMethods.SendInputs(ClickAction.MouseInputAt(nx0, ny0, NativeMethods.MouseEventMove));
            Thread.Sleep(30);

            NativeMethods.SendInputs(ClickAction.MouseInputAt(nx0, ny0, NativeMethods.MouseEventLeftDown));
            Thread.Sleep(20);

            foreach (var (wx, wy) in waypoints)
            {
                var (nx, ny) = ClickAction.Normalize(wx, wy);
                NativeMethods.SendInputs(ClickAction.MouseInputAt(nx, ny, NativeMethods.MouseEventMove));
                Thread.Sleep(5);
            }

            var last = waypoints[waypoints.Count - 1];
            var (nxL, nyL) = ClickAction.Normalize(last.X, last.Y);
            NativeMethods.SendInputs(ClickAction.MouseInputAt(nxL, nyL, NativeMethods.MouseEventLeftUp));
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
}
