// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using static AutoMancer.Testing.Assertions;

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

        // The welcome popup's Close button is targeted via XPath to avoid matching the title-bar Close button, which shares AutomationId="Close".
        var popupClose = Locator.ByXPath("//Window[@Name='Popup']//Button[@Name='Close']");
        var quick = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 2_000 });
        try
        {
            await quick.ClickAsync(popupClose);
            await _app.WaitUntilGoneAsync(popupClose);
        }
        catch (ElementNotFoundError) { /* popup absent — already dismissed on this Windows install */ }

        // Paint remembers canvas size from the last session, which would silently break the radii/offsets each derived test computes — force a known size.
        await SetCanvasSizeAsync(800);
    }

    // Kills Paint without triggering the save dialog; no-ops if InitializeAsync itself failed to launch, so that failure surfaces on its own instead of being masked by a NullReferenceException here.
    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.KillAsync();
    }

    // Opens the Resize and Skew flyout and sets the canvas to an exact square pixel size, then waits for the status bar to confirm it.
    private async Task SetCanvasSizeAsync(int size)
    {
        var width = Locator.ByAutomationId("HorizontalResizeTextBox");
        var height = Locator.ByAutomationId("VerticalResizeTextBox");
        await _app.ClickAtAsync(Locator.ByName("Resize and skew"));
        await _app.ClickAsync(Locator.ByName("Pixels"));
        await _app.TypeAsync(width, size.ToString());
        await _app.TypeAsync(height, size.ToString());

        // With "Maintain aspect ratio" on, setting the height re-derives the width, so a non-square canvas never becomes square; Paint remembers the toggle across sessions, so check rather than assume.
        if (await _app.GetValueAsync(width) != size.ToString())
        {
            await _app.ClickAsync(Locator.ByAutomationId("MaintainAspectRatioButton"));
            await _app.TypeAsync(width, size.ToString());
            await _app.TypeAsync(height, size.ToString());
        }

        await _app.ClickAsync(Locator.ByAutomationId("PrimaryButton"));
        await Expect(_app, Locator.ByAutomationId("CanvasSizeTextBlock")).ToHaveNameAsync($"{size} × {size}px");
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

        // The address bar's edit box (41477) exists only while it's in edit mode, so it appearing and then going away brackets the navigation.
        var addressEdit = Locator.ByAutomationId("41477");
        await dialog.PressKeyAsync(Key.D, new KeyModifiers(Alt: true));   // Alt+D — focus address bar
        await dialog.FindAsync(addressEdit);
        await dialog.TypeDirectAsync(dir);
        await dialog.PressKeyAsync(Key.Enter);            // Enter — navigate to folder
        await dialog.WaitUntilGoneAsync(addressEdit);
        await dialog.FindAsync(Locator.ByName($"Address: {dir}"));

        // Physically click the filename box, wipe "Untitled" with Ctrl+A, then type the filename, since the dialog reads the displayed text on Enter, not the ValuePattern COM value. XPath because the address toolbar shares AutomationId 1001.
        var fileNameBox = Locator.ByXPath("//Edit[@AutomationId='1001']");
        await dialog.ClickAtAsync(fileNameBox);
        await dialog.PressKeyAsync(Key.A, new KeyModifiers(Control: true));  // Ctrl+A — select all
        await dialog.TypeDirectAsync(fileName);
        await Expect(dialog, fileNameBox).ToHaveValueAsync(fileName);
        await dialog.PressKeyAsync(Key.Enter);           // Enter — submit the dialog

        // On subsequent runs the file already exists; dismiss the Replace confirmation if it appears.
        var confirm = await App.FindDialogAsync(_app.ProcessId, "Replace", timeoutMs: 1_500);
        if (confirm is not null)
            await confirm.ClickAsync(Locator.ByName("Replace"));

        // Paint writes the file after the dialog closes; a PNG that decodes is one it has finished writing.
        await Expect(_app, () => Task.FromResult(TryDecodePng(savePath) is not null)).ToSatisfyAsync(done => done, $"to show '{savePath}' fully written", timeoutMs: 10_000);
    }

    // Decodes the PNG at path into a BGRA32 pixel reader, or null while it doesn't exist yet or is still being written.
    protected static PngPixels? TryDecodePng(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var frame = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            var bgra = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
            bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
            return new PngPixels(bgra.PixelWidth, bgra.PixelHeight, pixels);
        }
        catch (Exception ex) when (ex is IOException or FileFormatException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    // A decoded image's pixels, readable by (x, y) as an (R, G, B) triple.
    protected sealed record PngPixels(int Width, int Height, byte[] Bgra)
    {
        // Reads the pixel at (x, y).
        public (byte R, byte G, byte B) this[int x, int y]
        {
            get
            {
                var i = (y * Width + x) * 4;
                return (Bgra[i + 2], Bgra[i + 1], Bgra[i]);
            }
        }
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
