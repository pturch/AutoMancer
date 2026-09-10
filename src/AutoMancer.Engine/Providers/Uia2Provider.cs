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

    // Resolves the root element for the session's window; null if the handle is invalid or the window has been destroyed — never throws (FromHandle itself can throw ElementNotAvailableException for a destroyed HWND).
    private static AutomationElement? TryGetRoot(AppSession session)
    {
        try { return AutomationElement.FromHandle(session.RootWindowHandle); }
        catch (Exception ex) when (ex is InvalidOperationException or ElementNotAvailableException) { return null; }
    }

    // Finds the first descendant of the session's root window matching the locator; null if none match — never throws.
    public Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var condition = BuildCondition(locator);
            if (condition is null)
                return (ElementHandle?)null;

            var root = TryGetRoot(session);
            if (root is null) return (ElementHandle?)null;

            // A stale element mid-search can abort FindFirst entirely — treat that as not-found instead of throwing.
            try
            {
                var found = root.FindFirst(TreeScope.Descendants, condition);
                if (found is null)
                    return null; // no element in the tree matched the locator

                return Wrap(found); // a match was located, but Wrap can still be null if it went stale before we could read it
            }
            catch (Exception ex) when (ex is InvalidOperationException or ElementNotAvailableException)
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
            var condition = BuildCondition(locator);
            if (condition is null)
                return (IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>();

            var root = TryGetRoot(session);
            if (root is null)
                return (IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>();

            try
            {
                var matches = root.FindAll(TreeScope.Descendants, condition);
                var results = new List<ElementHandle>(matches.Count);
                foreach (AutomationElement element in matches)
                    if (Wrap(element) is { } wrapped)
                        results.Add(wrapped);

                return (IReadOnlyList<ElementHandle>)results;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ElementNotAvailableException)
            {
                return (IReadOnlyList<ElementHandle>)Array.Empty<ElementHandle>();
            }
        }, ct);
    }

    // Snapshots the element tree rooted at the session's window using TreeWalker.ControlViewWalker.
    public Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var root = TryGetRoot(session);
            if (root is null)
                return (IReadOnlyList<ElementSnapshot>)Array.Empty<ElementSnapshot>();

            return (IReadOnlyList<ElementSnapshot>)[WalkTree(root, TreeWalker.ControlViewWalker, 0)];
        }, ct);
    }

    // Recursively builds a snapshot of an element and its children (defensive reads, since dynamic apps like Task Manager can invalidate elements mid-walk), stopping at MaxTreeDepth.
    private static ElementSnapshot WalkTree(AutomationElement element, TreeWalker walker, int depth)
    {
        var children = new List<ElementSnapshot>();
        if (depth < MaxTreeDepth)
        {
            AutomationElement? child = null;
            try { child = walker.GetFirstChild(element); } catch { }
            while (child is not null)
            {
                try { children.Add(WalkTree(child, walker, depth + 1)); } catch { }
                AutomationElement? next = null;
                try { next = walker.GetNextSibling(child); } catch { }
                child = next;
            }
        }

        // Read all properties in one block; if the element goes stale partway through, use whatever was captured.
        string id = "", name = "", automationId = "", className = "", controlTypeName = "Unknown";
        Rect rect = default;
        try
        {
            id = GetRuntimeId(element);
            var props = element.Current;
            name = props.Name;
            automationId = props.AutomationId;
            className = props.ClassName;
            controlTypeName = ControlTypeNames.GetValueOrDefault(props.ControlType.Id, "Unknown");
            rect = ToRect(props.BoundingRectangle);
        }
        catch { }

        return new ElementSnapshot(id, name, automationId, className, controlTypeName, rect, children);
    }

    // Builds a UIA2 property condition for the locator's strategy; null if the strategy or control type name isn't recognized. Internal (not private) so BuildConditionParityTests can call it directly.
    internal static Condition? BuildCondition(Locator locator) => locator.Strategy switch
    {
        LocatorStrategy.Name => new PropertyCondition(AutomationElement.NameProperty, locator.Value),
        LocatorStrategy.AutomationId => new PropertyCondition(AutomationElement.AutomationIdProperty, locator.Value),
        LocatorStrategy.ClassName => new PropertyCondition(AutomationElement.ClassNameProperty, locator.Value),
        LocatorStrategy.ControlType => ControlTypeMap.TryGetValue(locator.Value, out var controlType)
            ? new PropertyCondition(AutomationElement.ControlTypeProperty, controlType)
            : null,
        LocatorStrategy.RuntimeId => ParseRuntimeId(locator.Value) is int[] id
            ? new PropertyCondition(AutomationElement.RuntimeIdProperty, id)
            : null,
        LocatorStrategy.Property => int.TryParse(locator.Value, out var propertyId)
            ? new PropertyCondition(AutomationProperty.LookupById(propertyId), locator.PropertyValue)
            : null,
        _ => null,
    };

    // Parses a dotted RuntimeId string (e.g. "42.333896.3.1") back to int[] for use in a UIA2 property condition.
    private static int[]? ParseRuntimeId(string value)
    {
        try { return value.Split('.').Select(int.Parse).ToArray(); }
        catch { return null; }
    }

    private static readonly Uia2Operator _op = new();

    // Wraps a live UIA2 element as an ElementHandle; null only if identity can't be read — a single failed property just leaves that field blank.
    private ElementHandle? Wrap(AutomationElement element)
    {
        string id = "", name = "", automationId = "", className = "", controlTypeName = "Unknown";
        Rect rect = default;
        bool isEnabled = false, isOffscreen = false;
        try
        {
            id = GetRuntimeId(element);
            var props = element.Current;
            name = props.Name;
            automationId = props.AutomationId;
            className = props.ClassName;
            controlTypeName = ControlTypeNames.GetValueOrDefault(props.ControlType.Id, "Unknown");
            rect = ToRect(props.BoundingRectangle);
            isEnabled = props.IsEnabled;
            isOffscreen = props.IsOffscreen;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ElementNotAvailableException) { }

        if (id.Length == 0)
            return null; // couldn't even establish identity — treat the same as not found

        return new ElementHandle(id, "uia2", element)
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

    // Converts a UIA2 RuntimeId (int array) into the dotted string ID AutoMancer uses for ElementHandle.Id.
    private static string GetRuntimeId(AutomationElement element) => string.Join(".", element.GetRuntimeId());

    // Converts a System.Windows.Rect into AutoMancer's Rect struct.
    private static Rect ToRect(System.Windows.Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
