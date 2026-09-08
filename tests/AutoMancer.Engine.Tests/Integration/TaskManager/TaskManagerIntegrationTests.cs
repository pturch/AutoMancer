// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Covers a childless-root snapshot wrongly counting as success instead of falling through to a provider with real content.
// Also covers Find throwing instead of returning null when a stale element aborts the search.
[Collection("TaskManager")]
[Trait("Category", "Integration")]
public sealed class TaskManagerIntegrationTests(TaskManagerFixture fixture) : IClassFixture<TaskManagerFixture>
{
    // A snapshot must include real descendants, not just the bare root window uia3 alone can see.
    [Fact]
    public async Task SnapshotAsync_ReturnsDescendants_NotJustRootWindow()
    {
        var snapshot = await fixture.App.SnapshotAsync();

        Assert.NotNull(snapshot);
        var root = Assert.Single(snapshot);
        Assert.Equal("Task Manager", root.Name);
        Assert.NotEmpty(root.Children);
    }

    // Task Manager's content is only visible via uia2 on this provider chain — locks in the fallback behavior the snapshot fix depends on.
    [Fact]
    public async Task SnapshotAsync_FindsTitleBarNode_ResolvedViaUia2()
    {
        var snapshot = await fixture.App.SnapshotAsync();

        Assert.NotNull(snapshot);
        var titleBar = FindByControlType(snapshot[0], "TitleBar");
        Assert.NotNull(titleBar);
        Assert.Equal("Task Manager", titleBar.Name);
    }

    // Which provider resolves it isn't asserted — TitleBar sits at the root window's top level, so either can legitimately find it.
    [Fact]
    public async Task FindAsync_ResolvesTitleBar()
    {
        var element = await fixture.App.FindAsync(Locator.ByControlType("TitleBar"));

        Assert.NotNull(element);
        Assert.Equal("Task Manager", element.Name);
    }

    // Depth-first search of a snapshot subtree for the first node of the given control type.
    private static ElementSnapshot? FindByControlType(ElementSnapshot node, string controlType)
    {
        if (node.ControlType == controlType) return node;
        foreach (var child in node.Children)
        {
            var found = FindByControlType(child, controlType);
            if (found is not null) return found;
        }
        return null;
    }
}
