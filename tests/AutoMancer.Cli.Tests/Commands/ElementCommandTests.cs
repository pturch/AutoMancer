// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class ElementCommandTests
{
    // Each element command with the arguments that make its parse valid.
    public static TheoryData<string, string[]> Cases() => new()
    {
        { "focus", ["--by", "name", "--value", "x"] },
    };

    // Builds the named command.
    private static Command Build(string name) => FocusCommand.Build();

    // With no --pid and no current target, the command fails with the target-not-found exit code.
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NoTarget_ExitsTargetNotFound(string name, string[] args)
    {
        CliTestHelper.ClearState();

        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(Build(name), args);

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Contains("No target", stderr);
    }

    // An unknown --by strategy is a usage error raised before attaching to the process.
    [Theory]
    [InlineData("focus", "--by", "css", "--value", "x")]
    public async Task UnknownStrategy_IsUsageError(string name, params string[] args)
    {
        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(Build(name), [.. CliTestHelper.Self, .. args]);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Contains("Unknown strategy 'css'", stderr);
    }
}
