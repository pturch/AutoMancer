// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using AutoMancer.Cli.Commands;
using AutoMancer.Engine.Core;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class ScopedLocatorTests
{
    // Every command that takes a locator (or, for tree, a scope), with the arguments that make its parse valid without a scope.
    public static TheoryData<string, string[]> Cases() => new()
    {
        { "find", ["--by", "name", "--value", "x"] },
        { "click", ["--by", "name", "--value", "x"] },
        { "type", ["hello", "--by", "name", "--value", "x"] },
        { "hover", ["--by", "name", "--value", "x"] },
        { "focus", ["--by", "name", "--value", "x"] },
        { "screenshot", ["--out", "x.png", "--by", "name", "--value", "x"] },
        { "assert", ["--by", "name", "--value", "x"] },
        { "tree", [] },
    };

    // Builds the named command.
    private static Command Build(string name) => name switch
    {
        "find" => FindCommand.Build(),
        "click" => ClickCommand.Build(),
        "type" => TypeCommand.Build(),
        "hover" => HoverCommand.Build(),
        "focus" => FocusCommand.Build(),
        "screenshot" => ScreenshotCommand.Build(),
        "assert" => AssertCommand.Build(),
        _ => TreeCommand.Build(),
    };

    // A lone --within-by or --within-value is a usage error raised before attaching, on every command.
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task LoneHalfOfTheScope_IsUsageError(string name, string[] args)
    {
        var (_, stderrBy, exitBy) = await CliTestHelper.RunAsync(Build(name), [.. CliTestHelper.Self, .. args, "--within-by", "name"]);
        var (_, stderrValue, exitValue) = await CliTestHelper.RunAsync(Build(name), [.. CliTestHelper.Self, .. args, "--within-value", "x"]);

        Assert.Equal(CliExitCode.Usage, exitBy);
        Assert.Equal(CliExitCode.Usage, exitValue);
        Assert.Contains("--within-by and --within-value must be given together", stderrBy);
        Assert.Contains("--within-by and --within-value must be given together", stderrValue);
    }

    // An unknown scope strategy is a usage error, named for the scope's own strategy.
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task UnknownScopeStrategy_IsUsageError(string name, string[] args)
    {
        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(
            Build(name), [.. CliTestHelper.Self, .. args, "--within-by", "css", "--within-value", "x"]);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Contains("Unknown strategy 'css'", stderr);
    }

    // On commands where the locator is optional, a scope with no locator is a usage error: it says where to look, not what to find.
    [Theory]
    [InlineData("assert", "--check", "stable")]
    [InlineData("screenshot", "--out", "x.png")]
    public async Task ScopeWithoutLocator_IsUsageError(string name, string option, string value)
    {
        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(
            Build(name), [.. CliTestHelper.Self, option, value, "--within-by", "name", "--within-value", "x"]);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Contains("need --by and --value", stderr);
    }

    // With both pairs given, the options parse into a locator and a scope with the right strategies and values.
    [Fact]
    public void Parse_BothPairs_GivesTargetAndScope()
    {
        var opts = new LocatorOptions(required: true);
        var command = new Command("probe");
        opts.AddTo(command);

        var parsed = command.Parse("--by", "id", "--value", "OK", "--within-by", "name", "--within-value", "Settings");
        var scoped = opts.Parse(parsed)!;

        Assert.Equal(LocatorStrategy.AutomationId, scoped.Target.Strategy);
        Assert.Equal("OK", scoped.Target.Value);
        Assert.Equal(LocatorStrategy.Name, scoped.Scope!.Strategy);
        Assert.Equal("Settings", scoped.Scope.Value);
    }

    // Without a scope the scoped locator carries none, and with optional options and nothing given there is no locator at all.
    [Fact]
    public void Parse_NoScope_AndNoLocator()
    {
        var required = new LocatorOptions(required: true);
        var requiredCommand = new Command("probe");
        required.AddTo(requiredCommand);
        var optional = new LocatorOptions(required: false);
        var optionalCommand = new Command("probe2");
        optional.AddTo(optionalCommand);

        var scoped = required.Parse(requiredCommand.Parse("--by", "name", "--value", "x"))!;
        var none = optional.Parse(optionalCommand.Parse());

        Assert.Null(scoped.Scope);
        Assert.Null(none);
    }

    // An xpath or path locator can't be scoped (the engine would search the whole window), so the combination is a usage error on every locator command.
    [Theory]
    [InlineData("find", "xpath")]
    [InlineData("click", "path")]
    [InlineData("type", "xpath")]
    [InlineData("hover", "path")]
    [InlineData("focus", "xpath")]
    [InlineData("screenshot", "xpath")]
    [InlineData("assert", "path")]
    public async Task XPathOrPath_WithScope_IsUsageError(string name, string by)
    {
        string[] extra = name switch
        {
            "type" => ["hello"],
            "screenshot" => ["--out", "x.png"],
            _ => [],
        };

        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(
            Build(name), [.. CliTestHelper.Self, .. extra, "--by", by, "--value", "x", "--within-by", "control", "--within-value", "TabItem"]);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Contains("search from the window root", stderr);
    }

    // The same locators are fine without a scope, and xpath or path as the scope itself is fine, since the scope is resolved from the root.
    [Theory]
    [InlineData("xpath")]
    [InlineData("path")]
    public void XPathOrPath_AllowedWithoutScope_AndAsTheScope(string by)
    {
        var opts = new LocatorOptions(required: true);
        var command = new Command("probe");
        opts.AddTo(command);

        var unscoped = opts.Parse(command.Parse("--by", by, "--value", "x"))!;
        var xpathScope = opts.Parse(command.Parse("--by", "name", "--value", "x", "--within-by", by, "--within-value", "y"))!;

        Assert.Null(unscoped.Scope);
        Assert.NotNull(xpathScope.Scope);
    }

    // The old tree option names are gone: tree starts at a scope now, named like every other command's.
    [Fact]
    public async Task Tree_OldRootOptions_AreGone()
    {
        var (_, _, exitCode) = await CliTestHelper.RunAsync(TreeCommand.Build(), [.. CliTestHelper.Self, "--root-by", "name", "--root-value", "x"]);

        Assert.Equal(CliExitCode.Usage, exitCode);
    }
}
