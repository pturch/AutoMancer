// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.Json;

namespace AutoMancer.Cli.Tests;

public sealed class CliAppTests
{
    // With no arguments the command list is shown and the exit code is 0, not a "command required" error.
    [Fact]
    public async Task NoArguments_ShowsCommands_AndSucceeds()
    {
        var (stdout, stderr, exitCode) = await CliTestHelper.RunAppAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Commands:", stdout);
        Assert.Empty(stderr);
    }

    // --help at the root is a successful request.
    [Fact]
    public async Task RootHelp_Succeeds()
    {
        var (stdout, _, exitCode) = await CliTestHelper.RunAppAsync("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("assert", stdout);
        Assert.Contains("--pid", stdout);
    }

    // --version prints one version line and succeeds.
    [Fact]
    public async Task Version_PrintsOneLine()
    {
        var (stdout, _, exitCode) = await CliTestHelper.RunAppAsync("--version");

        Assert.Equal(0, exitCode);
        Assert.Single(stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    // Every command's --help works and exits 0.
    [Fact]
    public async Task EveryCommandHelp_Succeeds()
    {
        var names = CliApp.BuildRoot().Subcommands.Select(c => c.Name).ToList();
        Assert.True(names.Count >= 13);

        foreach (var name in names)
        {
            var (stdout, _, exitCode) = await CliTestHelper.RunAppAsync(name, "--help");

            Assert.True(exitCode == 0, $"{name} --help exited {exitCode}");
            Assert.Contains("Usage:", stdout);
        }
    }

    // A syntax error in text mode puts the message and a hint on stderr and leaves stdout clean, instead of dumping the help text there.
    [Fact]
    public async Task ParseError_Text_StdoutStaysClean()
    {
        var (stdout, stderr, exitCode) = await CliTestHelper.RunAppAsync("find", "--value", "x");

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Empty(stdout);
        Assert.Contains("Option '--by' is required.", stderr);
        Assert.Contains("Run 'automancer find --help' for usage.", stderr);
        Assert.DoesNotContain("Description:", stderr);
    }

    // A syntax error with --json is one JSON usage error on stdout, wherever --json appears, and stderr stays empty.
    [Theory]
    [InlineData("find", "--value", "x", "--json")]
    [InlineData("--json", "find", "--value", "x")]
    [InlineData("nosuchcommand", "--json")]
    [InlineData("configure", "--implicit-wait", "abc", "--json")]
    [InlineData("assert", "--pid", "abc", "--json")]
    public async Task ParseError_Json_IsOneUsageObject(params string[] args)
    {
        var (stdout, stderr, exitCode) = await CliTestHelper.RunAppAsync(args);

        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("usage", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("--help", doc.RootElement.GetProperty("error").GetProperty("message").GetString());
        Assert.Empty(stderr);
    }

    // The .NET type names System.CommandLine puts in messages are replaced with plain words.
    [Theory]
    [InlineData("configure", "--implicit-wait", "abc", "a whole number")]
    [InlineData("assert", "--tolerance", "abc", "a number")]
    [InlineData("configure", "--reresolve-on-stale", "maybe", "--reresolve-on-stale")]
    public async Task ParseError_UsesPlainTypeNames(string command, string option, string value, string expected)
    {
        var (_, stderr, _) = await CliTestHelper.RunAppAsync(command, option, value);

        Assert.Contains(expected, stderr);
        Assert.DoesNotContain("System.", stderr);
    }

    // An unrecognized command points at the command list.
    [Fact]
    public async Task UnknownCommand_PointsAtHelp()
    {
        var (stdout, stderr, exitCode) = await CliTestHelper.RunAppAsync("lanch");

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Empty(stdout);
        Assert.Contains("Run 'automancer --help' for the commands.", stderr);
    }
}
