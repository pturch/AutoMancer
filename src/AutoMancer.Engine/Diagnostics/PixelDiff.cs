// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AutoMancer.Engine.Diagnostics;

// Pixel-level comparison of two PNG captures; shared by idle detection and visual regression.
public static class PixelDiff
{
    // Returns the fraction (0..1) of pixels that differ between two PNGs; 1.0 when their dimensions differ.
    public static double DifferenceRatio(byte[] pngA, byte[] pngB)
    {
        var (a, width, height) = Decode(pngA);
        var (b, widthB, heightB) = Decode(pngB);
        if (width != widthB || height != heightB)
            return 1.0;

        var differing = 0;
        for (var pixel = 0; pixel < width * height; pixel++)
        {
            var start = pixel * 4; // each pixel is 4 bytes: blue, green, red, alpha
            var colorA = a.AsSpan(start, 3); // 3 bytes = the color only; alpha is skipped because captures are always opaque
            var colorB = b.AsSpan(start, 3);
            if (!colorA.SequenceEqual(colorB))
                differing++;
        }
        return (double)differing / (width * height);
    }

    // Decodes a PNG into 32bpp BGRA bytes plus its pixel dimensions.
    private static (byte[] Pixels, int Width, int Height) Decode(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var frame = new FormatConvertedBitmap(BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);
        var pixels = new byte[frame.PixelWidth * frame.PixelHeight * 4];
        frame.CopyPixels(pixels, frame.PixelWidth * 4, 0);
        return (pixels, frame.PixelWidth, frame.PixelHeight);
    }
}
