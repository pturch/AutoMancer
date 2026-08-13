// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Shared launch/teardown/canvas-setup/save/geometry helpers for the Paint art tests (Frog, WizardLogo, Smile) — each draws its own picture in one [Fact] against its own freshly-launched Paint instance.
public abstract class PaintArtTestBase : IAsyncLifetime
{
    protected App _app = null!;

    // Launches Paint and waits for the toolbar to be ready, then dismisses the welcome popup and forces a known canvas size.
    public async Task InitializeAsync()
    {
        _app = await App.LaunchAsync("mspaint.exe");
        var warmup = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 10_000 });
        await warmup.FindAsync(Locator.ByAutomationId("PencilTool"));

        var quick = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 2_000 });
        try { await quick.ClickAsync(Locator.ByXPath("//Window[@Name='Popup']//Button[@Name='Close']")); }
        catch { }

        await Task.Delay(300);

        // Paint remembers canvas size from the last session, which would silently break the radii/offsets each derived test computes — force a known size.
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

    // Clicks the toolbar Save button and interacts with the Save As dialog to write to savePath.
    protected async Task SaveAsync(string savePath)
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
    protected static IReadOnlyList<(int X, int Y)> CirclePoints(int cx, int cy, int r, int segments)
        => Enumerable.Range(0, segments + 1)
            .Select(i => {
                var a = 2 * Math.PI * i / segments;
                return (X: (int)(cx + r * Math.Cos(a)), Y: (int)(cy + r * Math.Sin(a)));
            })
            .ToList();

    // Returns waypoints for a closed ellipse approximated with the given number of line segments.
    protected static IReadOnlyList<(int X, int Y)> EllipsePoints(int cx, int cy, int rx, int ry, int segments)
        => Enumerable.Range(0, segments + 1)
            .Select(i => {
                var a = 2 * Math.PI * i / segments;
                return (X: (int)(cx + rx * Math.Cos(a)), Y: (int)(cy + ry * Math.Sin(a)));
            })
            .ToList();
}
