// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// The property or mechanism a Locator matches an element against.
public enum LocatorStrategy
{
    Name,
    AutomationId,
    ClassName,
    ControlType,
    AutoMancerPath,
    RuntimeId,
    AutoMancerXPath,
    Spatial,
    Property,
}
