// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Core;

public sealed class SpatialMatcherTests
{
    private static readonly Rect Anchor = new(100, 100, 50, 20);

    private static ElementHandle Candidate(string id, Rect rect) =>
        new(id, "test", new object()) { BoundingRect = rect };

    [Fact]
    public void FindNearest_CandidateDirectlyRightOfAnchor_Matches()
    {
        var candidate = Candidate("right", new Rect(Anchor.X + Anchor.Width + 10, Anchor.Y, 50, 20));

        var result = SpatialMatcher.FindNearest(Anchor, new[] { candidate }, SpatialDirection.RightOf, maxDistancePx: 200);

        Assert.Same(candidate, result);
    }

    [Fact]
    public void FindNearest_CandidateAboveAndRight_DoesNotMatchRightOfPastAngularTolerance()
    {
        // Offset far more vertically than horizontally from the anchor's center — well outside RightOf's angular tolerance.
        var candidate = Candidate("above-right", new Rect(Anchor.X + Anchor.Width + 20, Anchor.Y - 200, 50, 20));

        var result = SpatialMatcher.FindNearest(Anchor, new[] { candidate }, SpatialDirection.RightOf, maxDistancePx: 300);

        Assert.Null(result);
    }

    [Fact]
    public void FindNearest_CandidateBeyondMaxDistancePx_IsExcluded()
    {
        var candidate = Candidate("far", new Rect(Anchor.X + Anchor.Width + 500, Anchor.Y, 50, 20));

        var result = SpatialMatcher.FindNearest(Anchor, new[] { candidate }, SpatialDirection.RightOf, maxDistancePx: 200);

        Assert.Null(result);
    }

    [Fact]
    public void FindNearest_TwoValidCandidates_NearestByCenterDistanceWins()
    {
        var near = Candidate("near", new Rect(Anchor.X + Anchor.Width + 10, Anchor.Y, 50, 20));
        var far = Candidate("far", new Rect(Anchor.X + Anchor.Width + 100, Anchor.Y, 50, 20));

        var result = SpatialMatcher.FindNearest(Anchor, new[] { far, near }, SpatialDirection.RightOf, maxDistancePx: 200);

        Assert.Same(near, result);
    }

    [Fact]
    public void FindNearest_NoQualifyingCandidates_ReturnsNull()
    {
        var candidate = Candidate("left", new Rect(Anchor.X - 100, Anchor.Y, 50, 20));

        var result = SpatialMatcher.FindNearest(Anchor, new[] { candidate }, SpatialDirection.RightOf, maxDistancePx: 200);

        Assert.Null(result);
    }

    [Fact]
    public void FindNearest_Near_IgnoresDirectionAndUsesDistanceOnly()
    {
        var above = Candidate("above", new Rect(Anchor.X, Anchor.Y - 30, 50, 20));

        var result = SpatialMatcher.FindNearest(Anchor, new[] { above }, SpatialDirection.Near, maxDistancePx: 100);

        Assert.Same(above, result);
    }
}
