// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Errors;

// Thrown by VisualRegression.CompareToBaseline when the capture and the baseline were taken at different display scales, so their pixels can't be compared.
public sealed class BaselineDpiMismatchError : Exception
{
    public double BaselineDpi { get; }
    public double CurrentDpi { get; }

    // Records both DPIs and builds a message telling the user how to fix it.
    public BaselineDpiMismatchError(double baselineDpi, double currentDpi)
        : base($"The baseline was recorded at {baselineDpi:F0} DPI but this capture is at {currentDpi:F0} DPI. Re-record the baseline at this display scale, or run at the baseline's scale.")
    {
        BaselineDpi = baselineDpi;
        CurrentDpi = currentDpi;
    }
}
