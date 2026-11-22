// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli.Commands;
using AutoMancer.Engine.Core;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class TreeFilterTests
{
    // Builds a snapshot node with the given control type and children.
    private static ElementSnapshot Node(string id, string type, params ElementSnapshot[] children)
        => new(id, id, null, null, type, default, children);

    // Non-interactive containers vanish and their interactive descendants move up to the nearest kept ancestor.
    [Fact]
    public void KeepInteractive_HoistsChildrenOfDroppedNodes()
    {
        var tree = Node("win", "Window", Node("pane", "Pane", Node("ok", "Button"), Node("grp", "Group", Node("box", "Edit"))));

        var kept = TreeCommand.KeepInteractive(tree).ToList();

        Assert.Equal(["ok", "box"], kept.Select(n => n.Id));
    }

    // Truncate keeps the requested number of levels below the node and drops the rest.
    [Fact]
    public void Truncate_LimitsDepth()
    {
        var tree = Node("a", "Window", Node("b", "Pane", Node("c", "Button")));

        Assert.Empty(TreeCommand.Truncate(tree, 0).Children);
        Assert.Empty(TreeCommand.Truncate(tree, 1).Children.Single().Children);
        Assert.Single(TreeCommand.Truncate(tree, 2).Children.Single().Children);
    }

    // Bad filter arguments are rejected as usage errors before attaching to the target process.
    [Theory]
    [InlineData("--depth", "-1")]
    [InlineData("--within-by", "name")]
    [InlineData("--within-value", "x")]
    public async Task Tree_BadFilterArgs_AreUsageErrors(string option, string value)
    {
        var (_, _, exitCode) = await CliTestHelper.RunAsync(TreeCommand.Build(), [.. CliTestHelper.Self, option, value]);

        Assert.Equal(CliExitCode.Usage, exitCode);
    }
}
