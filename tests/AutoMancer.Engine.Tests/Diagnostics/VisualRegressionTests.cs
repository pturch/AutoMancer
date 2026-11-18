// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Diagnostics;

// Verifies baseline comparison, including refusing a different-DPI capture, on synthetic PNGs.
public class VisualRegressionTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    // Removes the temp baseline directory.
    public void Dispose() => Directory.Delete(_directory, true);

    // Writes png to a baseline file in the temp directory and returns its path.
    private string Baseline(byte[] png)
    {
        var path = Path.Combine(_directory, "baseline.png");
        File.WriteAllBytes(path, png);
        return path;
    }

    // An identical capture passes with zero difference.
    [Fact]
    public void CompareToBaseline_Identical_Passes()
    {
        var png = TestPng.Halves(20, 20, 96, 0x20, 0xC0);
        var result = VisualRegression.CompareToBaseline(png, Baseline(png));
        Assert.True(result.Passed);
        Assert.Equal(0.0, result.DifferenceRatio);
    }

    // A recolored half exceeds the threshold.
    [Fact]
    public void CompareToBaseline_Recolored_Fails()
    {
        var result = VisualRegression.CompareToBaseline(TestPng.Halves(20, 20, 96, 0x20, 0x80), Baseline(TestPng.Halves(20, 20, 96, 0x20, 0xC0)));
        Assert.False(result.Passed);
        Assert.Equal(0.5, result.DifferenceRatio);
    }

    // A shifted edge exceeds a tight threshold but passes a loose one.
    [Fact]
    public void CompareToBaseline_ThresholdControlsVerdict()
    {
        var baseline = Baseline(TestPng.Halves(20, 20, 96, 0x20, 0xC0));
        var shifted = TestPng.Halves(22, 20, 96, 0x20, 0xC0);
        Assert.False(VisualRegression.CompareToBaseline(shifted, baseline, 0.99).Passed); // dimensions differ => ratio 1.0
        var drifted = TestPng.Halves(20, 20, 96, 0x21, 0xC0);
        Assert.False(VisualRegression.CompareToBaseline(drifted, baseline, 0.01).Passed);
        Assert.True(VisualRegression.CompareToBaseline(drifted, baseline, 0.6).Passed);
    }

    // A capture at a different DPI than the baseline is refused with a message naming both, rather than scored as a 100% difference.
    [Fact]
    public void CompareToBaseline_DifferentDpi_Throws()
    {
        var baseline = Baseline(TestPng.Halves(20, 20, 96, 0x20, 0xC0));
        var error = Assert.Throws<BaselineDpiMismatchError>(() => VisualRegression.CompareToBaseline(TestPng.Halves(30, 30, 144, 0x20, 0xC0), baseline));
        Assert.Equal((96.0, 144.0), (Math.Round(error.BaselineDpi), Math.Round(error.CurrentDpi)));
        Assert.Contains("96 DPI", error.Message);
        Assert.Contains("144 DPI", error.Message);
    }

    // DPIs within PNG's whole-pixels-per-meter rounding of each other count as the same.
    [Fact]
    public void CompareToBaseline_DpiWithinRounding_Compares()
    {
        var baseline = Baseline(TestPng.Halves(20, 20, 96, 0x20, 0xC0));
        Assert.Equal(0.0, VisualRegression.CompareToBaseline(TestPng.Halves(20, 20, 96.3, 0x20, 0xC0), baseline).DifferenceRatio);
    }

    // Cropping to the right half yields exactly the right-half color at the right size, and keeps the recorded DPI.
    [Fact]
    public void Crop_RightHalf_KeepsOnlyThatRegionAndDpi()
    {
        var cropped = ScreenshotAction.Crop(TestPng.Halves(20, 20, 144, 0x20, 0xC0), new System.Windows.Int32Rect(10, 0, 10, 20));
        using var stream = new MemoryStream(cropped);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.Equal((10, 20), (frame.PixelWidth, frame.PixelHeight));
        Assert.InRange(frame.DpiX, 143.9, 144.1); // PNG stores pixels per meter, so DPI round-trips with a small rounding error.
        Assert.Equal(0.0, PixelDiff.DifferenceRatio(cropped, TestPng.Halves(10, 20, 144, 0xC0, 0xC0)));
    }

    // A rect fully inside the window maps to window-relative pixels, rounding outward to whole pixels.
    [Fact]
    public void RegionInWindow_InsideWindow_IsWindowRelative()
    {
        var region = ScreenshotAction.RegionInWindow(new Rect(110.4, 205.2, 30.2, 10.5), 100, 200, 400, 500);
        Assert.Equal(new System.Windows.Int32Rect(10, 5, 31, 11), region);
    }

    // A rect hanging off the window's edges is clamped to the window.
    [Fact]
    public void RegionInWindow_PartlyOutside_IsClamped()
    {
        var region = ScreenshotAction.RegionInWindow(new Rect(50, 450, 100, 100), 100, 200, 400, 500);
        Assert.Equal(new System.Windows.Int32Rect(0, 250, 50, 50), region);
    }

    // A rect entirely outside the window, or touching it only along an edge, has no region.
    [Fact]
    public void RegionInWindow_OutsideOrEdgeOnly_IsNull()
    {
        Assert.Null(ScreenshotAction.RegionInWindow(new Rect(500, 600, 10, 10), 100, 200, 400, 500));
        Assert.Null(ScreenshotAction.RegionInWindow(new Rect(0, 250, 100, 10), 100, 200, 400, 500));
    }
}
