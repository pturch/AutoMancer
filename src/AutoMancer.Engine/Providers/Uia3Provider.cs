// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Operators;
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

    // Resolves the root element for the session's window; null if the handle is invalid or the window has been destroyed — never throws (ElementFromHandle itself can throw COMException for a destroyed HWND).
    private static IUIAutomationElement? TryGetRoot(AppSession session)
    {
        try { return Automation.ElementFromHandle(session.RootWindowHandle); }
        catch (COMException) { return null; }
    }

    // Finds the first descendant of the session's root window matching the locator; null if none match or the strategy is unsupported.
    public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (locator.Strategy == LocatorStrategy.AutoMancerXPath)
                return FindByXPath(locator.Value, session);
            if (locator.Strategy == LocatorStrategy.AutoMancerPath)
                return FindByPath(locator.Value, session);

            var condition = BuildCondition(locator);
            if (condition is null)
                return (ElementHandle?)null;

            var root = TryGetRoot(session);
            if (root is null) return (ElementHandle?)null;

            // A stale element mid-search can abort FindFirst entirely — treat that as not-found instead of throwing.
            try
            {
                var found = root.FindFirst(TreeScope.TreeScope_Descendants, condition);
                if (found is null)
                    return null; // no element in the tree matched the locator

                return Wrap(found); // a match was located, but Wrap can still be null if it went stale before we could read it
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                return null;
            }
        }, ct);
    }

    // Finds every descendant of the session's root window matching the locator; empty if none match or a dynamic app invalidates an element mid-search.
    public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (locator.Strategy == LocatorStrategy.AutoMancerXPath)
                return (IReadOnlyList<ElementHandle>)FindAllByXPath(locator.Value, session);
            if (locator.Strategy == LocatorStrategy.AutoMancerPath)
                return (IReadOnlyList<ElementHandle>)FindAllByPath(locator.Value, session);

            var condition = BuildCondition(locator);
            if (condition is null)
                return (IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>();

            var root = TryGetRoot(session);
            if (root is null)
                return (IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>();

            try
            {
                var matches = root.FindAll(TreeScope.TreeScope_Descendants, condition);
                var results = new List<ElementHandle>(matches.Length);
                for (var i = 0; i < matches.Length; i++)
                    if (Wrap(matches.GetElement(i)) is { } wrapped)
                        results.Add(wrapped);

                return (IReadOnlyList<ElementHandle>)results;
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                return (IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>();
            }
        }, ct);
    }

    // Snapshots the tree, evaluates the XPath expression, then re-walks the live tree to find the first matching element.
    private ElementHandle? FindByXPath(string xpath, AppSession session)
    {
        var root = TryGetRoot(session);
        if (root is null) return null;
        var snapshot = WalkTree(root, Automation.ControlViewWalker, 0);
        var indices = XPathEvaluator.Evaluate(xpath, [snapshot]);
        if (indices.Count == 0) return null;
        var matched = CollectElements(root, [.. indices]);
        return matched.Count > 0 ? Wrap(matched[0]) : null;
    }

    // Snapshots the tree, evaluates the XPath expression, then re-walks the live tree to find all matching elements.
    private List<ElementHandle> FindAllByXPath(string xpath, AppSession session)
    {
        var root = TryGetRoot(session);
        if (root is null) return [];
        var snapshot = WalkTree(root, Automation.ControlViewWalker, 0);
        var indices = XPathEvaluator.Evaluate(xpath, [snapshot]);
        if (indices.Count == 0) return [];
        return CollectElements(root, [.. indices]).Select(Wrap).OfType<ElementHandle>().ToList();
    }

    // Resolves a tree-path locator ("Window > Pane[2] > Button[\"OK\"]") by walking live children segment by segment; returns the first match at the deepest level.
    private ElementHandle? FindByPath(string path, AppSession session)
    {
        var candidates = ResolvePathCandidates(AutoMancerPathParser.Parse(path), session);
        return candidates.Count > 0 ? Wrap(candidates[0]) : null;
    }

    // Resolves a tree-path locator like FindByPath, returning every match at the deepest level.
    private List<ElementHandle> FindAllByPath(string path, AppSession session)
    {
        var candidates = ResolvePathCandidates(AutoMancerPathParser.Parse(path), session);
        return candidates.Select(Wrap).OfType<ElementHandle>().ToList();
    }

    // Walks the tree from the root (which the first segment must match) through each later segment against direct children of every prior match — all matches when Index is unset, only the one at Index when it is.
    private static List<IUIAutomationElement> ResolvePathCandidates(IReadOnlyList<PathSegment> segments, AppSession session)
    {
        var root = TryGetRoot(session);
        if (root is null || segments.Count == 0 || !SegmentMatches(root, segments[0]))
            return [];

        var candidates = new List<IUIAutomationElement> { root };
        for (var i = 1; i < segments.Count && candidates.Count > 0; i++)
        {
            var segment = segments[i];
            var next = new List<IUIAutomationElement>();
            foreach (var candidate in candidates)
            {
                var matches = ChildrenMatching(candidate, segment);
                if (segment.Index is int index)
                {
                    if (index >= 0 && index < matches.Count) next.Add(matches[index]);
                }
                else
                {
                    next.AddRange(matches);
                }
            }
            candidates = next;
        }
        return candidates;
    }

    // Returns true when element's control type (or "*" wildcard) and optional Name both satisfy segment.
    private static bool SegmentMatches(IUIAutomationElement element, PathSegment segment)
    {
        if (segment.ControlType != "*" &&
            !string.Equals(ControlTypeNames.GetValueOrDefault(element.CurrentControlType, "Unknown"), segment.ControlType, StringComparison.OrdinalIgnoreCase))
            return false;
        return segment.Name is null || element.CurrentName == segment.Name;
    }

    // Returns parent's direct children whose control type/name satisfy segment, in tree order (defensive reads — a dynamic app can invalidate a child mid-walk).
    private static List<IUIAutomationElement> ChildrenMatching(IUIAutomationElement parent, PathSegment segment)
    {
        var result = new List<IUIAutomationElement>();
        IUIAutomationElement? child = null;
        try { child = Automation.ControlViewWalker.GetFirstChildElement(parent); } catch (Exception ex) when (ex is COMException or InvalidOperationException) { return result; }
        while (child is not null)
        {
            try { if (SegmentMatches(child, segment)) result.Add(child); } catch (Exception ex) when (ex is COMException or InvalidOperationException) { }
            IUIAutomationElement? next = null;
            try { next = Automation.ControlViewWalker.GetNextSiblingElement(child); } catch (Exception ex) when (ex is COMException or InvalidOperationException) { }
            child = next;
        }
        return result;
    }

    // Walks the live UIA tree depth-first and returns the elements whose flat indices are in targetIndices.
    private static List<IUIAutomationElement> CollectElements(IUIAutomationElement root, HashSet<int> targetIndices)
    {
        var results = new List<IUIAutomationElement>();
        int idx = 0;
        CollectWalk(root, Automation.ControlViewWalker, targetIndices, results, ref idx, 0);
        return results;
    }

    // Recursive depth-first walk matching the snapshot order in WalkTree; adds elements whose flat index is a target (defensive reads — a dynamic app can invalidate an element mid-walk).
    private static void CollectWalk(IUIAutomationElement element, IUIAutomationTreeWalker walker,
        HashSet<int> targets, List<IUIAutomationElement> results, ref int idx, int depth)
    {
        if (targets.Contains(idx))
            results.Add(element);
        idx++;
        if (depth >= MaxTreeDepth) return;
        IUIAutomationElement? child = null;
        try { child = walker.GetFirstChildElement(element); } catch (Exception ex) when (ex is COMException or InvalidOperationException) { }
        while (child is not null)
        {
            try { CollectWalk(child, walker, targets, results, ref idx, depth + 1); } catch (Exception ex) when (ex is COMException or InvalidOperationException) { }
            IUIAutomationElement? next = null;
            try { next = walker.GetNextSiblingElement(child); } catch (Exception ex) when (ex is COMException or InvalidOperationException) { }
            child = next;
        }
    }

    // Snapshots the element tree rooted at the session's window, walking up to MaxTreeDepth levels deep.
    public Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var root = TryGetRoot(session);
            if (root is null)
                return (IReadOnlyList<ElementSnapshot>)Array.Empty<ElementSnapshot>();

            return (IReadOnlyList<ElementSnapshot>)[WalkTree(root, Automation.ControlViewWalker, 0)];
        }, ct);
    }

    // Recursively builds a snapshot of an element and its children (defensive reads, since dynamic apps like Task Manager can invalidate elements mid-walk), stopping at MaxTreeDepth.
    private static ElementSnapshot WalkTree(IUIAutomationElement element, IUIAutomationTreeWalker walker, int depth)
    {
        var children = new List<ElementSnapshot>();
        if (depth < MaxTreeDepth)
        {
            IUIAutomationElement? child = null;
            try { child = walker.GetFirstChildElement(element); } catch { }
            while (child is not null)
            {
                try { children.Add(WalkTree(child, walker, depth + 1)); } catch { }
                IUIAutomationElement? next = null;
                try { next = walker.GetNextSiblingElement(child); } catch { }
                child = next;
            }
        }

        // Read all properties in one block; if the element goes stale partway through, use whatever was captured.
        string id = "", name = "", automationId = "", className = "";
        int controlTypeId = 0;
        tagRECT rect = default;
        try
        {
            id = GetRuntimeId(element);
            name = element.CurrentName;
            automationId = element.CurrentAutomationId;
            className = element.CurrentClassName;
            controlTypeId = element.CurrentControlType;
            rect = element.CurrentBoundingRectangle;
        }
        catch { }

        return new ElementSnapshot(
            id,
            name,
            automationId,
            className,
            ControlTypeNames.GetValueOrDefault(controlTypeId, "Unknown"),
            ToRect(rect),
            children);
    }

    // Builds a UIA property condition for the locator's strategy; null if the strategy or control type name isn't recognized. Internal (not private) so BuildConditionParityTests can call it directly.
    internal static IUIAutomationCondition? BuildCondition(Locator locator) => locator.Strategy switch
    {
        LocatorStrategy.Name => Automation.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, locator.Value),
        LocatorStrategy.AutomationId => Automation.CreatePropertyCondition(UIA_PropertyIds.UIA_AutomationIdPropertyId, locator.Value),
        LocatorStrategy.ClassName => Automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ClassNamePropertyId, locator.Value),
        LocatorStrategy.ControlType => ControlTypeMap.TryGetValue(locator.Value, out var controlTypeId)
            ? Automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, controlTypeId)
            : null,
        LocatorStrategy.RuntimeId => ParseRuntimeId(locator.Value) is int[] id
            ? Automation.CreatePropertyCondition(UIA_PropertyIds.UIA_RuntimeIdPropertyId, id)
            : null,
        LocatorStrategy.Property => int.TryParse(locator.Value, out var propertyId)
            ? Automation.CreatePropertyCondition(propertyId, locator.PropertyValue)
            : null,
        _ => null,
    };

    // Parses a dotted RuntimeId string (e.g. "42.333896.3.1") back to int[] for use in a UIA property condition.
    private static int[]? ParseRuntimeId(string value)
    {
        try { return value.Split('.').Select(int.Parse).ToArray(); }
        catch { return null; }
    }

    private static readonly Uia3Operator _op = new();

    // Wraps a live UIA3 element as an ElementHandle; null only if identity can't be read — a single failed property just leaves that field blank.
    private ElementHandle? Wrap(IUIAutomationElement element)
    {
        string id = "", name = "", automationId = "", className = "", controlTypeName = "Unknown";
        Rect rect = default;
        bool isEnabled = false, isOffscreen = false;
        try
        {
            id = GetRuntimeId(element);
            name = element.CurrentName;
            automationId = element.CurrentAutomationId;
            className = element.CurrentClassName;
            controlTypeName = ControlTypeNames.GetValueOrDefault(element.CurrentControlType, "Unknown");
            rect = ToRect(element.CurrentBoundingRectangle);
            isEnabled = element.CurrentIsEnabled != 0;
            isOffscreen = element.CurrentIsOffscreen != 0;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException) { }

        if (id.Length == 0)
            return null; // couldn't even establish identity — treat the same as not found

        return new ElementHandle(id, "uia3", element)
        {
            Name = name,
            AutomationId = automationId,
            ClassName = className,
            ControlType = controlTypeName,
            BoundingRect = rect,
            IsEnabled = isEnabled,
            IsOffscreen = isOffscreen,
            Provider = this,
            Operator = _op,
        };
    }

    // Converts a UIA RuntimeId (an int array, opaque per-session) into the dotted string ID AutoMancer uses for ElementHandle.Id.
    private static string GetRuntimeId(IUIAutomationElement element) => string.Join(".", element.GetRuntimeId());

    // Converts a UIA tagRECT into AutoMancer's Rect struct.
    private static Rect ToRect(tagRECT rect) => new(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
}
