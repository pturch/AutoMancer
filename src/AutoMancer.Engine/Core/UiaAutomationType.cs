// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// A custom UIA property's declared value type, passed to IUIAutomationRegistrar.RegisterProperty so it can resolve the property's GUID to a numeric PropertyId.
// Needed because UI Automation Core can't infer a type from a bare GUID — every member's value matches the native UIAutomationType enum (uiautomationcore.h) exactly, since it's marshaled straight into the registration struct.
public enum UiaAutomationType
{
    Int = 1,
    Bool = 2,
    String = 3,
    Double = 4,
    Point = 5,
    Rect = 6,
    Element = 7,
}
