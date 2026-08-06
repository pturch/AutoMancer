// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using Xunit.Abstractions;

namespace AutoMancer.Engine.Tests.Integration;

// End-to-end workflow exercising pencil drawing of straight and curved outlines, fill-bucket coloring, and text-tool TypeAsync.
[Collection("Paint")]
[Trait("Category", "Integration")]
public sealed class PaintWizardLogoTests(ITestOutputHelper output) : IAsyncLifetime
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

    // Returns waypoints for a closed ellipse approximated with the given number of line segments.
    private static IReadOnlyList<(int X, int Y)> EllipsePoints(int cx, int cy, int rx, int ry, int segments)
        => Enumerable.Range(0, segments + 1)
            .Select(i => {
                var a = 2 * Math.PI * i / segments;
                return (X: (int)(cx + rx * Math.Cos(a)), Y: (int)(cy + ry * Math.Sin(a)));
            })
            .ToList();

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
