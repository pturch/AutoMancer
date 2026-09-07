// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Core;

public sealed class ElementSnapshotExtensionsTests
{
    private static ElementSnapshot Node(string name, params ElementSnapshot[] children) =>
        new(Id: name, Name: name, AutomationId: null, ClassName: null, ControlType: null, BoundingRect: default, Children: children);

    [Fact]
    public void DescendantsAndSelf_SingleNodeNoChildren_ReturnsJustItself()
    {
        var node = Node("Root");

        Assert.Equal(["Root"], node.DescendantsAndSelf().Select(s => s.Name));
    }

    [Fact]
    public void DescendantsAndSelf_NestedTree_ReturnsDepthFirstParentBeforeChildren()
    {
        var tree = Node("Root",
            Node("A", Node("A1"), Node("A2")),
            Node("B"));

        Assert.Equal(["Root", "A", "A1", "A2", "B"], tree.DescendantsAndSelf().Select(s => s.Name));
    }

    [Fact]
    public void DescendantsAndSelf_OnRootList_FlattensEveryRootAndItsSubtree()
    {
        IReadOnlyList<ElementSnapshot> roots = [Node("Root1", Node("Child1")), Node("Root2")];

        Assert.Equal(["Root1", "Child1", "Root2"], roots.DescendantsAndSelf().Select(s => s.Name));
    }

    [Fact]
    public void DescendantsAndSelf_EmptyRootList_ReturnsEmpty()
    {
        Assert.Empty(Array.Empty<ElementSnapshot>().DescendantsAndSelf());
    }
}
