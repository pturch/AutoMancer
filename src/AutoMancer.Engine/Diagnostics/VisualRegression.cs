// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.IO;
using System.Windows.Media.Imaging;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Diagnostics;

// Outcome of comparing a capture to a baseline: whether it passed and the fraction of pixels that differed.
public sealed record VisualComparison(bool Passed, double DifferenceRatio);

// Pixel-diffs a PNG capture against a stored baseline PNG recorded at the same DPI.
internal static class VisualRegression
{
    // Compares currentPng to the baseline file, passing when at most maxDifferenceRatio of pixels differ; throws BaselineDpiMismatchError when the two were captured at different DPIs, since scaled pixels never match.
    internal static VisualComparison CompareToBaseline(byte[] currentPng, string baselinePath, double maxDifferenceRatio = 0.01)
    {
        var baselinePng = File.ReadAllBytes(baselinePath);
        var baselineDpi = DpiOf(baselinePng);
        var currentDpi = DpiOf(currentPng);
        // PNG stores DPI as whole pixels per meter, so equal DPIs read back a hair apart.
        if (Math.Abs(baselineDpi - currentDpi) > 0.5)
            throw new BaselineDpiMismatchError(baselineDpi, currentDpi);

        var ratio = PixelDiff.DifferenceRatio(baselinePng, currentPng);
        return new VisualComparison(ratio <= maxDifferenceRatio, ratio);
    }

    // Reads the DPI recorded in a PNG's metadata (X axis; captures are uniform).
    private static double DpiOf(byte[] png)
    {
        using var stream = new MemoryStream(png);
        return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).DpiX;
    }
}
