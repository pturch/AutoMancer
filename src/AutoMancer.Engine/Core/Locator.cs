// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// Describes what element to find — a strategy plus the value to match against it. Use the factory methods below, not the constructor directly.
public sealed record Locator(LocatorStrategy Strategy, string Value)
{
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
    public static Locator ByXPath(string xpath) => new(LocatorStrategy.AutomancerXPath, xpath);
}
