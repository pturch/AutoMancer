// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class TreeCommandTests
{
    // A session ID that was never launched fails with a clear message and a non-zero exit code.
    [Fact]
    public async Task Tree_UnknownSession_PrintsErrorAndExitsNonZero()
    {
        var sessionId = Guid.NewGuid().ToString("N");

        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(TreeCommand.Build(), sessionId);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdout);
        Assert.Contains(sessionId, stderr);
        Assert.Contains("not found", stderr);
    }
}
