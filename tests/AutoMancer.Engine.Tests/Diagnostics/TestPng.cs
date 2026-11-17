// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AutoMancer.Engine.Tests.Diagnostics;

// Builds tiny synthetic PNGs for tests that need frames to compare.
internal static class TestPng
{
    // Builds a 4x4 PNG filled with one gray, recording dpi in its metadata.
    public static byte[] Gray(byte gray, double dpi = 96) => Halves(4, 4, dpi, gray, gray);

    // Builds a PNG recording dpi whose left half is one gray and right half another, so rescaling has a hard edge to preserve.
    public static byte[] Halves(int width, int height, double dpi, byte left, byte right)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var value = (i / 4 % width) < width / 2 ? left : right;
            pixels[i] = pixels[i + 1] = pixels[i + 2] = value;
            pixels[i + 3] = 0xFF;
        }
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, dpi, dpi, PixelFormats.Bgra32, null, pixels, width * 4)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
