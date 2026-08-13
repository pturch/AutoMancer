// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using Xunit.Abstractions;

namespace AutoMancer.Engine.Tests.Integration;

// End-to-end workflow exercising pencil drawing, fill-bucket coloring, text-tool TypeAsync, and the Save As dialog flow.
[Collection("Paint")]
[Trait("Category", "Integration")]
public sealed class PaintSmileTests(ITestOutputHelper output) : PaintArtTestBase
{
    // Draws a colorful smiley face, adds a text label, and saves the result as a PNG to %TEMP%.
    [Fact]
    public async Task DrawSmile_SavesToPng_CanBeViewedManually()
    {
        var canvas = await _app.FindAsync(Locator.ByAutomationId("image"));
        var rect   = canvas.BoundingRect;
        var cx     = (int)(rect.X + rect.Width  / 2);
        var cy     = (int)(rect.Y + rect.Height / 2);
        var r      = (int)(Math.Min(rect.Width, rect.Height) * 0.30);
        var er     = r / 7;   // eye radius

        // ── Outlines (pencil, black) ──────────────────────────────────────
        await _app.ClickAtAsync(Locator.ByAutomationId("PencilTool"));
        await _app.ClickAsync(Locator.ByName("Black"));
        await Task.Delay(200);

        await _app.DragThroughAsync(CirclePoints(cx, cy, r, segments: 64));
        await Task.Delay(80);

        int lex = cx - r / 3, ley = cy - r / 4;
        int rex = cx + r / 3, rey = ley;
        await _app.DragThroughAsync(CirclePoints(lex, ley, er, segments: 28));
        await Task.Delay(60);
        await _app.DragThroughAsync(CirclePoints(rex, rey, er, segments: 28));
        await Task.Delay(60);

        await _app.DragThroughAsync(SmilePoints(cx, cy, r, steps: 40));
        await Task.Delay(100);

        // ── Fill with colours (fill bucket); ClickAtAsync is required since InvokePattern skips the pointer-event pipeline that switches the active tool.
        await _app.ClickAtAsync(Locator.ByName("Fill"));
        await Task.Delay(200);

        // Yellow face — click in the centre of the face, between the eyes and the smile.
        await _app.ClickAsync(Locator.ByName("Yellow"));
        await _app.ClickAtAsync(cx, cy);
        await Task.Delay(300);

        // Turquoise eyes — click inside each eye outline, where the yellow fill hasn't crossed and the interior is still white.
        await _app.ClickAsync(Locator.ByName("Turquoise"));
        await _app.ClickAtAsync(lex, ley);
        await Task.Delay(200);
        await _app.ClickAtAsync(rex, rey);
        await Task.Delay(300);

        // ── Text label: selects the Text tool, places a cursor below the face, then types via TypeAsync against Paint's WinUI3 TextBox.
        await _app.ClickAsync(Locator.ByName("Black"));
        await _app.ClickAtAsync(Locator.ByName("Text"));
        await Task.Delay(300);

        await _app.ClickAtAsync(cx, cy + r + 24);    // place cursor just below the face
        await Task.Delay(400);                        // give Paint time to open the text box and focus it

        // TypeDirectAsync sends to whatever has focus, avoiding an element search that couldn't distinguish this Edit control from others.
        await _app.TypeDirectAsync("Hello :)");
        await Task.Delay(300);

        await _app.ClickAtAsync(cx, cy - r - 24);    // click outside the text box to commit
        await Task.Delay(400);

        // Switch away from the text tool before saving; leaving it active can interfere with the Save click.
        await _app.ClickAtAsync(Locator.ByAutomationId("PencilTool"));
        await Task.Delay(200);

        // ── Save ──────────────────────────────────────────────────────────
        var stamp    = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var savePath = Path.Combine(Path.GetTempPath(), $"automancer-smile-{stamp}.png");
        await SaveAsync(savePath);

        Assert.True(File.Exists(savePath), $"File not found at {savePath}");
        output.WriteLine($"Smile saved — open to view: {savePath}");
    }

    // Returns waypoints for a smile arc: a parabola from the left mouth corner to the right, curving down.
    private static IReadOnlyList<(int X, int Y)> SmilePoints(int cx, int cy, int r, int steps)
        => Enumerable.Range(0, steps + 1)
            .Select(i => {
                var t  = (double)i / steps;
                var x  = (int)(cx - r * 0.5 + r * t);
                var y  = (int)(cy + r * 0.2  + r * 0.4 * 4 * t * (1 - t));
                return (X: x, Y: y);
            })
            .ToList();
}
