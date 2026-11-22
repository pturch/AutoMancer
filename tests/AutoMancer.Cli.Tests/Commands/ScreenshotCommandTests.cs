// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class ScreenshotCommandTests
{
    // With no --pid and no current target, the command fails with the target-not-found exit code.
    [Fact]
    public async Task Screenshot_NoTarget_ExitsTargetNotFound()
    {
        CliTestHelper.ClearState();

        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(ScreenshotCommand.Build(), "--out", "x.png");

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Contains("No target", stderr);
    }

    // A lone --by (no --value) is a usage error raised before attaching to the process.
    [Fact]
    public async Task Screenshot_LoneBy_IsUsageError()
    {
        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(
            ScreenshotCommand.Build(), [.. CliTestHelper.Self, "--out", "x.png", "--by", "name"]);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Contains("--by and --value", stderr);
    }
}
