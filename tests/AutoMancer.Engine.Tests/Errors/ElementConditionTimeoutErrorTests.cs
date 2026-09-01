// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Errors;

public sealed class ElementConditionTimeoutErrorTests
{
    [Fact]
    public void Constructor_SetsLocatorElapsedMsAndMessage()
    {
        var locator = Locator.ByName("Save");

        var ex = new ElementConditionTimeoutError(locator, 5000);

        Assert.Equal(locator, ex.Locator);
        Assert.Equal(5000, ex.ElapsedMs);
        Assert.Contains("Name=Save", ex.Message);
        Assert.Contains("5000ms", ex.Message);
    }
}
