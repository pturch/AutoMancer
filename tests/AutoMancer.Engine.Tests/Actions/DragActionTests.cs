// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class DragActionTests
{
    [Fact]
    public async Task DragThroughAsync_EmptyWaypoints_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => DragAction.DragThroughAsync([]));

        Assert.Equal("waypoints", ex.ParamName);
    }

    [Fact]
    public async Task DragThroughAsync_SingleWaypoint_ThrowsArgumentException()
        => await Assert.ThrowsAsync<ArgumentException>(() => DragAction.DragThroughAsync([(0, 0)]));

    // steps=0 interpolates to a single point (0/0 == the start), so the same <2-waypoints guard must
    // reject it before any input is sent — this also verifies the interpolation math wasn't off-by-one.
    [Fact]
    public async Task DragAsync_ZeroSteps_ThrowsArgumentException()
        => await Assert.ThrowsAsync<ArgumentException>(() => DragAction.DragAsync(0, 0, 10, 10, steps: 0));
}
