// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.Text.Json;
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class AssertCommandTests
{
    // Runs 'assert' with this test process as the target (so nothing can fail before validation) and returns its exit code and stderr.
    private static async Task<(int ExitCode, string Stderr)> RunWithTargetAsync(params string[] args)
    {
        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(AssertCommand.Build(), [.. CliTestHelper.Self, .. args]);
        return (exitCode, stderr);
    }

    // With no --pid and no current target, assert fails with the target-not-found exit code.
    [Fact]
    public async Task Assert_NoTarget_ExitsTargetNotFound()
    {
        CliTestHelper.ClearState();

        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(AssertCommand.Build(), "--by", "name", "--value", "x");

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Contains("No target", stderr);
    }

    // Bad argument combinations are usage errors raised before attaching to the process.
    [Theory]
    [InlineData("--by", "name", "--value", "x", "--check", "!exists", "--check", "enabled")]
    [InlineData("--by", "name", "--value", "x", "--timeout", "-5")]
    [InlineData("--by", "name", "--value", "x", "--check", "bogus")]
    [InlineData("--by", "name", "--value", "x", "--check", "enabled=maybe")]
    [InlineData("--expected", "a.png", "--actual", "b.png", "--tolerance", "2")]
    [InlineData("--expected", "a.png")]
    [InlineData("--actual", "b.png")]
    [InlineData("--by", "name", "--value", "x", "--check", "!exists", "--expected", "a.png", "--actual", "b.png")]
    [InlineData("--by", "name", "--value", "x", "--check", "enabled", "--expected", "a.png")]
    [InlineData("--by", "name", "--stable")]
    [InlineData("--check", "enabled")]
    [InlineData("--check", "stable", "--check", "name=x")]
    [InlineData("--timeout", "100")]
    public async Task Assert_BadArgs_AreUsageErrors(params string[] args)
    {
        var (exitCode, _) = await RunWithTargetAsync(args);

        Assert.Equal(CliExitCode.Usage, exitCode);
    }

    // Without a locator the usage error names the checks that are allowed.
    [Fact]
    public async Task Assert_NoLocatorElementCheck_NamesAllowedChecks()
    {
        var (_, stderr) = await RunWithTargetAsync("--check", "enabled");

        Assert.Contains("--expected/--actual", stderr);
    }

    // Each expression form parses to the expected check name, operator and polarity.
    [Theory]
    [InlineData("enabled", "enabled", "None", true)]
    [InlineData("!enabled", "enabled", "None", false)]
    [InlineData("enabled=false", "enabled", "None", false)]
    [InlineData("clickable=true", "clickable", "None", true)]
    [InlineData("name=OK", "name", "Equals", true)]
    [InlineData("name~Ok", "name", "Contains", true)]
    [InlineData("text=~^H.llo$", "text", "Matches", true)]
    [InlineData("automation-id=SaveBtn", "automation-id", "Equals", true)]
    [InlineData("count=3", "count", "Equals", true)]
    [InlineData("name=a=b", "name", "Equals", true)]
    public void Parse_ValidExpressions(string expr, string name, string op, bool want)
    {
        var check = AssertChecks.Parse(expr);

        Assert.Equal(name, check.Name);
        Assert.Equal(Enum.Parse<CheckOp>(op), check.Op);
        Assert.Equal(want, check.Want);
    }

    // Malformed expressions are rejected with ArgumentException.
    [Theory]
    [InlineData("")]
    [InlineData("nope")]
    [InlineData("!name=x")]
    [InlineData("name")]
    [InlineData("name=")]
    [InlineData("count=-1")]
    [InlineData("count=abc")]
    [InlineData("count~3")]
    [InlineData("enabled~x")]
    [InlineData("text=~(")]
    [InlineData("name?x")]
    public void Parse_InvalidExpressions_Throw(string expr)
    {
        Assert.ThrowsAny<ArgumentException>(() => AssertChecks.Parse(expr));
    }

    // Text comparisons keep the value verbatim, including any '=' after the operator, and regexes compile once at parse time.
    [Fact]
    public void Parse_TextValueAndRegex()
    {
        Assert.Equal("a=b", AssertChecks.Parse("name=a=b").Value);
        Assert.Matches(AssertChecks.Parse("name=~^x").Pattern!, "xyz");
        Assert.Equal("name contains", AssertChecks.Parse("name~x").Label);
    }

    // Identical images pass without attaching to any app, so any resolvable target works as a stand-in.
    [Fact]
    public async Task Images_Identical_Pass()
    {
        using var dir = new TempDir();
        var a = PngTestHelper.Write(dir.Path, "a.png", 4, 4, 10);
        var b = PngTestHelper.Write(dir.Path, "b.png", 4, 4, 10);

        var (exitCode, _) = await RunWithTargetAsync("--expected", a, "--actual", b);

        Assert.Equal(CliExitCode.Ok, exitCode);
    }

    // Images over the tolerance fail as an assertion; raising --tolerance makes the same pair pass.
    [Fact]
    public async Task Images_Different_FailUnlessWithinTolerance()
    {
        using var dir = new TempDir();
        var a = PngTestHelper.Write(dir.Path, "a.png", 4, 4, 10);
        var b = PngTestHelper.Write(dir.Path, "b.png", 4, 4, 10, changedPixels: 2);

        var (failExit, failErr) = await RunWithTargetAsync("--expected", a, "--actual", b);
        var (passExit, _) = await RunWithTargetAsync("--expected", a, "--actual", b, "--tolerance", "0.2");

        Assert.Equal(CliExitCode.CheckFailed, failExit);
        Assert.Contains("images-match", failErr);
        Assert.Equal(CliExitCode.Ok, passExit);
    }

    // Images of different sizes fail with the dimensions spelled out.
    [Fact]
    public void CompareImages_DifferentSizes_ReportsDimensions()
    {
        using var dir = new TempDir();
        var a = PngTestHelper.Write(dir.Path, "a.png", 4, 4, 10);
        var b = PngTestHelper.Write(dir.Path, "b.png", 8, 2, 10);

        var check = AssertChecks.CompareImages(a, b, 0.01);

        Assert.False(check.Passed);
        Assert.Equal("sizes differ", check.Actual);
        Assert.Contains("4x4", check.Detail);
        Assert.Contains("8x2", check.Detail);
    }

    // A missing file is an environment error (exit 4); a non-PNG file is a usage error (exit 2).
    [Fact]
    public async Task Images_BadFiles_MapToErrorAndUsage()
    {
        using var dir = new TempDir();
        var good = PngTestHelper.Write(dir.Path, "a.png", 4, 4, 10);
        var notPng = Path.Combine(dir.Path, "x.png");
        File.WriteAllText(notPng, "hello world, definitely not a png file");

        var (missingExit, _) = await RunWithTargetAsync("--expected", good, "--actual", Path.Combine(dir.Path, "nope.png"));
        var (badExit, _) = await RunWithTargetAsync("--expected", good, "--actual", notPng);

        Assert.Equal(CliExitCode.Error, missingExit);
        Assert.Equal(CliExitCode.Usage, badExit);
    }

    // A failed assertion exits 1 with expected-vs-actual in the message (including clickable's breakdown) and every check in the JSON details.
    [Fact]
    public async Task AssertionFailed_ReportsFailingChecksAndDetails()
    {
        var checks = new List<AssertCheck>
        {
            new("exists", true, true, true),
            new("enabled", true, false, false),
            new("name", "OK", null, false),
            new("clickable", true, false, false, "enabled=True visible=True unobscured=False"),
        };
        var command = new Command("probe");
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, _ => throw new AssertionFailedException(checks)));

        var (stdout, _, exitCode) = await CliTestHelper.RunAsync(command, "--json");

        using var doc = JsonDocument.Parse(stdout);
        var error = doc.RootElement.GetProperty("error");
        var message = error.GetProperty("message").GetString();
        Assert.Equal(CliExitCode.CheckFailed, exitCode);
        Assert.Equal("assertion_failed", error.GetProperty("code").GetString());
        Assert.Contains("enabled: expected True, actual False", message);
        Assert.Contains("name: expected \"OK\", actual unknown", message);
        Assert.Contains("(enabled=True visible=True unobscured=False)", message);
        Assert.Equal(4, error.GetProperty("details").GetProperty("checks").GetArrayLength());
    }
}
