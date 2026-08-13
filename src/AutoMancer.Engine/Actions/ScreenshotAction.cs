// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.IO;
using System.Windows.Media.Imaging;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Captures a window's on-screen pixels as a PNG via a GDI block transfer; no external imaging dependencies (WPF's PngBitmapEncoder does the encoding).
public static class ScreenshotAction
{
    // Captures windowHandle's current bounding rectangle from the desktop and returns it PNG-encoded; captures composited/DWM windows correctly because it reads the desktop framebuffer, not the app's own back buffer.
    public static Task<byte[]> CaptureAsync(IntPtr windowHandle, CancellationToken ct = default) => Task.Run(() =>
    {
        NativeMethods.GetWindowRect(windowHandle, out var rect).ThrowIfFailed("GetWindowRect");
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;

        var screenDeviceContext = NativeMethods.GetDeviceContext(IntPtr.Zero);
        var memoryDeviceContext = NativeMethods.CreateCompatibleDeviceContext(screenDeviceContext);
        var bitmap = NativeMethods.CreateCompatibleBitmap(screenDeviceContext, width, height);
        var oldBitmap = NativeMethods.SelectObject(memoryDeviceContext, bitmap);
        try
        {
            NativeMethods.BitBlockTransfer(memoryDeviceContext, 0, 0, width, height, screenDeviceContext, rect.Left, rect.Top, NativeMethods.SrcCopy);

            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                bitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        finally
        {
            NativeMethods.SelectObject(memoryDeviceContext, oldBitmap);
            NativeMethods.DeleteObject(bitmap);
            NativeMethods.DeleteDeviceContext(memoryDeviceContext);
            NativeMethods.ReleaseDeviceContext(IntPtr.Zero, screenDeviceContext);
        }
    }, ct);
}
