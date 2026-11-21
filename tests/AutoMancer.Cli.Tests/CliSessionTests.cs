// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.Text.Json;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Cli.Tests;

public sealed class CliSessionTests
{
    // Builds a throwaway 'probe' command whose body is the given function, run through the shared helper.
    private static Command Probe(Func<CancellationToken, Task<CliResult>> body)
    {
        var command = new Command("probe");
        command.SetHandler(ctx => CliSession.RunAsync(ctx, body));
        return command;
    }

    // With --json, a successful result is one parseable JSON object with ok=true and the data payload.
    [Fact]
    public async Task Json_Success_EmitsSingleOkObject()
    {
        var (stdout, _, exitCode) = await CliTestHelper.RunAsync(
            Probe(_ => Task.FromResult(new CliResult("text", new { count = 3 }))), "--json");

        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal(0, exitCode);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(3, doc.RootElement.GetProperty("data").GetProperty("count").GetInt32());
    }

    // Without --json, the human text is printed verbatim.
    [Fact]
    public async Task Text_Success_PrintsText()
    {
        var (stdout, _, exitCode) = await CliTestHelper.RunAsync(
            Probe(_ => Task.FromResult(new CliResult("hello"))));

        Assert.Equal(0, exitCode);
        Assert.Equal("hello", stdout.Trim());
    }

    // With --json, a failure is one JSON object on stdout carrying the error code and message.
    [Fact]
    public async Task Json_Failure_EmitsErrorObject()
    {
        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(
            Probe(_ => throw new TargetNotFoundException("no app")), "--json");

        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Empty(stderr);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("target_not_found", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    // Each mapped exception type yields its documented exit code and JSON error code.
    [Theory]
    [MemberData(nameof(MappedErrors))]
    public async Task Errors_MapToExitCodes(Exception ex, int expectedExit, string expectedCode)
    {
        var (stdout, _, exitCode) = await CliTestHelper.RunAsync(Probe(_ => throw ex), "--json");

        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal(expectedExit, exitCode);
        Assert.Equal(expectedCode, doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private static readonly Locator L = Locator.ByName("x");

    // A minimized window tells the user how to restore it with this CLI, in text and JSON.
    [Fact]
    public async Task WindowMinimized_SuggestsTheRestoreCommand()
    {
        var (stdout, stderr, _) = await CliTestHelper.RunAsync(Probe(_ => throw new WindowMinimizedError(IntPtr.Zero)));
        var (json, _, _) = await CliTestHelper.RunAsync(Probe(_ => throw new WindowMinimizedError(IntPtr.Zero)), "--json");

        Assert.Contains("window --state normal", stderr);
        Assert.Contains("window --state normal", json);
        Assert.Empty(stdout);
    }

    // Supplies one exception per mapping row.
    public static TheoryData<Exception, int, string> MappedErrors() => new()
    {
        { new ArgumentException("bad"), CliExitCode.Usage, "usage" },
        { new InvalidOperationException("boom"), CliExitCode.Error, "error" },
        { new ElementNotFoundError(L, [], 0), CliExitCode.CheckFailed, "element_not_found" },
        { new ElementStillPresentError(L, 0), CliExitCode.CheckFailed, "still_present" },
        { new WindowMinimizedError(IntPtr.Zero), CliExitCode.CheckFailed, "window_minimized" },
        { new ElementNotInteractableError(null!, "disabled"), CliExitCode.CheckFailed, "not_interactable" },
        { new ElementConditionTimeoutError(L, 0), CliExitCode.CheckFailed, "condition_timeout" },
        { new TimeoutException(), CliExitCode.CheckFailed, "timeout" },
        { new BaselineDpiMismatchError(96, 144), CliExitCode.Error, "baseline_dpi_mismatch" },
    };
}
