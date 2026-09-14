// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Core;

// Pins UiaAutomationType's values to the real native UIAutomationType enum (uiautomationcore.h)
// A silent renumbering here would corrupt every custom property registration.
public sealed class UiaAutomationTypeTests
{
    [Theory]
    [InlineData(UiaAutomationType.Int, 1)]
    [InlineData(UiaAutomationType.Bool, 2)]
    [InlineData(UiaAutomationType.String, 3)]
    [InlineData(UiaAutomationType.Double, 4)]
    [InlineData(UiaAutomationType.Point, 5)]
    [InlineData(UiaAutomationType.Rect, 6)]
    [InlineData(UiaAutomationType.Element, 7)]
    public void Value_MatchesTheNativeUIAutomationTypeConstant(UiaAutomationType type, int nativeValue)
    {
        Assert.Equal(nativeValue, (int)type);
    }
}
