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

    // Launches a fresh Notepad instance, resolves its window handle for direct Win32 assertions, and clears any text left over from a reused single-instance window.
    private static async Task<(App App, IntPtr WindowHandle)> LaunchNotepadAsync()
    {
        var app = await App.LaunchAsync("notepad.exe");
        await NotepadFixture.ClearDocumentAsync(app);
        return (app, Process.GetProcessById(app.ProcessId).MainWindowHandle);
    }

    // Launches a fresh Calculator instance and resolves its window handle for direct Win32 assertions.
    private static async Task<(App App, IntPtr WindowHandle)> LaunchCalculatorAsync()
    {
        var app = await App.LaunchPackagedAsync(CalculatorAumid);
        // Grab the handle immediately; waiting first can catch it mid-transition and read back stale/zero.
        var windowHandle = Process.GetProcessById(app.ProcessId).MainWindowHandle;
        await Task.Delay(500); // let the launch entry animation settle before we act on it
        return (app, windowHandle);
    }

    [Fact]
    public async Task MoveAsync_WhenTransformPatternSupported_MovesWindow()
    {
        var (app, windowHandle) = await LaunchNotepadAsync();
        try
        {
            await app.MoveWindowAsync(120, 90);
            await Task.Delay(200);

            NativeMethods.GetWindowRect(windowHandle, out var rect);
            Assert.InRange(rect.Left, 118, 122);
            Assert.InRange(rect.Top, 88, 92);
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
            await Task.Delay(200);

            // DPI virtualization offsets X/Y here (width/height are exact), so just check for a large move.
            NativeMethods.GetWindowRect(windowHandle, out var after);
            Assert.True(Math.Abs(after.Left - before.Left) > 100 || Math.Abs(after.Top - before.Top) > 100,
                $"Expected a large position change from ({before.Left},{before.Top}), got ({after.Left},{after.Top}).");
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
            await Task.Delay(200);

            NativeMethods.GetWindowRect(windowHandle, out var rect);
            Assert.InRange(rect.Right - rect.Left, 636, 644);
            Assert.InRange(rect.Bottom - rect.Top, 476, 484);
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
            await Task.Delay(200);

            NativeMethods.GetWindowRect(windowHandle, out var rect);
            Assert.InRange(rect.Right - rect.Left, 496, 504);
            Assert.InRange(rect.Bottom - rect.Top, 596, 604);
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
            await Task.Delay(300);
            Assert.True(NativeMethods.IsZoomed(windowHandle));

            await app.SetWindowStateAsync(WindowState.Normal);
            await Task.Delay(300);
            Assert.False(NativeMethods.IsZoomed(windowHandle));
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
            await Task.Delay(300);
            Assert.True(NativeMethods.IsZoomed(windowHandle));

            await app.SetWindowStateAsync(WindowState.Normal);
            await Task.Delay(300);
            Assert.False(NativeMethods.IsZoomed(windowHandle));
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

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline && NativeMethods.IsWindowVisible(windowHandle))
                await Task.Delay(200);

            Assert.False(NativeMethods.IsWindowVisible(windowHandle));
        }
        finally
        {
            await app.KillAsync();
        }
    }
}
