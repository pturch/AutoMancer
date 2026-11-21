// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class ClickCommandTests
{
    // With no --pid and no current target, the command fails with a clear message and the target-not-found exit code.
    [Fact]
    public async Task Click_NoTarget_PrintsErrorAndExitsTargetNotFound()
    {
        CliTestHelper.ClearState();

        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(
            ClickCommand.Build(), "--by", "name", "--value", "irrelevant");

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Empty(stdout);
        Assert.Contains("No target", stderr);
    }

    // A --pid that no process has fails as target-not-found with the engine's reason and a hint.
    [Fact]
    public async Task Click_DeadPid_ExitsTargetNotFoundWithHint()
    {
        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(
            ClickCommand.Build(), [.. CliTestHelper.Dead, "--by", "name", "--value", "irrelevant"]);

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Empty(stdout);
        Assert.Contains("No process with PID", stderr);
        Assert.Contains("owns the window", stderr);
    }

    // An unrecognized --by strategy is rejected before the command ever tries to attach to the target process.
    [Fact]
    public async Task Click_UnknownStrategy_PrintsLocatorErrorWithoutAttaching()
    {
        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(
            ClickCommand.Build(), [.. CliTestHelper.Self, "--by", "css", "--value", "irrelevant"]);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Empty(stdout);
        Assert.Contains("Unknown strategy 'css'", stderr);
    }
}
