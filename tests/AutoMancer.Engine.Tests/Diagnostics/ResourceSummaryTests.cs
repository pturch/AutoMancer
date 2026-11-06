// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Tests.Diagnostics;

// Unit tests for ResourceSample.Capture and ResourceSummary's deltas and table.
public sealed class ResourceSummaryTests
{
    // Capturing the test host's own process returns plausible, non-zero counters.
    [Fact]
    public void Capture_CurrentProcess_ReturnsPlausibleValues()
    {
        var sample = ResourceSample.Capture(Environment.ProcessId);

        Assert.True(sample.GdiObjects >= 0 && sample.UserObjects >= 0);
        Assert.True(sample.HandleCount > 0 && sample.ThreadCount > 0);
        Assert.True(sample.WorkingSetBytes > 0 && sample.PrivateBytes > 0);
    }

    // Every delta is after minus before, so growth is positive and shrinkage negative.
    [Fact]
    public void Deltas_AreAfterMinusBefore()
    {
        var summary = new ResourceSummary(new(10, 5, 200, 15, 100, 80), new(12, 4, 230, 15, 150, 120));

        Assert.Equal(2, summary.GdiDelta);
        Assert.Equal(-1, summary.UserDelta);
        Assert.Equal(30, summary.HandleDelta);
        Assert.Equal(0, summary.ThreadDelta);
        Assert.Equal(50, summary.WorkingSetDelta);
        Assert.Equal(40, summary.PrivateDelta);
    }

    // DiffTo treats the receiver as the earlier reading, so swapping the order flips every delta's sign.
    [Fact]
    public void DiffTo_ComparesEarlierToLater()
    {
        ResourceSample early = new(10, 5, 200, 15, 100, 80), late = new(12, 4, 230, 15, 150, 120);

        Assert.Equal(30, early.DiffTo(late).HandleDelta);
        Assert.Equal(-30, late.DiffTo(early).HandleDelta);
    }

    // ToString prints a table with signed deltas so a failure message is readable on its own.
    [Fact]
    public void ToString_ShowsSignedDeltas()
    {
        var text = new ResourceSummary(new(10, 5, 200, 15, 0, 0), new(12, 4, 230, 15, 0, 0)).ToString();

        Assert.Contains("+2", text);
        Assert.Contains("-1", text);
        Assert.Contains("+30", text);
    }
}
