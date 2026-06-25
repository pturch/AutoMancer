// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Dpi;

// Converts between logical (96 DPI baseline) and physical screen coordinates for SendInput targeting.
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

    // Converts logical coordinates relative to hWnd into physical screen coordinates, reading hWnd's live DPI and rect.
    public static (double X, double Y) LogicalToPhysical(double logicalX, double logicalY, IntPtr hWnd) =>
        LogicalToPhysical(logicalX, logicalY, (int)NativeMethods.GetDpiForWindow(hWnd), GetWindowRect(hWnd));

    // Converts physical screen coordinates into logical coordinates relative to hWnd, reading hWnd's live DPI and rect.
    public static (double X, double Y) PhysicalToLogical(double physicalX, double physicalY, IntPtr hWnd) =>
        PhysicalToLogical(physicalX, physicalY, (int)NativeMethods.GetDpiForWindow(hWnd), GetWindowRect(hWnd));

    // Reads hWnd's current screen-space bounding rectangle as AutoMancer's Rect struct.
    private static Rect GetWindowRect(IntPtr hWnd)
    {
        NativeMethods.GetWindowRect(hWnd, out var rect);
        return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
}
