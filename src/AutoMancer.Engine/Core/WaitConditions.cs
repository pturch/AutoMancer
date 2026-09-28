// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.RegularExpressions;

namespace AutoMancer.Engine.Core;

// Canned Func<ElementHandle, bool> factories for App.WaitForAsync, mirroring Selenium's ExpectedConditions.
public static class WaitConditions
{
    // True once the element is neither reported offscreen nor a stand-in for "not found" — the same signal ElementInputHelpers uses to gate interactability.
    public static Func<ElementHandle, bool> IsVisible()
        => e => e.IsOffscreen != true;

    // True once the element's Name equals expected exactly.
    public static Func<ElementHandle, bool> NameEquals(string expected)
        => e => e.Name == expected;

    // True once the element's Name contains expected as a substring.
    public static Func<ElementHandle, bool> NameContains(string expected)
        => e => e.Name?.Contains(expected, StringComparison.Ordinal) == true;

    // True once the element's displayed text (Name, the only text ElementHandle carries synchronously) equals expected exactly.
    public static Func<ElementHandle, bool> TextEquals(string expected)
        => e => e.Name == expected;

    // True once the element currently accepts input.
    public static Func<ElementHandle, bool> IsEnabled()
        => e => e.IsEnabled == true;

    // True once the element's AutomationId equals expected exactly.
    public static Func<ElementHandle, bool> AutomationIdEquals(string expected)
        => e => e.AutomationId == expected;

    // True once the element's ClassName equals expected exactly.
    public static Func<ElementHandle, bool> ClassNameEquals(string expected)
        => e => e.ClassName == expected;

    // True once the element's ControlType equals expected exactly.
    public static Func<ElementHandle, bool> ControlTypeEquals(string expected)
        => e => e.ControlType == expected;

    // True once the element's Name matches pattern.
    public static Func<ElementHandle, bool> TextMatches(Regex pattern)
        => e => e.Name is not null && pattern.IsMatch(e.Name);

    // True once condition is false — for waiting on a spinner/overlay to disappear rather than appear.
    public static Func<ElementHandle, bool> Not(Func<ElementHandle, bool> condition)
        => e => !condition(e);
}
