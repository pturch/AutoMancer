// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Core;

// Named constants for built-in UIA properties worth surfacing without a raw property ID; use with Locator.ByProperty(UiaProperty, object).
public enum UiaProperty
{
    HelpText,
    LocalizedControlType,
    IsOffscreen,
    ItemStatus,
    IsContentElement,
    AriaRole,
    AriaProperties,
}

// Maps UiaProperty's named constants to their well-known UIA_PropertyIds integer values.
internal static class UiaPropertyExtensions
{
    private static readonly Dictionary<UiaProperty, int> PropertyIds = new()
    {
        [UiaProperty.HelpText] = UIA_PropertyIds.UIA_HelpTextPropertyId,
        [UiaProperty.LocalizedControlType] = UIA_PropertyIds.UIA_LocalizedControlTypePropertyId,
        [UiaProperty.IsOffscreen] = UIA_PropertyIds.UIA_IsOffscreenPropertyId,
        [UiaProperty.ItemStatus] = UIA_PropertyIds.UIA_ItemStatusPropertyId,
        [UiaProperty.IsContentElement] = UIA_PropertyIds.UIA_IsContentElementPropertyId,
        [UiaProperty.AriaRole] = UIA_PropertyIds.UIA_AriaRolePropertyId,
        [UiaProperty.AriaProperties] = UIA_PropertyIds.UIA_AriaPropertiesPropertyId,
    };

    // Resolves property to its well-known UIA property ID.
    internal static int ToPropertyId(this UiaProperty property) => PropertyIds[property];
}
