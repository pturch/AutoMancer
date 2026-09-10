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

    // Near doesn't fit the single-string-Value factory shape — it carries a nested Locator, a direction, and a
    // distance via internal init-only properties instead, so this pins those are readable back off the result.
    [Fact]
    public void Near_SetsAnchorDirectionAndMaxDistance()
    {
        var anchor = Locator.ByName("Username");

        var locator = Locator.Near(anchor, SpatialDirection.RightOf, maxDistancePx: 50);

        Assert.Equal(LocatorStrategy.Spatial, locator.Strategy);
        Assert.Equal(anchor, locator.Anchor);
        Assert.Equal(SpatialDirection.RightOf, locator.Direction);
        Assert.Equal(50, locator.MaxDistancePx);
    }

    // maxDistancePx defaults to 200 when the caller doesn't specify one.
    [Fact]
    public void Near_DefaultsMaxDistancePxTo200()
    {
        var locator = Locator.Near(Locator.ByName("Username"), SpatialDirection.Above);

        Assert.Equal(200, locator.MaxDistancePx);
    }

    // ByProperty stringifies propertyId into Value (for equality/logging) but keeps the typed value separately —
    // CreatePropertyCondition needs the value's actual CLR type, and a boxed bool isn't interchangeable with "True".
    [Fact]
    public void ByProperty_SetsPropertyIdAsValueAndKeepsTypedPropertyValue()
    {
        var locator = Locator.ByProperty(30005, true);

        Assert.Equal(LocatorStrategy.Property, locator.Strategy);
        Assert.Equal("30005", locator.Value);
        Assert.Equal(true, locator.PropertyValue);
        Assert.IsType<bool>(locator.PropertyValue);
    }

    // Locator.ByProperty(UiaProperty, object) is a named-constant convenience over ByProperty(int, object) — same
    // resolved Locator either way, since UIA_HelpTextPropertyId's well-known value is 30013.
    [Fact]
    public void ByProperty_WithUiaProperty_ProducesSameLocatorAsRawId()
    {
        var viaEnum = Locator.ByProperty(UiaProperty.HelpText, "Search");
        var viaRawId = Locator.ByProperty(30013, "Search");

        Assert.Equal(viaRawId, viaEnum);
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
