// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using Xunit.Abstractions;

namespace AutoMancer.Engine.Tests.Integration;

// End-to-end workflow exercising pencil drawing of straight and curved outlines, fill-bucket coloring, and text-tool TypeAsync.
[Collection("Paint")]
[Trait("Category", "Integration")]
public sealed class PaintWizardLogoTests(ITestOutputHelper output) : PaintArtTestBase
{
    // Draws a wizard hat (cone + brim + star + sparkle), labels it "AutoMancer", and saves the result as a PNG.
    [Fact]
    public async Task DrawWizardHat_SavesToPng_CanBeViewedManually()
    {
        var canvas = await _app.FindAsync(Locator.ByAutomationId("image"));
        var rect   = canvas.BoundingRect;
        var cx     = (int)(rect.X + rect.Width  / 2);
        var cy     = (int)(rect.Y + rect.Height / 2);
        var r      = (int)(Math.Min(rect.Width, rect.Height) * 0.30);

        // ── Geometry: cone apex tilted slightly left of centre for a "flopped" look.
        var apex      = (X: cx - r / 10, Y: cy - r * 11 / 10);
        var baseLeft  = (X: cx - r * 65 / 100, Y: cy + r * 35 / 100);
        var baseRight = (X: cx + r * 65 / 100, Y: cy + r * 35 / 100);

        // Brim: a flattened ellipse centred on the cone's base line, so the cone hides its top cap and only the lower crescent reads as a brim.
        var brimCenter = (X: cx, Y: cy + r * 35 / 100);
        var brimRx     = r * 90 / 100;
        var brimRy     = r * 22 / 100;

        var starCenter    = (X: cx - r / 20, Y: cy - r * 35 / 100);
        var sparkleCenter = (X: cx + r * 95 / 100, Y: cy - r * 90 / 100);

        // ── Outlines (pencil, black) ──────────────────────────────────────
        await _app.ClickAtAsync(Locator.ByAutomationId("PencilTool"));
        await _app.ClickAsync(Locator.ByName("Black"));
        await Task.Delay(200);

        await _app.DragThroughAsync(EllipsePoints(brimCenter.X, brimCenter.Y, brimRx, brimRy, segments: 48));
        await Task.Delay(80);

        await _app.DragThroughAsync([apex, baseRight, baseLeft, apex]);
        await Task.Delay(80);

        await _app.DragThroughAsync(StarPoints(starCenter.X, starCenter.Y, outerR: r * 18 / 100, innerR: r * 7 / 100, points: 5));
        await Task.Delay(60);

        await _app.DragThroughAsync(StarPoints(sparkleCenter.X, sparkleCenter.Y, outerR: r * 16 / 100, innerR: r * 4 / 100, points: 4));
        await Task.Delay(100);

        // ── Fill with colours (fill bucket); ClickAtAsync is required since InvokePattern skips the pointer-event pipeline that switches the active tool.
        await _app.ClickAtAsync(Locator.ByName("Fill"));
        await Task.Delay(200);

        // Purple cone — click below the star so the flood fill doesn't cross into the star's interior.
        await _app.ClickAsync(Locator.ByName("Purple"));
        await _app.ClickAtAsync(cx - r * 30 / 100, cy);
        await Task.Delay(300);

        // Indigo brim — click in the lower crescent, below the cone's base line.
        await _app.ClickAsync(Locator.ByName("Indigo"));
        await _app.ClickAtAsync(cx, cy + r * 50 / 100);
        await Task.Delay(300);

        // Gold star.
        await _app.ClickAsync(Locator.ByName("Gold"));
        await _app.ClickAtAsync(starCenter.X, starCenter.Y);
        await Task.Delay(200);

        // Turquoise sparkle.
        await _app.ClickAsync(Locator.ByName("Turquoise"));
        await _app.ClickAtAsync(sparkleCenter.X, sparkleCenter.Y);
        await Task.Delay(300);

        // ── Text label ────────────────────────────────────────────────────
        await _app.ClickAsync(Locator.ByName("Black"));
        await _app.ClickAtAsync(Locator.ByName("Text"));
        await Task.Delay(300);

        await _app.ClickAtAsync(cx - r / 2, cy + r * 90 / 100);
        await Task.Delay(400);

        await _app.TypeDirectAsync("AutoMancer");
        await Task.Delay(300);

        await _app.ClickAtAsync(cx, cy - r * 12 / 10);
        await Task.Delay(400);

        // Switch away from the text tool before saving; leaving it active can interfere with the Save click.
        await _app.ClickAtAsync(Locator.ByAutomationId("PencilTool"));
        await Task.Delay(200);

        // ── Save ──────────────────────────────────────────────────────────
        var stamp    = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var savePath = Path.Combine(Path.GetTempPath(), $"automancer-wizard-logo-{stamp}.png");
        await SaveAsync(savePath);

        Assert.True(File.Exists(savePath), $"File not found at {savePath}");
        output.WriteLine($"Wizard logo saved — open to view: {savePath}");
    }

    // Returns waypoints for a closed N-pointed star, alternating outer and inner radii, tip pointing up.
    private static IReadOnlyList<(int X, int Y)> StarPoints(int cx, int cy, int outerR, int innerR, int points)
        => Enumerable.Range(0, 2 * points + 1)
            .Select(i => {
                var a = -Math.PI / 2 + i * Math.PI / points;
                var radius = i % 2 == 0 ? outerR : innerR;
                return (X: (int)(cx + radius * Math.Cos(a)), Y: (int)(cy + radius * Math.Sin(a)));
            })
            .ToList();
}
