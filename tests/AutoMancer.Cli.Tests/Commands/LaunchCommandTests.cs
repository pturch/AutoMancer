// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class LaunchCommandTests
{
    // A path that doesn't resolve to a real executable fails at Process.Start with a non-zero exit code and no session.
    [Fact]
    public async Task Launch_NonexistentExecutable_PrintsErrorAndExitsNonZero()
    {
        var badPath = Path.Combine(Path.GetTempPath(), $"automancer-does-not-exist-{Guid.NewGuid():N}.exe");

        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(LaunchCommand.Build(), badPath);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdout);
        Assert.Contains("Launch failed", stderr);
    }

    // --args is accepted by the parser and threaded through to AppOptions.Arguments without disrupting the existing failure path — a parsing regression would fail differently (a System.CommandLine parse error, different exit code/message), not with "Launch failed". The end-to-end proof that the value actually reaches the OS process is AppOptionsArgumentsIntegrationTests, which needs a real window this project deliberately doesn't depend on.
    [Fact]
    public async Task Launch_NonexistentExecutable_WithArgsOption_StillFailsAtProcessStartNotAtParsing()
    {
        var badPath = Path.Combine(Path.GetTempPath(), $"automancer-does-not-exist-{Guid.NewGuid():N}.exe");

        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(LaunchCommand.Build(), badPath, "--args", "--some-flag \"value with spaces\"");

        Assert.Equal(1, exitCode);
        Assert.Empty(stdout);
        Assert.Contains("Launch failed", stderr);
    }
}
