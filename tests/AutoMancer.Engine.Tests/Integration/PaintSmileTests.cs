// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using Xunit.Abstractions;

namespace AutoMancer.Engine.Tests.Integration;

// End-to-end workflow: launch Paint, draw a colorful smiley face (black outlines, yellow face, blue eyes), save to a temp PNG.
[Collection("Paint")]
[Trait("Category", "Integration")]
public sealed class PaintSmileTests(ITestOutputHelper output) : IAsyncLifetime
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

    // Draws a colorful smiley face: black outlines filled with yellow skin and blue eyes.
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
        await _app.ClickAsync(Locator.ByAutomationId("PencilTool"));
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

        // ── Fill with colours (fill bucket) ──────────────────────────────
        await _app.ClickAsync(Locator.ByName("Fill"));
        await Task.Delay(200);

        // Yellow face — click in the centre of the face, between the eyes and the smile.
        await _app.ClickAsync(Locator.ByName("Yellow"));
        await _app.ClickAtAsync(cx, cy);
        await Task.Delay(300);

        // Turquoise eyes — click inside each eye outline; the yellow fill does not cross the eye border,
        // so the interiors are still white and will accept the colour cleanly.
        await _app.ClickAsync(Locator.ByName("Turquoise"));
        await _app.ClickAtAsync(lex, ley);
        await Task.Delay(200);
        await _app.ClickAtAsync(rex, rey);
        await Task.Delay(300);

        // ── Save ──────────────────────────────────────────────────────────
        var savePath = Path.Combine(Path.GetTempPath(), "automancer-smile.png");
        await SaveAsync(savePath);

        Assert.True(File.Exists(savePath), $"File not found at {savePath}");
        output.WriteLine($"Smile saved — open to view: {savePath}");
    }

    // Clicks the toolbar Save button and interacts with the Save As dialog to write to savePath.
    private async Task SaveAsync(string savePath)
    {
        await _app.ClickAsync(Locator.ByName("Save"));

        // FindDialogAsync enumerates all top-level windows to find the dialog by PID + title,
        // which works even though modal dialogs don't change Process.MainWindowTitle.
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

    // Returns waypoints for a closed circle approximated with the given number of line segments.
    private static IReadOnlyList<(int X, int Y)> CirclePoints(int cx, int cy, int r, int segments)
        => Enumerable.Range(0, segments + 1)
            .Select(i => {
                var a = 2 * Math.PI * i / segments;
                return (X: (int)(cx + r * Math.Cos(a)), Y: (int)(cy + r * Math.Sin(a)));
            })
            .ToList();

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
