// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// Describes what element to find — a strategy plus the value to match against it. Use the factory methods below, not the constructor directly.
public sealed record Locator(LocatorStrategy Strategy, string Value)
{
    // Spatial-strategy only: the element to search relative to.
    internal Locator? Anchor { get; init; }

    // Spatial-strategy only: which side of the anchor to search.
    internal SpatialDirection? Direction { get; init; }

    // Spatial-strategy only: how far from the anchor's edge, in pixels, a candidate can be.
    internal int? MaxDistancePx { get; init; }

    // Property-strategy only: the typed value to match — kept alongside the string-ified Value (used for equality/logging) since CreatePropertyCondition needs the value's actual CLR type (e.g. a boxed bool, not the string "true").
    internal object? PropertyValue { get; init; }

    // Matches the element's visible text or accessible name.
    public static Locator ByName(string name) => new(LocatorStrategy.Name, name);

    // Matches the developer-assigned automation identifier — stable across app relaunches.
    public static Locator ByAutomationId(string id) => new(LocatorStrategy.AutomationId, id);

    // Matches the Win32 window class name (e.g. "Edit", "Button").
    public static Locator ByClassName(string className) => new(LocatorStrategy.ClassName, className);

    // Matches the UIA semantic control type (e.g. "Button", "Edit") regardless of Win32 class.
    public static Locator ByControlType(string controlType) => new(LocatorStrategy.ControlType, controlType);

    // Matches via a tree-traversal path (e.g. "Window > Pane[2] > Button[\"OK\"]").
    public static Locator ByPath(string path) => new(LocatorStrategy.AutoMancerPath, path);

    // Re-finds a specific element by its UIA RuntimeId (e.g. "42.333896.3.1") — the dotted string from ElementHandle.Id.
    public static Locator ByRuntimeId(string runtimeId) => new(LocatorStrategy.RuntimeId, runtimeId);

    // Finds elements via XPath evaluated against the UIA element tree (0-based indices; tags are control types, attrs are UIA properties).
    public static Locator ByXPath(string xpath) => new(LocatorStrategy.AutoMancerXPath, xpath);

    // Finds the nearest element in the given direction from an anchor element, within maxDistancePx.
    public static Locator Near(Locator anchor, SpatialDirection direction, int maxDistancePx = 200) =>
        new(LocatorStrategy.Spatial, Value: string.Empty) { Anchor = anchor, Direction = direction, MaxDistancePx = maxDistancePx };

    // Matches any built-in UIA property by its numeric ID — an escape hatch for properties with no named strategy of their own.
    public static Locator ByProperty(int propertyId, object value) =>
        new(LocatorStrategy.Property, Value: propertyId.ToString()) { PropertyValue = value };

    // Matches a well-known built-in UIA property by its named constant instead of a raw ID.
    public static Locator ByProperty(UiaProperty property, object value) =>
        ByProperty(property.ToPropertyId(), value);
}
