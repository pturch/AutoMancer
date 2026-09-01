// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Errors;

public sealed class ElementNotFoundErrorTests
{
    private static readonly Locator TestLocator = Locator.ByName("Save");

    [Fact]
    public void Message_NoProvidersNoClosestMatch_OmitsBothClauses()
    {
        var ex = new ElementNotFoundError(TestLocator, [], 1000);

        Assert.Equal(TestLocator, ex.Locator);
        Assert.Empty(ex.AttemptedProviders);
        Assert.Null(ex.ClosestMatch);
        Assert.Equal("Element not found: Name=Save after 1000ms", ex.Message);
    }

    [Fact]
    public void Message_WithAttemptedProviders_ListsThemJoined()
    {
        var ex = new ElementNotFoundError(TestLocator, ["uia3", "uia2", "win32"], 1000);

        Assert.Contains("(tried: uia3, uia2, win32)", ex.Message);
    }

    [Fact]
    public void Message_WithClosestMatch_AppendsDidYouMeanHint()
    {
        var closestMatch = new ClosestMatch("Sve", 0.8);

        var ex = new ElementNotFoundError(TestLocator, ["uia3"], 1000, closestMatch);

        Assert.Same(closestMatch, ex.ClosestMatch);
        Assert.Contains($"Did you mean \"Sve\" (confidence: {0.8:P0})?", ex.Message);
    }
}
