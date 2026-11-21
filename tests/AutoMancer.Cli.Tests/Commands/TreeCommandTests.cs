// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class TreeCommandTests
{
    // With no --pid and no current target, the command fails with a clear message and the target-not-found exit code.
    [Fact]
    public async Task Tree_NoTarget_PrintsErrorAndExitsTargetNotFound()
    {
        CliTestHelper.ClearState();

        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(TreeCommand.Build());

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Empty(stdout);
        Assert.Contains("No target", stderr);
    }
}
