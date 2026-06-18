// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Dpi;

namespace AutoMancer.Engine.Tests.Dpi;

public sealed class DpiHelperTests
{
    [Theory]
    [InlineData(96,  10, 20, 100, 50, 110,   70.0)]  // 1.0x — no scaling
    [InlineData(120, 10, 20, 100, 50, 135,   82.5)]  // 1.25x
    [InlineData(144, 10, 20, 100, 50, 160,   95.0)]  // 1.5x
    public void LogicalToPhysical_ScalesAndOffsets(
        int dpi, double wx, double wy, double lx, double ly, double ex, double ey)
    {
        var window = new Rect(wx, wy, 800, 600);

        var (px, py) = DpiHelper.LogicalToPhysical(lx, ly, dpi, window);

        Assert.Equal(ex, px, precision: 5);
        Assert.Equal(ey, py, precision: 5);
    }

    [Theory]
    [InlineData(96,  10, 20, 110,   70.0, 100, 50)]  // 1.0x
    [InlineData(120, 10, 20, 135,   82.5, 100, 50)]  // 1.25x
    [InlineData(144, 10, 20, 160,   95.0, 100, 50)]  // 1.5x
    public void PhysicalToLogical_ScalesAndOffsets(
        int dpi, double wx, double wy, double px, double py, double ex, double ey)
    {
        var window = new Rect(wx, wy, 800, 600);

        var (lx, ly) = DpiHelper.PhysicalToLogical(px, py, dpi, window);

        Assert.Equal(ex, lx, precision: 5);
        Assert.Equal(ey, ly, precision: 5);
    }

    [Fact]
    public void RoundTrip_LogicalToPhysicalToLogical_ReturnsOriginal()
    {
        var window = new Rect(50, 100, 800, 600);
        const int dpi = 120;
        const double lx = 200, ly = 150;

        var (px, py) = DpiHelper.LogicalToPhysical(lx, ly, dpi, window);
        var (rx, ry) = DpiHelper.PhysicalToLogical(px, py, dpi, window);

        Assert.Equal(lx, rx, precision: 5);
        Assert.Equal(ly, ry, precision: 5);
    }
}
