// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli;
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class TypeCommandTests
{
    // A session ID that was never launched fails with a clear message and a non-zero exit code.
    [Fact]
    public async Task Type_UnknownSession_PrintsErrorAndExitsNonZero()
    {
        var sessionId = Guid.NewGuid().ToString("N");

        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(
            TypeCommand.Build(), sessionId, "Hello", "--by", "name", "--value", "irrelevant");

        Assert.Equal(1, exitCode);
        Assert.Empty(stdout);
        Assert.Contains(sessionId, stderr);
        Assert.Contains("not found", stderr);
    }

    // An unrecognized --by strategy is rejected before the command ever tries to attach to the target process.
    [Fact]
    public async Task Type_UnknownStrategy_PrintsLocatorErrorWithoutAttaching()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        SessionStore.Save(new SessionEntry(sessionId, ProcessId: -1, "unused.exe"));
        try
        {
            var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(
                TypeCommand.Build(), sessionId, "Hello", "--by", "css", "--value", "irrelevant");

            Assert.Equal(1, exitCode);
            Assert.Empty(stdout);
            Assert.Contains("Unknown strategy 'css'", stderr);
        }
        finally
        {
            SessionStore.Remove(sessionId);
        }
    }
}
