// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Diagnostics;

// A locator that singles out one element in the tree it was built from; IsStable is false when that depends on the rest of the tree staying as it is — a class name unique today, a sibling position.
public sealed record LocatorSuggestion(Locator Locator, bool IsStable);

// Ranks the locators that single out one element in a snapshot tree: unique AutomationId, unique Name, unique ClassName, then a positional XPath.
public static class LocatorSuggester
{
    // Returns the locators that match only the element with the given Id in this tree, best first; empty if no node has that Id.
    public static IReadOnlyList<LocatorSuggestion> Candidates(string elementId, IReadOnlyList<ElementSnapshot> tree)
    {
        var all = tree.DescendantsAndSelf().ToList();
        var target = all.FirstOrDefault(n => n.Id == elementId);
        if (target is null)
            return [];

        var candidates = new List<LocatorSuggestion>();
        // Best first: an identity is offered only if no other node in the tree shares it (stable, survives layout changes), then the class name (unique today only), and the positional XPath always last as the fallback that matches by construction.
        if (!string.IsNullOrEmpty(target.AutomationId) && all.Count(n => n.AutomationId == target.AutomationId) == 1)
            candidates.Add(new(Locator.ByAutomationId(target.AutomationId), true));
        if (!string.IsNullOrEmpty(target.Name) && all.Count(n => n.Name == target.Name) == 1)
            candidates.Add(new(Locator.ByName(target.Name), true));
        if (!string.IsNullOrEmpty(target.ClassName) && all.Count(n => n.ClassName == target.ClassName) == 1)
            candidates.Add(new(Locator.ByClassName(target.ClassName), false));
        candidates.Add(new(Locator.ByXPath(PositionalXPath(elementId, tree)!), false));
        return candidates;
    }

    // Builds a root-relative XPath of Type[n] steps, n being the 0-based index among same-typed siblings; null if the Id isn't beneath siblings.
    private static string? PositionalXPath(string elementId, IReadOnlyList<ElementSnapshot> siblings)
    {
        foreach (var node in siblings)
        {
            var tag = XPathEvaluator.SafeTagName(node.ControlType);
            var step = $"{tag}[{siblings.Where(s => XPathEvaluator.SafeTagName(s.ControlType) == tag).ToList().IndexOf(node)}]";
            if (node.Id == elementId)
                return step;
            var below = PositionalXPath(elementId, node.Children);
            if (below is not null)
                return $"{step}/{below}";
        }
        return null;
    }
}
