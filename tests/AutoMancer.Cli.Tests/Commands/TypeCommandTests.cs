// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class TypeCommandTests
{
    // With no --pid and no current target, the command fails with a clear message and the target-not-found exit code.
    [Fact]
    public async Task Type_NoTarget_PrintsErrorAndExitsTargetNotFound()
    {
        CliTestHelper.ClearState();

        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(
            TypeCommand.Build(), "Hello", "--by", "name", "--value", "irrelevant");

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Empty(stdout);
        Assert.Contains("No target", stderr);
    }

    // An unrecognized --by strategy is rejected before the command ever tries to attach to the target process.
    [Fact]
    public async Task Type_UnknownStrategy_PrintsLocatorErrorWithoutAttaching()
    {
        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(
            TypeCommand.Build(), [.. CliTestHelper.Self, "Hello", "--by", "css", "--value", "irrelevant"]);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Empty(stdout);
        Assert.Contains("Unknown strategy 'css'", stderr);
    }
}
