// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies WindowAction's UIA-pattern path and Win32 fallback, using Notepad (supports both) and Calculator (supports neither); in "Notepad" collection to serialize foreground/window-state use.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class WindowActionIntegrationTests
{
    private const string CalculatorAumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";

    // Launches a fresh Notepad instance and resolves its window handle for direct Win32 assertions.
    private static async Task<(App App, IntPtr Hwnd)> LaunchNotepadAsync()
    {
        var app = await App.LaunchAsync("notepad.exe");
        return (app, Process.GetProcessById(app.ProcessId).MainWindowHandle);
    }

    // Launches a fresh Calculator instance and resolves its window handle for direct Win32 assertions.
    private static async Task<(App App, IntPtr Hwnd)> LaunchCalculatorAsync()
    {
        var app = await App.LaunchPackagedAsync(CalculatorAumid);
        // Grab the handle immediately; waiting first can catch it mid-transition and read back stale/zero.
        var hwnd = Process.GetProcessById(app.ProcessId).MainWindowHandle;
        await Task.Delay(500); // let the launch entry animation settle before we act on it
        return (app, hwnd);
    }

    [Fact]
    public async Task MoveAsync_WhenTransformPatternSupported_MovesWindow()
    {
        var (app, hwnd) = await LaunchNotepadAsync();
        try
        {
            await app.MoveWindowAsync(120, 90);
            await Task.Delay(200);

            NativeMethods.GetWindowRect(hwnd, out var rect);
            Assert.InRange(rect.Left, 118, 122);
            Assert.InRange(rect.Top, 88, 92);
        }
        finally
        {
            app.Kill();
            await Task.Delay(500);
        }
    }

    [Fact]
    public async Task MoveAsync_WhenTransformPatternUnsupported_FallsBackToSetWindowPos()
    {
        var (app, hwnd) = await LaunchCalculatorAsync();
        try
        {
            NativeMethods.GetWindowRect(hwnd, out var before);

            await app.MoveWindowAsync(before.Left + 300, before.Top + 300);
            await Task.Delay(200);

            // DPI virtualization offsets X/Y here (width/height are exact), so just check for a large move.
            NativeMethods.GetWindowRect(hwnd, out var after);
            Assert.True(Math.Abs(after.Left - before.Left) > 100 || Math.Abs(after.Top - before.Top) > 100,
                $"Expected a large position change from ({before.Left},{before.Top}), got ({after.Left},{after.Top}).");
        }
        finally
        {
            app.Kill();
            await Task.Delay(500);
        }
    }

    [Fact]
    public async Task ResizeAsync_WhenTransformPatternSupported_ResizesWindow()
    {
        var (app, hwnd) = await LaunchNotepadAsync();
        try
        {
            await app.ResizeWindowAsync(640, 480);
            await Task.Delay(200);

            NativeMethods.GetWindowRect(hwnd, out var rect);
            Assert.InRange(rect.Right - rect.Left, 636, 644);
            Assert.InRange(rect.Bottom - rect.Top, 476, 484);
        }
        finally
        {
            app.Kill();
            await Task.Delay(500);
        }
    }

    [Fact]
    public async Task ResizeAsync_WhenTransformPatternUnsupported_FallsBackToSetWindowPos()
    {
        var (app, hwnd) = await LaunchCalculatorAsync();
        try
        {
            await app.ResizeWindowAsync(500, 600);
            await Task.Delay(200);

            NativeMethods.GetWindowRect(hwnd, out var rect);
            Assert.InRange(rect.Right - rect.Left, 496, 504);
            Assert.InRange(rect.Bottom - rect.Top, 596, 604);
        }
        finally
        {
            app.Kill();
            await Task.Delay(500);
        }
    }

    [Fact]
    public async Task SetVisualStateAsync_WhenWindowPatternSupported_ChangesVisualState()
    {
        var (app, hwnd) = await LaunchNotepadAsync();
        try
        {
            await app.SetWindowStateAsync(WindowState.Maximized);
            await Task.Delay(300);
            Assert.True(NativeMethods.IsZoomed(hwnd));

            await app.SetWindowStateAsync(WindowState.Normal);
            await Task.Delay(300);
            Assert.False(NativeMethods.IsZoomed(hwnd));
        }
        finally
        {
            app.Kill();
            await Task.Delay(500);
        }
    }

    [Fact]
    public async Task SetVisualStateAsync_WhenWindowPatternUnsupported_FallsBackToShowWindow()
    {
        var (app, hwnd) = await LaunchCalculatorAsync();
        try
        {
            await app.SetWindowStateAsync(WindowState.Maximized);
            await Task.Delay(300);
            Assert.True(NativeMethods.IsZoomed(hwnd));

            await app.SetWindowStateAsync(WindowState.Normal);
            await Task.Delay(300);
            Assert.False(NativeMethods.IsZoomed(hwnd));
        }
        finally
        {
            app.Kill();
            await Task.Delay(500);
        }
    }

    // Complements WindowCloseIntegrationTests' WM_CLOSE fallback case by exercising Notepad's WindowPattern.Close() path.
    [Fact]
    public async Task CloseAsync_WhenWindowPatternSupported_ClosesWindow()
    {
        var (app, hwnd) = await LaunchNotepadAsync();
        try
        {
            await app.CloseWindowAsync();

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline && NativeMethods.IsWindowVisible(hwnd))
                await Task.Delay(200);

            Assert.False(NativeMethods.IsWindowVisible(hwnd));
        }
        finally
        {
            app.Kill();
            await Task.Delay(500);
        }
    }
}
