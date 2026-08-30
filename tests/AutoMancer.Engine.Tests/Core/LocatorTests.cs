// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Core;

public sealed class LocatorTests
{
    // Every factory is a one-line pass-through to the constructor; the only way one can be wrong is
    // picking the wrong LocatorStrategy (e.g. a copy-paste slip), which this pins per factory.
    public static IEnumerable<object[]> Factories { get; } = new[]
    {
        new object[] { (Func<string, Locator>)Locator.ByName, LocatorStrategy.Name },
        new object[] { (Func<string, Locator>)Locator.ByAutomationId, LocatorStrategy.AutomationId },
        new object[] { (Func<string, Locator>)Locator.ByClassName, LocatorStrategy.ClassName },
        new object[] { (Func<string, Locator>)Locator.ByControlType, LocatorStrategy.ControlType },
        new object[] { (Func<string, Locator>)Locator.ByPath, LocatorStrategy.AutoMancerPath },
        new object[] { (Func<string, Locator>)Locator.ByRuntimeId, LocatorStrategy.RuntimeId },
        new object[] { (Func<string, Locator>)Locator.ByXPath, LocatorStrategy.AutoMancerXPath },
    };

    [Theory]
    [MemberData(nameof(Factories))]
    public void Factory_SetsExpectedStrategyAndValue(Func<string, Locator> factory, LocatorStrategy expectedStrategy)
    {
        var locator = factory("value");

        Assert.Equal(expectedStrategy, locator.Strategy);
        Assert.Equal("value", locator.Value);
    }

    // Locator is a record — equality and inequality both follow structurally from Strategy+Value, not reference
    // identity. ElementResolverTests' Mock<IElementProvider> setups match calls by Locator equality, so this
    // pins the assumption those tests quietly depend on.
    [Fact]
    public void Equality_IsStructural()
    {
        Assert.Equal(Locator.ByName("File"), Locator.ByName("File"));
        Assert.NotEqual(Locator.ByName("File"), Locator.ByName("Edit"));
        Assert.NotEqual(Locator.ByName("File"), Locator.ByAutomationId("File"));
    }
}
