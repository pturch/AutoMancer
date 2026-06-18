// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Tests.Diagnostics;

public sealed class ClosestMatchFinderTests
{
    private static ElementSnapshot Snap(string name) =>
        new("id", name, null, null, null, default, Array.Empty<ElementSnapshot>());

    [Fact]
    public void ExactMatch_ReturnsConfidenceOne()
    {
        var result = ClosestMatchFinder.Find(Locator.ByName("Submit"), [Snap("Submit")]);

        Assert.NotNull(result);
        Assert.Equal("Submit", result.ElementName);
        Assert.Equal(1.0, result.Confidence, precision: 5);
    }

    [Fact]
    public void Typo_ReturnsAboveThreshold()
    {
        // "Submt" vs "Submit": distance=1, maxLen=6, similarity≈0.833
        var result = ClosestMatchFinder.Find(Locator.ByName("Submt"), [Snap("Submit")]);

        Assert.NotNull(result);
        Assert.True(result.Confidence >= 0.70);
    }

    [Fact]
    public void UnrelatedName_ReturnsNull()
    {
        // "abc" vs "Submit": similarity≈0.17, below the 0.70 threshold
        var result = ClosestMatchFinder.Find(Locator.ByName("abc"), [Snap("Submit")]);

        Assert.Null(result);
    }

    [Fact]
    public void EmptyTree_ReturnsNull()
    {
        var result = ClosestMatchFinder.Find(Locator.ByName("Submit"), []);

        Assert.Null(result);
    }

    [Fact]
    public void CaseInsensitive_MatchesRegardlessOfCase()
    {
        // "submit" and "Submit" are identical when lowercased → confidence 1.0
        var result = ClosestMatchFinder.Find(Locator.ByName("submit"), [Snap("Submit")]);

        Assert.NotNull(result);
        Assert.Equal("Submit", result.ElementName);
        Assert.Equal(1.0, result.Confidence, precision: 5);
    }

    [Fact]
    public void MultipleElements_ReturnsBestMatch()
    {
        // "Submt" is closer to "Submit" (distance 1) than to "Cancel" (distance 5)
        var result = ClosestMatchFinder.Find(Locator.ByName("Submt"), [Snap("Cancel"), Snap("Submit")]);

        Assert.NotNull(result);
        Assert.Equal("Submit", result.ElementName);
    }

    [Fact]
    public void NullName_IsSkipped()
    {
        var noName = new ElementSnapshot("id", null, null, null, null, default, Array.Empty<ElementSnapshot>());
        var result = ClosestMatchFinder.Find(Locator.ByName("Submit"), [noName]);

        Assert.Null(result);
    }

    [Fact]
    public void NestedChild_IsSearched()
    {
        var child = Snap("Submit");
        var parent = new ElementSnapshot("root", "Window", null, null, null, default, [child]);

        var result = ClosestMatchFinder.Find(Locator.ByName("Submit"), [parent]);

        Assert.NotNull(result);
        Assert.Equal("Submit", result.ElementName);
    }
}
