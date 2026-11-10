// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Tests.Diagnostics;

// Verifies LocatorSuggester's candidate ranking and uniqueness checks against synthetic snapshot trees.
public sealed class LocatorSuggesterTests
{
    // Builds a snapshot node with the given identity properties and children.
    private static ElementSnapshot Node(string id, string type, string? name = null, string? automationId = null, string? className = null, params ElementSnapshot[] children) =>
        new(id, name, automationId, className, type, default, children);

    // A unique AutomationId ranks first, then a unique Name, a unique ClassName, and the positional XPath last.
    [Fact]
    public void Candidates_AllUnique_RanksAutomationIdNameClassNameThenXPath()
    {
        var tree = new[] { Node("1", "Window", children: Node("2", "Button", "OK", "okButton", "Ok.Class")) };

        var result = LocatorSuggester.Candidates("2", tree);

        Assert.Equal([Locator.ByAutomationId("okButton"), Locator.ByName("OK"), Locator.ByClassName("Ok.Class"), Locator.ByXPath("Window[0]/Button[0]")], result.Select(c => c.Locator));
        Assert.Equal([true, true, false, false], result.Select(c => c.IsStable));
    }

    // A duplicated AutomationId is left out and the unique Name still ranks first.
    [Fact]
    public void Candidates_DuplicateAutomationId_OmitsIt()
    {
        var tree = new[] { Node("1", "Window", children: [Node("2", "Button", "OK", "btn"), Node("3", "Button", "Cancel", "btn")]) };

        var result = LocatorSuggester.Candidates("2", tree);

        Assert.Equal(Locator.ByName("OK"), result[0].Locator);
        Assert.DoesNotContain(result, c => c.Locator.Strategy == LocatorStrategy.AutomationId);
    }

    // An element with neither identity property gets only the unstable positional XPath.
    [Fact]
    public void Candidates_NoIdentity_OnlyUnstablePositionalXPath()
    {
        var tree = new[] { Node("1", "Window", children: [Node("2", "Pane"), Node("3", "Pane", children: [Node("4", "Button"), Node("5", "Button")])]) };

        var result = LocatorSuggester.Candidates("5", tree);

        var only = Assert.Single(result);
        Assert.Equal(Locator.ByXPath("Window[0]/Pane[1]/Button[1]"), only.Locator);
        Assert.False(only.IsStable);
    }

    // The positional XPath, evaluated against the same tree, selects exactly the target node.
    [Fact]
    public void Candidates_PositionalXPath_EvaluatesToTarget()
    {
        var tree = new[] { Node("1", "Window", children: [Node("2", "Pane"), Node("3", "Pane", children: [Node("4", "Button"), Node("5", "Button")])]) };

        var xpath = LocatorSuggester.Candidates("5", tree).Last();

        Assert.Equal([4], XPathEvaluator.Evaluate(xpath.Locator.Value, tree));
    }

    // Multiple roots are siblings for positional indexing.
    [Fact]
    public void Candidates_SecondRoot_IndexesAmongRoots()
    {
        var tree = new[] { Node("1", "Window"), Node("2", "Window") };

        var result = LocatorSuggester.Candidates("2", tree);

        Assert.Equal(Locator.ByXPath("Window[1]"), Assert.Single(result).Locator);
    }

    // An Id that isn't in the tree yields no candidates.
    [Fact]
    public void Candidates_UnknownId_ReturnsEmpty()
    {
        var tree = new[] { Node("1", "Window") };

        Assert.Empty(LocatorSuggester.Candidates("missing", tree));
    }

    // A class name shared with another node is left out; a unique one is offered, marked unstable.
    [Fact]
    public void Candidates_ClassName_OnlyWhenUnique()
    {
        var tree = new[] { Node("1", "Window", className: "Win", children: [Node("2", "Button", className: "Shared"), Node("3", "Button", className: "Shared"), Node("4", "Edit", className: "Only")]) };

        Assert.DoesNotContain(LocatorSuggester.Candidates("2", tree), c => c.Locator.Strategy == LocatorStrategy.ClassName);
        var unique = LocatorSuggester.Candidates("4", tree).Single(c => c.Locator.Strategy == LocatorStrategy.ClassName);
        Assert.Equal(Locator.ByClassName("Only"), unique.Locator);
        Assert.False(unique.IsStable);
    }
}
