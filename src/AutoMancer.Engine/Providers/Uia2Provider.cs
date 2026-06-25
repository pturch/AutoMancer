// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Automation;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Operators;

namespace AutoMancer.Engine.Providers;

// Finds elements via the managed System.Windows.Automation API — the second provider in the fallback chain, used when UIA3 returns nothing.
public sealed class Uia2Provider : IElementProvider
{
    public string ProviderName => "uia2";

    private const int MaxTreeDepth = 20;

    // AutoMancer's control type names mapped to System.Windows.Automation.ControlType values.
    private static readonly Dictionary<string, ControlType> ControlTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Button"] = ControlType.Button,
        ["Calendar"] = ControlType.Calendar,
        ["CheckBox"] = ControlType.CheckBox,
        ["ComboBox"] = ControlType.ComboBox,
        ["Edit"] = ControlType.Edit,
        ["Hyperlink"] = ControlType.Hyperlink,
        ["Image"] = ControlType.Image,
        ["ListItem"] = ControlType.ListItem,
        ["List"] = ControlType.List,
        ["Menu"] = ControlType.Menu,
        ["MenuBar"] = ControlType.MenuBar,
        ["MenuItem"] = ControlType.MenuItem,
        ["ProgressBar"] = ControlType.ProgressBar,
        ["RadioButton"] = ControlType.RadioButton,
        ["ScrollBar"] = ControlType.ScrollBar,
        ["Slider"] = ControlType.Slider,
        ["Spinner"] = ControlType.Spinner,
        ["StatusBar"] = ControlType.StatusBar,
        ["Tab"] = ControlType.Tab,
        ["TabItem"] = ControlType.TabItem,
        ["Text"] = ControlType.Text,
        ["ToolBar"] = ControlType.ToolBar,
        ["ToolTip"] = ControlType.ToolTip,
        ["Tree"] = ControlType.Tree,
        ["TreeItem"] = ControlType.TreeItem,
        ["Custom"] = ControlType.Custom,
        ["Group"] = ControlType.Group,
        ["Thumb"] = ControlType.Thumb,
        ["DataGrid"] = ControlType.DataGrid,
        ["DataItem"] = ControlType.DataItem,
        ["Document"] = ControlType.Document,
        ["SplitButton"] = ControlType.SplitButton,
        ["Window"] = ControlType.Window,
        ["Pane"] = ControlType.Pane,
        ["Header"] = ControlType.Header,
        ["HeaderItem"] = ControlType.HeaderItem,
        ["Table"] = ControlType.Table,
        ["TitleBar"] = ControlType.TitleBar,
        ["Separator"] = ControlType.Separator,
    };

    // Reverse of ControlTypeMap — turns a resolved element's ControlType.Id back into its AutoMancer name.
    private static readonly Dictionary<int, string> ControlTypeNames =
        ControlTypeMap.GroupBy(kv => kv.Value.Id).ToDictionary(g => g.Key, g => g.First().Key);

    // Finds the first descendant of the session's root window matching the locator; null if none match or the strategy is unsupported.
    public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var condition = BuildCondition(locator);
            if (condition is null)
                return (ElementHandle?)null;

            var root = AutomationElement.FromHandle(session.RootWindowHandle);
            var found = root?.FindFirst(TreeScope.Descendants, condition);
            return found is null ? null : Wrap(found);
        }, ct);
    }

    // Finds every descendant of the session's root window matching the locator; empty if none match.
    public Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var condition = BuildCondition(locator);
            if (condition is null)
                return (IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>();

            var root = AutomationElement.FromHandle(session.RootWindowHandle);
            if (root is null)
                return (IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>();

            var matches = root.FindAll(TreeScope.Descendants, condition);
            var results = new List<ElementHandle>(matches.Count);
            foreach (AutomationElement element in matches)
                results.Add(Wrap(element));

            return (IReadOnlyList<ElementHandle>)results;
        }, ct);
    }

    // Snapshots the element tree rooted at the session's window using TreeWalker.ControlViewWalker.
    public Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var root = AutomationElement.FromHandle(session.RootWindowHandle);
            if (root is null)
                return (IReadOnlyList<ElementSnapshot>)Array.Empty<ElementSnapshot>();

            return (IReadOnlyList<ElementSnapshot>)[WalkTree(root, TreeWalker.ControlViewWalker, 0)];
        }, ct);
    }

    // Recursively builds a snapshot of an element and its children, stopping at MaxTreeDepth.
    private static ElementSnapshot WalkTree(AutomationElement element, TreeWalker walker, int depth)
    {
        var children = new List<ElementSnapshot>();
        if (depth < MaxTreeDepth)
        {
            var child = walker.GetFirstChild(element);
            while (child is not null)
            {
                children.Add(WalkTree(child, walker, depth + 1));
                child = walker.GetNextSibling(child);
            }
        }

        var props = element.Current;
        return new ElementSnapshot(
            GetRuntimeId(element),
            props.Name,
            props.AutomationId,
            props.ClassName,
            ControlTypeNames.GetValueOrDefault(props.ControlType.Id, "Unknown"),
            ToRect(props.BoundingRectangle),
            children);
    }

    // Builds a UIA2 property condition for the locator's strategy; null if the strategy or control type name isn't recognized.
    private static Condition? BuildCondition(Locator locator) => locator.Strategy switch
    {
        LocatorStrategy.Name => new PropertyCondition(AutomationElement.NameProperty, locator.Value),
        LocatorStrategy.AutomationId => new PropertyCondition(AutomationElement.AutomationIdProperty, locator.Value),
        LocatorStrategy.ClassName => new PropertyCondition(AutomationElement.ClassNameProperty, locator.Value),
        LocatorStrategy.ControlType => ControlTypeMap.TryGetValue(locator.Value, out var controlType)
            ? new PropertyCondition(AutomationElement.ControlTypeProperty, controlType)
            : null,
        _ => null,
    };

    private static readonly Uia2Operator _op = new();

    // Wraps a live UIA2 element as an opaque ElementHandle, capturing its display metadata at resolve time.
    private ElementHandle Wrap(AutomationElement element)
    {
        var props = element.Current;
        return new ElementHandle(GetRuntimeId(element), "uia2", element)
        {
            Name = props.Name,
            AutomationId = props.AutomationId,
            ClassName = props.ClassName,
            ControlType = ControlTypeNames.GetValueOrDefault(props.ControlType.Id, "Unknown"),
            BoundingRect = ToRect(props.BoundingRectangle),
            Provider = this,
            Operator = _op,
        };
    }

    // Converts a UIA2 RuntimeId (int array) into the dotted string ID AutoMancer uses for ElementHandle.Id.
    private static string GetRuntimeId(AutomationElement element) => string.Join(".", element.GetRuntimeId());

    // Converts a System.Windows.Rect into AutoMancer's Rect struct.
    private static Rect ToRect(System.Windows.Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
