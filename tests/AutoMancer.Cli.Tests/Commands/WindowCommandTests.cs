// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli.Commands;
using AutoMancer.Engine.Actions;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class WindowCommandTests
{
    // With no --pid and no current target, the command fails with the target-not-found exit code.
    [Fact]
    public async Task Window_NoTarget_ExitsTargetNotFound()
    {
        CliTestHelper.ClearState();

        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(WindowCommand.Build());

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Contains("No target", stderr);
    }

    // More than one action in a single call is a usage error raised before attaching.
    [Theory]
    [InlineData("--move", "1,2", "--resize", "300,200")]
    [InlineData("--close", "--kill")]
    [InlineData("--state", "max", "--close")]
    [InlineData("--move", "1,2", "--kill")]
    public async Task MoreThanOneAction_IsUsageError(params string[] args)
    {
        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(WindowCommand.Build(), [.. CliTestHelper.Self, .. args]);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Contains("one of", stderr);
    }

    // Malformed values are usage errors raised before attaching.
    [Theory]
    [InlineData("--move", "12")]
    [InlineData("--move", "a,b")]
    [InlineData("--move", "1,2,3")]
    [InlineData("--resize", "0,100")]
    [InlineData("--resize", "100,-5")]
    [InlineData("--resize", "800")]
    [InlineData("--state", "huge")]
    public async Task BadValues_AreUsageErrors(string option, string value)
    {
        var (_, _, exitCode) = await CliTestHelper.RunAsync(WindowCommand.Build(), [.. CliTestHelper.Self, option, value]);

        Assert.Equal(CliExitCode.Usage, exitCode);
    }

    // A move to a negative coordinate (a window on a monitor left of or above the primary) is accepted when written with '='.
    [Fact]
    public void ParsePair_Move_AllowsNegatives()
    {
        Assert.Equal((-1200, 40), WindowCommand.ParsePair("-1200,40", "--move", allowNegative: true));
        Assert.Equal((10, -5), WindowCommand.ParsePair(" 10 , -5 ", "--move", allowNegative: true));
    }

    // A resize needs positive numbers.
    [Theory]
    [InlineData("0,10")]
    [InlineData("10,-1")]
    [InlineData("x,y")]
    [InlineData("10")]
    public void ParsePair_Resize_RejectsBadValues(string text)
    {
        Assert.Throws<ArgumentException>(() => WindowCommand.ParsePair(text, "--resize", allowNegative: false));
    }

    // State names resolve in any case, including the long forms.
    [Theory]
    [InlineData("normal", WindowState.Normal)]
    [InlineData("MAX", WindowState.Maximized)]
    [InlineData("maximized", WindowState.Maximized)]
    [InlineData("min", WindowState.Minimized)]
    [InlineData("Minimized", WindowState.Minimized)]
    public void ParseState_ResolvesNames(string text, WindowState expected)
    {
        Assert.Equal(expected, WindowCommand.ParseState(text));
    }

    // An unknown state name is rejected.
    [Fact]
    public void ParseState_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => WindowCommand.ParseState("huge"));
    }
}
