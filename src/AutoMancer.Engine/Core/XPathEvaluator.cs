// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace AutoMancer.Engine.Core;

// Evaluates XPath expressions against a UIA element tree snapshot, returning 0-based flat indices into the snapshot list.
public static class XPathEvaluator
{
    // Evaluates xpath against the snapshot tree; returns flat indices (0-based, depth-first) of matching elements.
    public static IReadOnlyList<int> Evaluate(string xpath, IReadOnlyList<ElementSnapshot> snapshots)
    {
        var (root, _) = BuildDocument(snapshots);
        var adjusted = AdjustIndexPredicates(xpath);

        return root.XPathSelectElements(adjusted)
            .Select(e => (int)e.Attribute("_idx")!)
            .ToList();
    }

    // Returns the snapshot tree as a formatted XML string — useful for debugging XPath expressions.
    public static string ToXml(IReadOnlyList<ElementSnapshot> snapshots)
    {
        var (root, _) = BuildDocument(snapshots);
        return root.ToString();
    }

    // Builds the root XElement and the parallel flat list from the snapshot tree.
    private static (XElement root, List<ElementSnapshot> flatList) BuildDocument(IReadOnlyList<ElementSnapshot> snapshots)
    {
        var flatList = new List<ElementSnapshot>();
        var root = new XElement("root");
        foreach (var snapshot in snapshots)
            root.Add(ToXElement(snapshot, flatList));
        return (root, flatList);
    }

    // Recursively converts a snapshot to an XElement, recording its flat position in _idx and all UIA properties as attributes.
    private static XElement ToXElement(ElementSnapshot snapshot, List<ElementSnapshot> flatList)
    {
        int idx = flatList.Count;
        flatList.Add(snapshot);

        var element = new XElement(
            SafeTagName(snapshot.ControlType),
            new XAttribute("_idx", idx),
            new XAttribute("Name", snapshot.Name ?? ""),
            new XAttribute("AutomationId", snapshot.AutomationId ?? ""),
            new XAttribute("ClassName", snapshot.ClassName ?? ""));

        foreach (var child in snapshot.Children)
            element.Add(ToXElement(child, flatList));

        return element;
    }

    // Increments every bare numeric predicate by one so callers can use 0-based indices against XPath's 1-based engine.
    private static string AdjustIndexPredicates(string xpath) =>
        Regex.Replace(xpath, @"\[(\d+)\]", m => $"[{int.Parse(m.Groups[1].Value) + 1}]");

    // Returns the control type name when it begins with a letter (valid XML start character), otherwise "Unknown".
    private static string SafeTagName(string? controlType) =>
        !string.IsNullOrEmpty(controlType) && char.IsLetter(controlType[0]) ? controlType : "Unknown";
}
