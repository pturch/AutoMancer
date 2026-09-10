// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// Filters candidate elements by BoundingRect proximity and direction relative to an anchor's rect. Operates on already-resolved rects, so it has no provider/session dependency of its own.
public static class SpatialMatcher
{
    // Candidates whose center falls outside this many degrees of the direction's primary axis don't count, even if they clear the half-plane edge check (e.g. "RightOf" excludes a candidate far above-and-right).
    private const double AngularToleranceDegrees = 45.0;

    // Returns the nearest candidate in the given direction from the anchor, within maxDistancePx of its edge; ties broken by center-to-center distance. Null when no candidate qualifies.
    public static ElementHandle? FindNearest(Rect anchorRect, IReadOnlyList<ElementHandle> candidates, SpatialDirection direction, int maxDistancePx)
    {
        ElementHandle? nearest = null;
        var nearestDistance = double.MaxValue;

        foreach (var candidate in candidates)
        {
            if (!Qualifies(anchorRect, candidate.BoundingRect, direction, maxDistancePx))
                continue;

            var distance = CenterDistance(anchorRect, candidate.BoundingRect);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    // A candidate qualifies when it clears the direction's half-plane edge (within maxDistancePx of the anchor's edge) and its center falls within the angular tolerance of that direction's primary axis. Near skips both direction checks and only caps center-to-center distance.
    private static bool Qualifies(Rect anchor, Rect candidate, SpatialDirection direction, int maxDistancePx)
    {
        if (direction == SpatialDirection.Near)
            return CenterDistance(anchor, candidate) <= maxDistancePx;

        var edgeGap = direction switch
        {
            SpatialDirection.Above => anchor.Y - (candidate.Y + candidate.Height),
            SpatialDirection.Below => candidate.Y - (anchor.Y + anchor.Height),
            SpatialDirection.LeftOf => anchor.X - (candidate.X + candidate.Width),
            SpatialDirection.RightOf => candidate.X - (anchor.X + anchor.Width),
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

        if (edgeGap < 0 || edgeGap > maxDistancePx)
            return false;

        return IsWithinAngularTolerance(anchor, candidate, direction);
    }

    // Checks the anchor-to-candidate center vector against the direction's primary axis (0=RightOf, 90=Below, 180=LeftOf, -90=Above in screen coordinates, where Y grows downward).
    private static bool IsWithinAngularTolerance(Rect anchor, Rect candidate, SpatialDirection direction)
    {
        var (anchorX, anchorY) = anchor.Center;
        var (candidateX, candidateY) = candidate.Center;
        var angle = Math.Atan2(candidateY - anchorY, candidateX - anchorX) * 180.0 / Math.PI;

        var targetAngle = direction switch
        {
            SpatialDirection.RightOf => 0.0,
            SpatialDirection.Below => 90.0,
            SpatialDirection.LeftOf => 180.0,
            SpatialDirection.Above => -90.0,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

        var diff = Math.Abs(angle - targetAngle);
        if (diff > 180.0)
            diff = 360.0 - diff;

        return diff <= AngularToleranceDegrees;
    }

    // Straight-line distance between the two rects' midpoints.
    private static double CenterDistance(Rect a, Rect b)
    {
        var (ax, ay) = a.Center;
        var (bx, by) = b.Center;
        return Math.Sqrt(Math.Pow(bx - ax, 2) + Math.Pow(by - ay, 2));
    }
}
