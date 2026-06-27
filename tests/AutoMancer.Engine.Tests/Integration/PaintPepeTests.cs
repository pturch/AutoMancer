// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using Xunit.Abstractions;

namespace AutoMancer.Engine.Tests.Integration;

// End-to-end workflow: launch Paint, draw Pepe the Frog, save it to a temp PNG, and print the path.
[Collection("Paint")]
[Trait("Category", "Integration")]
public sealed class PaintPepeTests(ITestOutputHelper output) : IAsyncLifetime
{
    private App _app = null!;

    // Launches Paint and waits for the toolbar to be ready, then dismisses the welcome popup.
    public async Task InitializeAsync()
    {
        _app = await App.LaunchAsync("mspaint.exe");
        var warmup = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 10_000 });
        await warmup.FindAsync(Locator.ByAutomationId("PencilTool"));

        var quick = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 2_000 });
        try { await quick.ClickAsync(Locator.ByXPath("//Window[@Name='Popup']//Button[@Name='Close']")); }
        catch { }

        await Task.Delay(300);
    }

    // Kills Paint without triggering the save dialog.
    public async Task DisposeAsync()
    {
        _app.Kill();
        await Task.Delay(800);
    }

    // Draws Pepe the Frog with the Pencil tool and saves the result as a PNG to %TEMP%.
    [Fact]
    public async Task DrawPepe_SavesToPng_CanBeViewedManually()
    {
        await _app.ClickAsync(Locator.ByAutomationId("PencilTool"));
        await _app.ClickAsync(Locator.ByName("Green"));
        await Task.Delay(200);

        var canvas = await _app.FindAsync(Locator.ByAutomationId("image"));
        var rect   = canvas.BoundingRect;
        var cx     = (int)(rect.X + rect.Width  / 2);
        var cy     = (int)(rect.Y + rect.Height / 2);
        var R      = (int)(Math.Min(rect.Width, rect.Height) * 0.28);

        // ── Head ─────────────────────────────────────────────────────────────
        // Wide oval; centre shifted slightly upward so there's room for the mouth below.
        await _app.DragThroughAsync(Ellipse(cx, cy - R / 20, (int)(R * 1.15), R, segments: 64));
        await Task.Delay(80);

        // ── Eyes ─────────────────────────────────────────────────────────────
        // Pepe's eyes are large, prominent ovals.
        int lex = cx - (int)(R * 0.46), ley = cy - (int)(R * 0.16);
        int rex = cx + (int)(R * 0.46), rey = ley;

        await _app.DragThroughAsync(Ellipse(lex, ley, (int)(R * 0.39), (int)(R * 0.34), segments: 32));
        await Task.Delay(60);
        await _app.DragThroughAsync(Ellipse(rex, rey, (int)(R * 0.39), (int)(R * 0.34), segments: 32));
        await Task.Delay(60);

        // ── Pupils ───────────────────────────────────────────────────────────
        await _app.DragThroughAsync(Circle(lex, ley + R / 14, (int)(R * 0.14), segments: 20));
        await Task.Delay(50);
        await _app.DragThroughAsync(Circle(rex, rey + R / 14, (int)(R * 0.14), segments: 20));
        await Task.Delay(50);

        // ── Eyelids ──────────────────────────────────────────────────────────
        // Horizontal line at the eye's vertical centre — the signature heavy-lidded look.
        await _app.DragAsync(lex - (int)(R * 0.39), ley, lex + (int)(R * 0.39), ley);
        await Task.Delay(40);
        await _app.DragAsync(rex - (int)(R * 0.39), rey, rex + (int)(R * 0.39), rey);
        await Task.Delay(40);

        // ── Nostrils ─────────────────────────────────────────────────────────
        await _app.DragThroughAsync(Circle(cx - (int)(R * 0.18), cy + (int)(R * 0.24), (int)(R * 0.07), segments: 16));
        await Task.Delay(40);
        await _app.DragThroughAsync(Circle(cx + (int)(R * 0.18), cy + (int)(R * 0.24), (int)(R * 0.07), segments: 16));
        await Task.Delay(40);

        // ── Mouth ────────────────────────────────────────────────────────────
        // Upper lip: wide, flat, with corners drooping slightly downward for Pepe's neutral expression.
        await _app.DragThroughAsync(UpperMouth(cx, cy, R, steps: 32));
        await Task.Delay(50);

        // Lower lip: a gentle downward bulge below the upper lip line.
        await _app.DragThroughAsync(LowerLip(cx, cy, R, steps: 32));
        await Task.Delay(200);

        // ── Save ─────────────────────────────────────────────────────────────
        var savePath = Path.Combine(Path.GetTempPath(), "automancer-pepe.png");
        await SaveAsync(savePath);

        Assert.True(File.Exists(savePath), $"File not found at {savePath}");
        output.WriteLine($"Pepe saved — open to view: {savePath}");
    }

    // Clicks Save and interacts with the Save As dialog via FindDialogAsync.
    private async Task SaveAsync(string savePath)
    {
        await _app.ClickAsync(Locator.ByName("Save"));

        var dialog = await App.FindDialogAsync(_app.ProcessId, "Save", timeoutMs: 4_000);
        if (dialog is null) throw new InvalidOperationException("Save As dialog did not appear within 4 s.");

        // Navigate to the target directory via the address bar (Alt+D), then type just the filename.
        // Typing a full path into the filename ComboBox triggers "file name cannot contain special
        // characters" validation; typing only the filename after navigating to the folder avoids this.
        var dir      = Path.GetDirectoryName(savePath)!;
        var fileName = Path.GetFileName(savePath);

        await dialog.PressChordAsync(0x12, 0x44);   // Alt+D — focus address bar
        await Task.Delay(300);
        await dialog.TypeDirectAsync(dir);
        await dialog.PressKeyAsync(0x0D);            // Enter — navigate to folder
        await Task.Delay(1_000);

        // AutomationId "1001" is the IFileDialog filename ComboBox; more reliable than ByControlType("Edit")
        // which can accidentally match the search box or the file-list's inline rename edit.
        await dialog.ClickAsync(Locator.ByAutomationId("1001"));
        await Task.Delay(200);
        await dialog.TypeDirectAsync(fileName);
        await Task.Delay(200);
        await dialog.ClickAsync(Locator.ByName("Save"));
        await Task.Delay(1_000);
    }

    // ── Shape helpers ─────────────────────────────────────────────────────────

    // Returns waypoints for a closed circle approximated with the given number of segments.
    private static IReadOnlyList<(int X, int Y)> Circle(int cx, int cy, int r, int segments)
        => Ellipse(cx, cy, r, r, segments);

    // Returns waypoints for a closed ellipse with independent x/y radii.
    private static IReadOnlyList<(int X, int Y)> Ellipse(int cx, int cy, int rx, int ry, int segments)
        => Enumerable.Range(0, segments + 1)
            .Select(i => {
                var a = 2 * Math.PI * i / segments;
                return (X: (int)(cx + rx * Math.Cos(a)), Y: (int)(cy + ry * Math.Sin(a)));
            }).ToList();

    // Returns waypoints for Pepe's upper mouth: wide and flat, with corners drooping slightly downward.
    private static IReadOnlyList<(int X, int Y)> UpperMouth(int cx, int cy, int R, int steps)
        => Enumerable.Range(0, steps + 1)
            .Select(i => {
                var t    = (double)i / steps;
                var x    = (int)(cx - R * 0.53 + R * 1.06 * t);
                // (2t-1)^2 is 1 at the corners (t=0, t=1) and 0 at the centre — droop corners DOWN.
                var y    = (int)(cy + R * 0.46 + R * 0.06 * (2 * t - 1) * (2 * t - 1));
                return (X: x, Y: y);
            }).ToList();

    // Returns waypoints for Pepe's lower lip: a smooth parabolic arc bowing downward.
    private static IReadOnlyList<(int X, int Y)> LowerLip(int cx, int cy, int R, int steps)
        => Enumerable.Range(0, steps + 1)
            .Select(i => {
                var t = (double)i / steps;
                var x = (int)(cx - R * 0.43 + R * 0.86 * t);
                var y = (int)(cy + R * 0.54 + R * 0.16 * 4 * t * (1 - t));
                return (X: x, Y: y);
            }).ToList();
}
