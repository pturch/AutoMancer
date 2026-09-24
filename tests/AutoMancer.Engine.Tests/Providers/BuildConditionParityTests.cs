// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Providers;

// Uia3Provider and Uia2Provider each maintain their own BuildCondition switch over LocatorStrategy — different COM/managed APIs, so the logic can't be shared outright, but nothing stops the two from silently drifting (one wiring a strategy the other leaves unhandled). This pins that they agree on every strategy: same verdict on whether a condition is produced at all. It doesn't assert the resulting condition matches the same live elements — that's covered by the per-strategy live integration tests.
public sealed class BuildConditionParityTests
{
    public static IEnumerable<object[]> Locators { get; } = new[]
    {
        new object[] { Locator.ByName("File"), true },
        new object[] { Locator.ByAutomationId("File"), true },
        new object[] { Locator.ByClassName("Edit"), true },
        new object[] { Locator.ByControlType("Button"), true },
        new object[] { Locator.ByControlType("NotARealControlType"), false },
        new object[] { Locator.ByRuntimeId("42.1.2"), true },
        new object[] { Locator.ByRuntimeId("not-a-runtime-id"), false },
        new object[] { Locator.ByProperty(30005, "File"), true }, // UIA_NamePropertyId — a real, resolvable property ID
        new object[] { new Locator(LocatorStrategy.Property, "not-an-int"), false },
        new object[] { Locator.ByPath("Window > Pane"), false }, // UIA3-only, handled by tree-walking before BuildCondition is ever reached
        new object[] { Locator.ByXPath("//Button"), false }, // same — UIA3-only
        new object[] { Locator.Near(Locator.ByName("Five"), SpatialDirection.RightOf), false }, // intercepted by ElementResolver before reaching any provider
    };

    [Theory]
    [MemberData(nameof(Locators))]
    public void BuildCondition_Uia3AndUia2Agree_OnWhetherAConditionIsProduced(Locator locator, bool expectCondition)
    {
        var uia3Condition = Uia3Provider.BuildCondition(locator);
        var uia2Condition = Uia2Provider.BuildCondition(locator);

        Assert.Equal(expectCondition, uia3Condition is not null);
        Assert.Equal(expectCondition, uia2Condition is not null);
    }
}
