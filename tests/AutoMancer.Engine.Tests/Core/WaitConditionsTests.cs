// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.RegularExpressions;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Core;

public sealed class WaitConditionsTests
{
    private static ElementHandle Element(string? name = null, bool? isEnabled = null, bool? isOffscreen = null,
        string? automationId = null, string? className = null, string? controlType = null) =>
        new("id", "test", new object())
        {
            Name = name, IsEnabled = isEnabled, IsOffscreen = isOffscreen,
            AutomationId = automationId, ClassName = className, ControlType = controlType,
        };

    [Fact]
    public void IsVisible_NotOffscreen_ReturnsTrue()
        => Assert.True(WaitConditions.IsVisible()(Element(isOffscreen: false)));

    [Fact]
    public void IsVisible_Offscreen_ReturnsFalse()
        => Assert.False(WaitConditions.IsVisible()(Element(isOffscreen: true)));

    [Fact]
    public void NameEquals_ExactMatch_ReturnsTrue()
        => Assert.True(WaitConditions.NameEquals("Save")(Element(name: "Save")));

    [Fact]
    public void NameEquals_DifferentName_ReturnsFalse()
        => Assert.False(WaitConditions.NameEquals("Save")(Element(name: "Cancel")));

    [Fact]
    public void NameContains_Substring_ReturnsTrue()
        => Assert.True(WaitConditions.NameContains("av")(Element(name: "Save")));

    [Fact]
    public void NameContains_NotPresent_ReturnsFalse()
        => Assert.False(WaitConditions.NameContains("xyz")(Element(name: "Save")));

    [Fact]
    public void NameContains_NullName_ReturnsFalse()
        => Assert.False(WaitConditions.NameContains("av")(Element(name: null)));

    [Fact]
    public void TextEquals_ExactMatch_ReturnsTrue()
        => Assert.True(WaitConditions.TextEquals("Save")(Element(name: "Save")));

    [Fact]
    public void TextEquals_DifferentText_ReturnsFalse()
        => Assert.False(WaitConditions.TextEquals("Save")(Element(name: "Cancel")));

    [Fact]
    public void IsEnabled_True_ReturnsTrue()
        => Assert.True(WaitConditions.IsEnabled()(Element(isEnabled: true)));

    [Fact]
    public void IsEnabled_FalseOrNull_ReturnsFalse()
    {
        Assert.False(WaitConditions.IsEnabled()(Element(isEnabled: false)));
        Assert.False(WaitConditions.IsEnabled()(Element(isEnabled: null)));
    }

    [Fact]
    public void AutomationIdEquals_ExactMatch_ReturnsTrue()
        => Assert.True(WaitConditions.AutomationIdEquals("btnSave")(Element(automationId: "btnSave")));

    [Fact]
    public void AutomationIdEquals_DifferentId_ReturnsFalse()
        => Assert.False(WaitConditions.AutomationIdEquals("btnSave")(Element(automationId: "btnCancel")));

    [Fact]
    public void ClassNameEquals_ExactMatch_ReturnsTrue()
        => Assert.True(WaitConditions.ClassNameEquals("Button")(Element(className: "Button")));

    [Fact]
    public void ClassNameEquals_DifferentClass_ReturnsFalse()
        => Assert.False(WaitConditions.ClassNameEquals("Button")(Element(className: "Edit")));

    [Fact]
    public void ControlTypeEquals_ExactMatch_ReturnsTrue()
        => Assert.True(WaitConditions.ControlTypeEquals("Button")(Element(controlType: "Button")));

    [Fact]
    public void ControlTypeEquals_DifferentType_ReturnsFalse()
        => Assert.False(WaitConditions.ControlTypeEquals("Button")(Element(controlType: "Edit")));

    [Fact]
    public void TextMatches_PatternMatches_ReturnsTrue()
        => Assert.True(WaitConditions.TextMatches(new Regex(@"^\d+ items$"))(Element(name: "42 items")));

    [Fact]
    public void TextMatches_PatternDoesNotMatch_ReturnsFalse()
        => Assert.False(WaitConditions.TextMatches(new Regex(@"^\d+ items$"))(Element(name: "no items yet")));

    [Fact]
    public void TextMatches_NullName_ReturnsFalse()
        => Assert.False(WaitConditions.TextMatches(new Regex(".*"))(Element(name: null)));

    [Fact]
    public void Not_WrapsTrueCondition_ReturnsFalse()
        => Assert.False(WaitConditions.Not(WaitConditions.IsVisible())(Element(isOffscreen: false)));

    [Fact]
    public void Not_WrapsFalseCondition_ReturnsTrue()
        => Assert.True(WaitConditions.Not(WaitConditions.IsVisible())(Element(isOffscreen: true)));
}
