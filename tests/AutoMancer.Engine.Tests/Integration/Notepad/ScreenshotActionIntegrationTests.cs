// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Windows.Media.Imaging;
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies ScreenshotAction captures a live window's pixels as a valid, correctly-sized PNG.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class ScreenshotActionIntegrationTests
{
    // Every PNG file starts with this fixed 8-byte magic number; matching it confirms the captured bytes are a real PNG, not just non-empty.
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    [Fact]
    public async Task CaptureAsync_ReturnsNonEmptyPngMatchingWindowSize()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var windowHandle = Process.GetProcessById(app.ProcessId).MainWindowHandle;
            NativeMethods.GetWindowRect(windowHandle, out var rect);

            var png = await ScreenshotAction.CaptureAsync(windowHandle);

            Assert.NotEmpty(png);
            Assert.Equal(PngSignature, png.Take(8).ToArray());

            using var stream = new MemoryStream(png);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            Assert.Equal(rect.Right - rect.Left, frame.PixelWidth);
            Assert.Equal(rect.Bottom - rect.Top, frame.PixelHeight);
        }
        finally
        {
            await app.KillAsync();
        }
    }
}
