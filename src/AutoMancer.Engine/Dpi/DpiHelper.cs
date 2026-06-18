// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Dpi;

public static class DpiHelper
{
    private const double BaseDpi = 96.0;

    // Converts logical pixel coordinates (96 DPI baseline, relative to window origin) to physical screen coordinates.
    public static (double X, double Y) LogicalToPhysical(double logicalX, double logicalY, int dpi, Rect windowRect)
    {
        double scale = dpi / BaseDpi;
        return (windowRect.X + logicalX * scale, windowRect.Y + logicalY * scale);
    }

    // Converts physical screen coordinates to logical pixel coordinates (96 DPI baseline, relative to window origin).
    public static (double X, double Y) PhysicalToLogical(double physicalX, double physicalY, int dpi, Rect windowRect)
    {
        double scale = dpi / BaseDpi;
        return ((physicalX - windowRect.X) / scale, (physicalY - windowRect.Y) / scale);
    }
}
