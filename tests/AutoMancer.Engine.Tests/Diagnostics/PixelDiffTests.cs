// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Tests.Diagnostics;

// Verifies PixelDiff's ratio on synthetic PNGs.
public class PixelDiffTests
{
    // Builds a width x height PNG filled with one color, with an optional block of recolored pixels in the top-left corner.
    private static byte[] Png(int width, int height, byte fill, int recoloredPixels = 0)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var value = i / 4 < recoloredPixels ? (byte)(fill ^ 0xFF) : fill;
            pixels[i] = pixels[i + 1] = pixels[i + 2] = value;
            pixels[i + 3] = 0xFF;
        }
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    // Identical images report zero difference.
    [Fact]
    public void DifferenceRatio_IdenticalImages_IsZero()
        => Assert.Equal(0.0, PixelDiff.DifferenceRatio(Png(10, 10, 0x40), Png(10, 10, 0x40)));

    // A quarter of the pixels recolored reports a 0.25 ratio.
    [Fact]
    public void DifferenceRatio_QuarterRecolored_IsQuarter()
        => Assert.Equal(0.25, PixelDiff.DifferenceRatio(Png(10, 10, 0x40), Png(10, 10, 0x40, recoloredPixels: 25)));

    // Differently sized images are treated as entirely different.
    [Fact]
    public void DifferenceRatio_DifferentDimensions_IsOne()
        => Assert.Equal(1.0, PixelDiff.DifferenceRatio(Png(10, 10, 0x40), Png(12, 10, 0x40)));
}
