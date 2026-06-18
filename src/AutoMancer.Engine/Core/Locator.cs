// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

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
}
