// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using Xunit.Abstractions;

namespace AutoMancer.Engine.Tests.Integration;

// End-to-end workflow exercising pencil drawing of overlapping rounded shapes, fill-bucket coloring, and text-tool TypeAsync.
[Collection("Paint")]
[Trait("Category", "Integration")]
public sealed class PaintFrogTests(ITestOutputHelper output) : IAsyncLifetime
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

        // Paint remembers canvas size from the last session, which would silently break the radii/offsets computed below — force a known size.
        await SetCanvasSizeAsync(800);
    }

    // Kills Paint without triggering the save dialog.
    public async Task DisposeAsync()
    {
        _app.Kill();
        await Task.Delay(800);
    }

    // Opens the Resize and Skew flyout and sets the canvas to an exact square pixel size, so "Maintain aspect ratio" can't matter.
    private async Task SetCanvasSizeAsync(int size)
    {
        await _app.ClickAtAsync(Locator.ByName("Resize and skew"));
        await Task.Delay(300);

        await _app.ClickAsync(Locator.ByName("Pixels"));
        await Task.Delay(100);

        await _app.TypeAsync(Locator.ByAutomationId("HorizontalResizeTextBox"), size.ToString());
        await _app.TypeAsync(Locator.ByAutomationId("VerticalResizeTextBox"), size.ToString());
        await Task.Delay(100);

        await _app.ClickAsync(Locator.ByAutomationId("PrimaryButton"));
        await Task.Delay(400);
    }

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

    // Clicks the toolbar Save button and interacts with the Save As dialog to write to savePath.
    private async Task SaveAsync(string savePath)
    {
        await _app.ClickAsync(Locator.ByName("Save"));

        // FindDialogAsync enumerates top-level windows by PID + title, which works even though modal dialogs don't change Process.MainWindowTitle.
        var dialog = await App.FindDialogAsync(_app.ProcessId, "Save", timeoutMs: 4_000);
        if (dialog is null) throw new InvalidOperationException("Save As dialog did not appear within 4 s.");

        // Navigate to the target directory via the address bar (Alt+D) then type just the filename — a full path in the filename ComboBox triggers validation errors.
        var dir      = Path.GetDirectoryName(savePath)!;
        var fileName = Path.GetFileName(savePath);

        await dialog.PressKeyAsync(Key.D, new KeyModifiers(Alt: true));   // Alt+D — focus address bar
        await Task.Delay(500);                       // wait for address bar to enter edit mode
        await dialog.TypeDirectAsync(dir);
        await dialog.PressKeyAsync(Key.Enter);            // Enter — navigate to folder
        await Task.Delay(1_200);

        // Physically click the filename ComboBox, wipe "Untitled" with Ctrl+A, then type the filename, since the dialog reads the displayed text on Enter, not the ValuePattern COM value.
        await dialog.ClickAtAsync(Locator.ByAutomationId("1001"));
        await Task.Delay(150);
        await dialog.PressKeyAsync(Key.A, new KeyModifiers(Control: true));  // Ctrl+A — select all
        await Task.Delay(100);
        await dialog.TypeDirectAsync(fileName);
        await Task.Delay(300);
        await dialog.PressKeyAsync(Key.Enter);           // Enter — submit the dialog

        // On subsequent runs the file already exists; dismiss the Replace confirmation if it appears.
        var confirm = await App.FindDialogAsync(_app.ProcessId, "Replace", timeoutMs: 1_500);
        if (confirm is not null)
            await confirm.ClickAsync(Locator.ByName("Replace"));

        await Task.Delay(1_000);
    }

    // Returns waypoints for a closed circle approximated with the given number of line segments.
    private static IReadOnlyList<(int X, int Y)> CirclePoints(int cx, int cy, int r, int segments)
        => Enumerable.Range(0, segments + 1)
            .Select(i => {
                var a = 2 * Math.PI * i / segments;
                return (X: (int)(cx + r * Math.Cos(a)), Y: (int)(cy + r * Math.Sin(a)));
            })
            .ToList();

    // Returns waypoints for a closed ellipse approximated with the given number of line segments.
    private static IReadOnlyList<(int X, int Y)> EllipsePoints(int cx, int cy, int rx, int ry, int segments)
        => Enumerable.Range(0, segments + 1)
            .Select(i => {
                var a = 2 * Math.PI * i / segments;
                return (X: (int)(cx + rx * Math.Cos(a)), Y: (int)(cy + ry * Math.Sin(a)));
            })
            .ToList();

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
