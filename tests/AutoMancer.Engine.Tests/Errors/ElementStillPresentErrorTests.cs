// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Errors;

public sealed class ElementStillPresentErrorTests
{
    [Fact]
    public void Constructor_SetsLocatorElapsedMsAndMessage()
    {
        var locator = Locator.ByName("Dialog");

        var ex = new ElementStillPresentError(locator, 3000);

        Assert.Equal(locator, ex.Locator);
        Assert.Equal(3000, ex.ElapsedMs);
        Assert.Contains("Name=Dialog", ex.Message);
        Assert.Contains("3000ms", ex.Message);
    }
}
