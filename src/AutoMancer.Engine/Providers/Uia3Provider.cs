// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Providers;

// Finds elements via the UIAutomation3 COM API (CUIAutomation8) — the primary, most capable provider in the fallback chain.
public sealed class Uia3Provider : IElementProvider
{
    public string ProviderName => "uia3";

    private const int MaxTreeDepth = 20;

    private static readonly IUIAutomation Automation = new CUIAutomation8Class();

    // AutoMancer's control type names mapped to their UIA_*ControlTypeId constants.
    private static readonly Dictionary<string, int> ControlTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Button"] = UIA_ControlTypeIds.UIA_ButtonControlTypeId,
        ["Calendar"] = UIA_ControlTypeIds.UIA_CalendarControlTypeId,
        ["CheckBox"] = UIA_ControlTypeIds.UIA_CheckBoxControlTypeId,
        ["ComboBox"] = UIA_ControlTypeIds.UIA_ComboBoxControlTypeId,
        ["Edit"] = UIA_ControlTypeIds.UIA_EditControlTypeId,
        ["Hyperlink"] = UIA_ControlTypeIds.UIA_HyperlinkControlTypeId,
        ["Image"] = UIA_ControlTypeIds.UIA_ImageControlTypeId,
        ["ListItem"] = UIA_ControlTypeIds.UIA_ListItemControlTypeId,
        ["List"] = UIA_ControlTypeIds.UIA_ListControlTypeId,
        ["Menu"] = UIA_ControlTypeIds.UIA_MenuControlTypeId,
        ["MenuBar"] = UIA_ControlTypeIds.UIA_MenuBarControlTypeId,
        ["MenuItem"] = UIA_ControlTypeIds.UIA_MenuItemControlTypeId,
        ["ProgressBar"] = UIA_ControlTypeIds.UIA_ProgressBarControlTypeId,
        ["RadioButton"] = UIA_ControlTypeIds.UIA_RadioButtonControlTypeId,
        ["ScrollBar"] = UIA_ControlTypeIds.UIA_ScrollBarControlTypeId,
        ["Slider"] = UIA_ControlTypeIds.UIA_SliderControlTypeId,
        ["Spinner"] = UIA_ControlTypeIds.UIA_SpinnerControlTypeId,
        ["StatusBar"] = UIA_ControlTypeIds.UIA_StatusBarControlTypeId,
        ["Tab"] = UIA_ControlTypeIds.UIA_TabControlTypeId,
        ["TabItem"] = UIA_ControlTypeIds.UIA_TabItemControlTypeId,
        ["Text"] = UIA_ControlTypeIds.UIA_TextControlTypeId,
        ["ToolBar"] = UIA_ControlTypeIds.UIA_ToolBarControlTypeId,
        ["ToolTip"] = UIA_ControlTypeIds.UIA_ToolTipControlTypeId,
        ["Tree"] = UIA_ControlTypeIds.UIA_TreeControlTypeId,
        ["TreeItem"] = UIA_ControlTypeIds.UIA_TreeItemControlTypeId,
        ["Custom"] = UIA_ControlTypeIds.UIA_CustomControlTypeId,
        ["Group"] = UIA_ControlTypeIds.UIA_GroupControlTypeId,
        ["Thumb"] = UIA_ControlTypeIds.UIA_ThumbControlTypeId,
        ["DataGrid"] = UIA_ControlTypeIds.UIA_DataGridControlTypeId,
        ["DataItem"] = UIA_ControlTypeIds.UIA_DataItemControlTypeId,
        ["Document"] = UIA_ControlTypeIds.UIA_DocumentControlTypeId,
        ["SplitButton"] = UIA_ControlTypeIds.UIA_SplitButtonControlTypeId,
        ["Window"] = UIA_ControlTypeIds.UIA_WindowControlTypeId,
        ["Pane"] = UIA_ControlTypeIds.UIA_PaneControlTypeId,
        ["Header"] = UIA_ControlTypeIds.UIA_HeaderControlTypeId,
        ["HeaderItem"] = UIA_ControlTypeIds.UIA_HeaderItemControlTypeId,
        ["Table"] = UIA_ControlTypeIds.UIA_TableControlTypeId,
        ["TitleBar"] = UIA_ControlTypeIds.UIA_TitleBarControlTypeId,
        ["Separator"] = UIA_ControlTypeIds.UIA_SeparatorControlTypeId,
        ["SemanticZoom"] = UIA_ControlTypeIds.UIA_SemanticZoomControlTypeId,
        ["AppBar"] = UIA_ControlTypeIds.UIA_AppBarControlTypeId,
    };

    // Reverse of ControlTypeMap — used to turn a resolved element's numeric control type back into its AutoMancer name.
    private static readonly Dictionary<int, string> ControlTypeNames =
        ControlTypeMap.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);

    // Finds the first descendant of the session's root window matching the locator; null if none match or the strategy is unsupported.
    public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var condition = BuildCondition(locator);
            if (condition is null)
                return (ElementHandle?)null;

            var root = Automation.ElementFromHandle(session.RootWindowHandle);
            var found = root?.FindFirst(TreeScope.TreeScope_Descendants, condition);
            return found is null ? null : Wrap(found);
        }, ct);
    }

    // Finds every descendant of the session's root window matching the locator; empty if none match.
    public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var condition = BuildCondition(locator);
            var root = condition is null ? null : Automation.ElementFromHandle(session.RootWindowHandle);
            if (root is null || condition is null)
                return (IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>();

            var matches = root.FindAll(TreeScope.TreeScope_Descendants, condition);
            var results = new List<ElementHandle>(matches.Length);
            for (var i = 0; i < matches.Length; i++)
                results.Add(Wrap(matches.GetElement(i)));

            return (IReadOnlyList<ElementHandle>)results;
        }, ct);
    }

    // Snapshots the element tree rooted at the session's window, walking up to MaxTreeDepth levels deep.
    public Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var root = Automation.ElementFromHandle(session.RootWindowHandle);
            if (root is null)
                return (IReadOnlyList<ElementSnapshot>)Array.Empty<ElementSnapshot>();

            return (IReadOnlyList<ElementSnapshot>)[WalkTree(root, Automation.ControlViewWalker, 0)];
        }, ct);
    }

    // Recursively builds a snapshot of an element and its children, stopping once MaxTreeDepth is reached.
    private static ElementSnapshot WalkTree(IUIAutomationElement element, IUIAutomationTreeWalker walker, int depth)
    {
        var children = new List<ElementSnapshot>();
        if (depth < MaxTreeDepth)
        {
            var child = walker.GetFirstChildElement(element);
            while (child is not null)
            {
                children.Add(WalkTree(child, walker, depth + 1));
                child = walker.GetNextSiblingElement(child);
            }
        }

        return new ElementSnapshot(
            GetRuntimeId(element),
            element.CurrentName,
            element.CurrentAutomationId,
            element.CurrentClassName,
            ControlTypeNames.GetValueOrDefault(element.CurrentControlType, "Unknown"),
            ToRect(element.CurrentBoundingRectangle),
            children);
    }

    // Builds a UIA property condition for the locator's strategy; null if the strategy or control type name isn't recognized.
    private static IUIAutomationCondition? BuildCondition(Locator locator) => locator.Strategy switch
    {
        LocatorStrategy.Name => Automation.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, locator.Value),
        LocatorStrategy.AutomationId => Automation.CreatePropertyCondition(UIA_PropertyIds.UIA_AutomationIdPropertyId, locator.Value),
        LocatorStrategy.ClassName => Automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ClassNamePropertyId, locator.Value),
        LocatorStrategy.ControlType => ControlTypeMap.TryGetValue(locator.Value, out var controlTypeId)
            ? Automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, controlTypeId)
            : null,
        _ => null,
    };

    // Invokes the element via InvokePattern if it exposes one; returns false when it does not.
    public Task<bool> TryClickAsync(ElementHandle element, CancellationToken ct = default) => Task.Run(() =>
    {
        if (element.NativeHandle is not IUIAutomationElement uiaElement)
            return false;
        if (uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) is not IUIAutomationInvokePattern invoke)
            return false;
        invoke.Invoke();
        return true;
    }, ct);

    // Sets the element's value via ValuePattern if it is supported and not read-only; returns false otherwise.
    public Task<bool> TrySetValueAsync(ElementHandle element, string value, CancellationToken ct = default) => Task.Run(() =>
    {
        if (element.NativeHandle is not IUIAutomationElement uiaElement)
            return false;
        if (uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId) is not IUIAutomationValuePattern valuePattern)
            return false;
        if (valuePattern.CurrentIsReadOnly != 0)
            return false;
        valuePattern.SetValue(value);
        return true;
    }, ct);

    // Wraps a live UIA element as an opaque ElementHandle, capturing its display metadata at resolve time.
    private ElementHandle Wrap(IUIAutomationElement element) => new(GetRuntimeId(element), "uia3", element)
    {
        Name = element.CurrentName,
        AutomationId = element.CurrentAutomationId,
        ClassName = element.CurrentClassName,
        ControlType = ControlTypeNames.GetValueOrDefault(element.CurrentControlType, "Unknown"),
        BoundingRect = ToRect(element.CurrentBoundingRectangle),
        Provider = this,
    };

    // Converts a UIA RuntimeId (an int array, opaque per-session) into the dotted string ID AutoMancer uses for ElementHandle.Id.
    private static string GetRuntimeId(IUIAutomationElement element) => string.Join(".", element.GetRuntimeId());

    // Converts a UIA tagRECT into AutoMancer's Rect struct.
    private static Rect ToRect(tagRECT rect) => new(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
}
