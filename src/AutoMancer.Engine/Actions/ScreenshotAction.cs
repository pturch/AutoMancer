// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.IO;
using System.Windows.Media.Imaging;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Captures a window's on-screen pixels as a PNG via a GDI block transfer; no external imaging dependencies (WPF's PngBitmapEncoder does the encoding).
public static class ScreenshotAction
{
    // Captures windowHandle's current visible frame from the desktop and returns it PNG-encoded; captures composited/DWM windows correctly because it reads the desktop framebuffer, not the app's own back buffer.
    public static Task<byte[]> CaptureAsync(IntPtr windowHandle, CancellationToken ct = default) => Task.Run(() =>
    {
        var rect = VisibleBounds(windowHandle);
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
            // Record the window's DPI in the PNG metadata so visual regression can refuse to compare against a baseline recorded at a different display scale.
            var dpi = (double)NativeMethods.GetDpiForWindow(windowHandle);
            return EncodePng(source, dpi, dpi);
        }
        finally
        {
            NativeMethods.SelectObject(memoryDeviceContext, oldBitmap);
            NativeMethods.DeleteObject(bitmap);
            NativeMethods.DeleteDeviceContext(memoryDeviceContext);
            NativeMethods.ReleaseDeviceContext(IntPtr.Zero, screenDeviceContext);
        }
    }, ct);

    // Captures windowHandle, then crops to screenRect (physical screen coordinates) clamped to the window; throws InvalidOperationException if no part of it lies inside the window.
    public static async Task<byte[]> CaptureRegionAsync(IntPtr windowHandle, Rect screenRect, CancellationToken ct = default)
    {
        var png = await CaptureAsync(windowHandle, ct);
        var window = VisibleBounds(windowHandle);
        var region = RegionInWindow(screenRect, window.Left, window.Top, window.Right, window.Bottom)
            ?? throw new InvalidOperationException($"Region {screenRect} lies outside the window.");
        return Crop(png, region);
    }

    // The window's visible frame in screen coordinates, minus its 1px edge; GetWindowRect also includes Windows 11's invisible resize border, and the edge pixel is semi-transparent, so both blend in whatever is behind the window and change with it.
    internal static NativeMethods.RECT VisibleBounds(IntPtr windowHandle)
    {
        const int DWMWA_EXTENDED_FRAME_BOUNDS = 9; // mirrors the Windows SDK value, so it is searchable there
        if (NativeMethods.DwmGetWindowAttribute(windowHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out var frame, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>()) == 0)
            return new NativeMethods.RECT { Left = frame.Left + 1, Top = frame.Top + 1, Right = frame.Right - 1, Bottom = frame.Bottom - 1 };
        NativeMethods.GetWindowRect(windowHandle, out var rect).ThrowIfFailed("GetWindowRect");
        return rect;
    }

    // Converts a screen-coordinate rect to a pixel region of the window's capture, clamped to the window; null when no part of it lies inside.
    internal static System.Windows.Int32Rect? RegionInWindow(Rect screenRect, int windowLeft, int windowTop, int windowRight, int windowBottom)
    {
        var left = Math.Max((int)Math.Floor(screenRect.X) - windowLeft, 0);
        var top = Math.Max((int)Math.Floor(screenRect.Y) - windowTop, 0);
        var right = Math.Min((int)Math.Ceiling(screenRect.X + screenRect.Width) - windowLeft, windowRight - windowLeft);
        var bottom = Math.Min((int)Math.Ceiling(screenRect.Y + screenRect.Height) - windowTop, windowBottom - windowTop);
        return right <= left || bottom <= top ? null : new System.Windows.Int32Rect(left, top, right - left, bottom - top);
    }

    // Crops a PNG to region, keeping its recorded DPI.
    internal static byte[] Crop(byte[] png, System.Windows.Int32Rect region)
    {
        using var input = new MemoryStream(png);
        var frame = BitmapFrame.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        return EncodePng(new CroppedBitmap(frame, region), frame.DpiX, frame.DpiY);
    }

    // PNG-encodes source's pixels with the given DPI recorded in the metadata (a cropped or captured bitmap carries none of its own).
    private static byte[] EncodePng(BitmapSource source, double dpiX, double dpiY)
    {
        var bytesPerRow = source.PixelWidth * 4;
        var pixels = new byte[bytesPerRow * source.PixelHeight];
        source.CopyPixels(pixels, bytesPerRow, 0);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(source.PixelWidth, source.PixelHeight, dpiX, dpiY, source.Format, null, pixels, bytesPerRow)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
