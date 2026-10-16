// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies WindowAction's UIA-pattern path and Win32 fallback, using Notepad (supports both) and Calculator (supports neither); in "Notepad" collection to serialize foreground/window-state use.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class WindowActionIntegrationTests
{
    private const string CalculatorAumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";

    // Launches a fresh Notepad instance, resolves its window handle for direct Win32 assertions, and clears any text left over from a reused single-instance window.
    private static async Task<(App App, IntPtr WindowHandle)> LaunchNotepadAsync()
    {
        var app = await App.LaunchAsync("notepad.exe");
        await NotepadFixture.ClearDocumentAsync(app);
        return (app, Process.GetProcessById(app.ProcessId).MainWindowHandle);
    }

    // Launches a fresh Calculator instance and resolves its window handle for direct Win32 assertions, returning once its UI is on screen.
    private static async Task<(App App, IntPtr WindowHandle)> LaunchCalculatorAsync()
    {
        var app = await App.LaunchPackagedAsync(CalculatorAumid);
        // Grab the handle immediately; waiting first can catch it mid-transition and read back stale/zero.
        var windowHandle = Process.GetProcessById(app.ProcessId).MainWindowHandle;
        await app.WithOptions(new AppOptions { ImplicitWaitMs = 10_000 }).WaitForAsync(Locator.ByAutomationId("CalculatorResults"), WaitConditions.IsVisible());
        return (app, windowHandle);
    }

    [Fact]
    public async Task MoveAsync_WhenTransformPatternSupported_MovesWindow()
    {
        var (app, windowHandle) = await LaunchNotepadAsync();
        try
        {
            await app.MoveWindowAsync(120, 90);

            await ExpectWindowRectAsync(app, windowHandle, r => r.Left is >= 118 and <= 122 && r.Top is >= 88 and <= 92, "to sit at (120, 90) within 2px");
        }
        finally
        {
            await app.KillAsync();
        }
    }

    [Fact]
    public async Task MoveAsync_WhenTransformPatternUnsupported_FallsBackToSetWindowPos()
    {
        var (app, windowHandle) = await LaunchCalculatorAsync();
        try
        {
            NativeMethods.GetWindowRect(windowHandle, out var before);

            await app.MoveWindowAsync(before.Left + 300, before.Top + 300);

            // DPI virtualization offsets X/Y here (width/height are exact), so just check for a large move.
            await ExpectWindowRectAsync(app, windowHandle, r => Math.Abs(r.Left - before.Left) > 100 || Math.Abs(r.Top - before.Top) > 100, $"to move well away from ({before.Left},{before.Top})");
        }
        finally
        {
            await app.KillAsync();
        }
    }

    [Fact]
    public async Task ResizeAsync_WhenTransformPatternSupported_ResizesWindow()
    {
        var (app, windowHandle) = await LaunchNotepadAsync();
        try
        {
            await app.ResizeWindowAsync(640, 480);

            await ExpectWindowRectAsync(app, windowHandle, r => r.Right - r.Left is >= 636 and <= 644 && r.Bottom - r.Top is >= 476 and <= 484, "to be 640x480 within 4px");
        }
        finally
        {
            await app.KillAsync();
        }
    }

    [Fact]
    public async Task ResizeAsync_WhenTransformPatternUnsupported_FallsBackToSetWindowPos()
    {
        var (app, windowHandle) = await LaunchCalculatorAsync();
        try
        {
            await app.ResizeWindowAsync(500, 600);

            await ExpectWindowRectAsync(app, windowHandle, r => r.Right - r.Left is >= 496 and <= 504 && r.Bottom - r.Top is >= 596 and <= 604, "to be 500x600 within 4px");
        }
        finally
        {
            await app.KillAsync();
        }
    }

    [Fact]
    public async Task SetVisualStateAsync_WhenWindowPatternSupported_ChangesVisualState()
    {
        var (app, windowHandle) = await LaunchNotepadAsync();
        try
        {
            await app.SetWindowStateAsync(WindowState.Maximized);
            await ExpectZoomedAsync(app, windowHandle, zoomed: true);

            await app.SetWindowStateAsync(WindowState.Normal);
            await ExpectZoomedAsync(app, windowHandle, zoomed: false);
        }
        finally
        {
            await app.KillAsync();
        }
    }

    [Fact]
    public async Task SetVisualStateAsync_WhenWindowPatternUnsupported_FallsBackToShowWindow()
    {
        var (app, windowHandle) = await LaunchCalculatorAsync();
        try
        {
            await app.SetWindowStateAsync(WindowState.Maximized);
            await ExpectZoomedAsync(app, windowHandle, zoomed: true);

            await app.SetWindowStateAsync(WindowState.Normal);
            await ExpectZoomedAsync(app, windowHandle, zoomed: false);
        }
        finally
        {
            await app.KillAsync();
        }
    }

    // Complements WindowCloseIntegrationTests' WM_CLOSE fallback case by exercising Notepad's WindowPattern.Close() path.
    [Fact]
    public async Task CloseAsync_WhenWindowPatternSupported_ClosesWindow()
    {
        var (app, windowHandle) = await LaunchNotepadAsync();
        try
        {
            await app.CloseWindowAsync();

            await Expect(app, () => Task.FromResult(NativeMethods.IsWindowVisible(windowHandle))).ToSatisfyAsync(visible => !visible, "the window to close", timeoutMs: 10_000);
        }
        finally
        {
            await app.KillAsync();
        }
    }

    // Polls the window's screen rect until condition holds.
    private static Task<NativeMethods.RECT> ExpectWindowRectAsync(App app, IntPtr windowHandle, Func<NativeMethods.RECT, bool> condition, string description)
        => Expect(app, () =>
        {
            NativeMethods.GetWindowRect(windowHandle, out var rect);
            return Task.FromResult(rect);
        }).ToSatisfyAsync(condition, description);

    // Polls until the window's maximized state matches zoomed.
    private static Task<bool> ExpectZoomedAsync(App app, IntPtr windowHandle, bool zoomed)
        => Expect(app, () => Task.FromResult(NativeMethods.IsZoomed(windowHandle))).ToSatisfyAsync(z => z == zoomed, zoomed ? "to be maximized" : "to be restored");
}
