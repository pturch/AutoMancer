// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using Xunit.Abstractions;

namespace AutoMancer.Engine.Tests.Integration;

// End-to-end workflow exercising pencil drawing of overlapping rounded shapes, fill-bucket coloring, and text-tool TypeAsync.
[Collection("Paint")]
[Trait("Category", "Integration")]
public sealed class PaintFrogTests(ITestOutputHelper output) : PaintArtTestBase
{
    // Draws a cute green frog (body, two eye-bumps with white eyes and black pupils, a grin, front legs, and blush) and saves it as a PNG.
    [Fact]
    public async Task DrawFrog_SavesToPng_CanBeViewedManually()
    {
        var canvas = await _app.FindAsync(Locator.ByAutomationId("image"));
        var rect   = canvas.BoundingRect;
        var cx     = (int)(rect.X + rect.Width  / 2);
        var cy     = (int)(rect.Y + rect.Height / 2);
        var r      = (int)(Math.Min(rect.Width, rect.Height) * 0.30);

        // ── Geometry ────────────────────────────────────────────────────────
        var bodyCenter = (X: cx, Y: cy + r * 15 / 100);
        var bodyRx = r * 85 / 100;
        var bodyRy = r * 60 / 100;

        var bumpL = (X: cx - r * 40 / 100, Y: cy - r * 55 / 100);
        var bumpR = (X: cx + r * 40 / 100, Y: cy - r * 55 / 100);
        var bumpRadius = r * 28 / 100;

        var eyeL = (X: bumpL.X, Y: cy - r * 62 / 100);
        var eyeR = (X: bumpR.X, Y: cy - r * 62 / 100);
        var eyeRadius = r * 13 / 100;
        var pupilRadius = r * 5 / 100;

        var legL = (X: cx - r * 80 / 100, Y: cy + r * 55 / 100);
        var legR = (X: cx + r * 80 / 100, Y: cy + r * 55 / 100);
        var legRx = r * 30 / 100;
        var legRy = r * 20 / 100;

        var cheekL = (X: cx - r * 55 / 100, Y: cy - r * 5 / 100);
        var cheekR = (X: cx + r * 55 / 100, Y: cy - r * 5 / 100);
        var cheekRadius = r * 10 / 100;

        // ── Outlines (pencil, black) ──────────────────────────────────────
        await _app.ClickAtAsync(Locator.ByAutomationId("PencilTool"));
        await _app.ClickAsync(Locator.ByName("Black"));
        await Task.Delay(200);

        await _app.DragThroughAsync(EllipsePoints(bodyCenter.X, bodyCenter.Y, bodyRx, bodyRy, segments: 48));
        await Task.Delay(80);

        await _app.DragThroughAsync(EllipsePoints(legL.X, legL.Y, legRx, legRy, segments: 32));
        await Task.Delay(60);
        await _app.DragThroughAsync(EllipsePoints(legR.X, legR.Y, legRx, legRy, segments: 32));
        await Task.Delay(80);

        await _app.DragThroughAsync(CirclePoints(bumpL.X, bumpL.Y, bumpRadius, segments: 32));
        await Task.Delay(60);
        await _app.DragThroughAsync(CirclePoints(bumpR.X, bumpR.Y, bumpRadius, segments: 32));
        await Task.Delay(80);

        await _app.DragThroughAsync(CirclePoints(eyeL.X, eyeL.Y, eyeRadius, segments: 24));
        await Task.Delay(60);
        await _app.DragThroughAsync(CirclePoints(eyeR.X, eyeR.Y, eyeRadius, segments: 24));
        await Task.Delay(60);

        await _app.DragThroughAsync(CirclePoints(eyeL.X, eyeL.Y, pupilRadius, segments: 16));
        await Task.Delay(60);
        await _app.DragThroughAsync(CirclePoints(eyeR.X, eyeR.Y, pupilRadius, segments: 16));
        await Task.Delay(60);

        await _app.DragThroughAsync(CirclePoints(cheekL.X, cheekL.Y, cheekRadius, segments: 24));
        await Task.Delay(60);
        await _app.DragThroughAsync(CirclePoints(cheekR.X, cheekR.Y, cheekRadius, segments: 24));
        await Task.Delay(60);

        await _app.DragThroughAsync(GrinPoints(cx, cy - r * 5 / 100, halfWidth: r * 45 / 100, depth: r * 18 / 100, steps: 40));
        await Task.Delay(100);

        // ── Fill with colours (fill bucket); ClickAtAsync is required since InvokePattern skips the pointer-event pipeline that switches the active tool.
        await _app.ClickAtAsync(Locator.ByName("Fill"));
        await Task.Delay(200);

        // Green body, bumps and legs — click each region separately since the overlapping outlines split them into distinct enclosed areas,
        // including the lens-shaped slivers where a bump or leg outline crosses the body outline.
        await _app.ClickAsync(Locator.ByName("Green"));
        await _app.ClickAtAsync(cx, cy + r * 45 / 100);
        await Task.Delay(250);
        await _app.ClickAtAsync(bumpL.X, bumpL.Y - bumpRadius + r * 6 / 100);
        await Task.Delay(250);
        await _app.ClickAtAsync(bumpR.X, bumpR.Y - bumpRadius + r * 6 / 100);
        await Task.Delay(250);
        await _app.ClickAtAsync(bumpL.X, cy - r * 35 / 100);
        await Task.Delay(200);
        await _app.ClickAtAsync(bumpR.X, cy - r * 35 / 100);
        await Task.Delay(250);
        await _app.ClickAtAsync(legL.X, legL.Y);
        await Task.Delay(200);
        await _app.ClickAtAsync(legR.X, legR.Y);
        await Task.Delay(200);
        await _app.ClickAtAsync(legL.X + r * 15 / 100, legL.Y - r * 10 / 100);
        await Task.Delay(200);
        await _app.ClickAtAsync(legR.X - r * 15 / 100, legR.Y - r * 10 / 100);
        await Task.Delay(300);

        // Black pupils.
        await _app.ClickAsync(Locator.ByName("Black"));
        await _app.ClickAtAsync(eyeL.X, eyeL.Y);
        await Task.Delay(200);
        await _app.ClickAtAsync(eyeR.X, eyeR.Y);
        await Task.Delay(300);

        // Rose blush.
        await _app.ClickAsync(Locator.ByName("Rose"));
        await _app.ClickAtAsync(cheekL.X, cheekL.Y);
        await Task.Delay(200);
        await _app.ClickAtAsync(cheekR.X, cheekR.Y);
        await Task.Delay(300);

        // ── Text label ────────────────────────────────────────────────────
        await _app.ClickAsync(Locator.ByName("Black"));
        await _app.ClickAtAsync(Locator.ByName("Text"));
        await Task.Delay(300);

        await _app.ClickAtAsync(cx - r * 30 / 100, cy + r * 100 / 100);
        await Task.Delay(400);

        await _app.TypeDirectAsync("Ribbit!");
        await Task.Delay(300);

        await _app.ClickAtAsync(cx, cy - r * 120 / 100);
        await Task.Delay(400);

        // Switch away from the text tool before saving; leaving it active can interfere with the Save click.
        await _app.ClickAtAsync(Locator.ByAutomationId("PencilTool"));
        await Task.Delay(200);

        // ── Save ──────────────────────────────────────────────────────────
        var stamp    = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var savePath = Path.Combine(Path.GetTempPath(), $"automancer-frog-{stamp}.png");
        await SaveAsync(savePath);

        Assert.True(File.Exists(savePath), $"File not found at {savePath}");
        output.WriteLine($"Frog saved — open to view: {savePath}");
    }

    // Returns waypoints for an open grin arc: a parabola from the left mouth corner to the right, curving down.
    private static IReadOnlyList<(int X, int Y)> GrinPoints(int cx, int cy, int halfWidth, int depth, int steps)
        => Enumerable.Range(0, steps + 1)
            .Select(i => {
                var t = (double)i / steps;
                var x = (int)(cx - halfWidth + 2 * halfWidth * t);
                var y = (int)(cy + depth * 4 * t * (1 - t));
                return (X: x, Y: y);
            })
            .ToList();
}
