// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli.Tests;

public sealed class RobustnessTests
{
    // Many threads saving and loading the state at once never throw, never read a busy file as empty, and leave no temp files behind.
    [Fact]
    public async Task State_ConcurrentSavesAndLoads_AreSafe()
    {
        var target = CliTarget.ForProcess(Environment.ProcessId);
        CliState.Save(new CliStateData { Target = target });
        var emptyReads = 0;

        var writers = Enumerable.Range(0, 6).Select(w => Task.Run(() =>
        {
            for (var i = 0; i < 40; i++)
                CliState.Save(new CliStateData { Target = target, Settings = new CliSettings { ActionDelayMs = w * 100 + i } });
        }));
        var readers = Enumerable.Range(0, 6).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 40; i++)
                if (CliState.Load().Target is null)
                    Interlocked.Increment(ref emptyReads);
        }));
        await Task.WhenAll(writers.Concat(readers));

        Assert.Equal(0, emptyReads);
        Assert.Equal(target, CliState.Load().Target);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(CliState.FilePath)!, "*.tmp"));
    }

    // A tolerance that isn't a real fraction (NaN, infinity, out of range) is a usage error.
    [Theory]
    [InlineData("nan")]
    [InlineData("NaN")]
    [InlineData("inf")]
    [InlineData("-0.5")]
    [InlineData("1.5")]
    public async Task Tolerance_NotAFraction_IsUsageError(string value)
    {
        var (_, _, exitCode) = await CliTestHelper.RunAsync(
            AssertCommand.Build(), [.. CliTestHelper.Self, "--expected", "a.png", "--actual", "b.png", $"--tolerance={value}"]);

        Assert.Equal(CliExitCode.Usage, exitCode);
    }

    // An empty locator value is a usage error: an unset shell variable must not match an arbitrary element.
    [Theory]
    [InlineData("find")]
    [InlineData("click")]
    [InlineData("hover")]
    [InlineData("focus")]
    public async Task EmptyValue_IsUsageError(string name)
    {
        var command = name switch
        {
            "find" => FindCommand.Build(),
            "click" => ClickCommand.Build(),
            "hover" => HoverCommand.Build(),
            _ => FocusCommand.Build(),
        };

        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(command, [.. CliTestHelper.Self, "--by", "name", "--value", ""]);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Contains("can't be empty", stderr);
    }

    // An empty scope value is a usage error too.
    [Fact]
    public async Task EmptyScopeValue_IsUsageError()
    {
        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(
            FindCommand.Build(), [.. CliTestHelper.Self, "--by", "name", "--value", "x", "--within-by", "name", "--within-value", ""]);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Contains("can't be empty", stderr);
    }

    // Whitespace after an operator is part of the value, and spaces around a bare check name are ignored.
    [Fact]
    public void CheckParse_KeepsSignificantWhitespace()
    {
        Assert.Equal("  spaces  ", AssertChecks.Parse("text=  spaces  ").Value);
        Assert.Equal("a b ", AssertChecks.Parse("name~a b ").Value);
        Assert.Equal("enabled", AssertChecks.Parse("  enabled  ").Name);
        Assert.True(AssertChecks.Parse("enabled ").Want);
    }

    // configure --attach with nothing, or only spaces, is a usage error and does not list the machine's windows.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Attach_Empty_IsUsageError(string value)
    {
        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(ConfigureCommand.Build(), "--attach", value);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Contains("--attach needs a PID or a process name", stderr);
        Assert.DoesNotContain("matches several", stderr);
    }

    // launch with an empty or blank executable is a usage error, not an attempt to start nothing.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Launch_BlankExecutable_IsUsageError(string exe)
    {
        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(LaunchCommand.Build(), exe);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Contains("needs the path or name", stderr);
    }

    // Repeated provider names are saved once.
    [Fact]
    public async Task ProviderChain_DuplicatesCollapse()
    {
        CliTestHelper.ClearState();

        await CliTestHelper.RunAsync(ConfigureCommand.Build(), "--provider-chain", "uia3, UIA3 ,win32,uia3");

        Assert.Equal(["uia3", "win32"], CliState.Load().Settings.ProviderChain!);
    }

    // Pointing --out, --expected or --actual at a folder is a usage error that says so.
    [Fact]
    public async Task FolderInsteadOfFile_IsUsageError()
    {
        using var dir = new TempDir();

        var (_, shotErr, shotExit) = await CliTestHelper.RunAsync(ScreenshotCommand.Build(), [.. CliTestHelper.Self, "--out", dir.Path]);
        var (_, imgErr, imgExit) = await CliTestHelper.RunAsync(AssertCommand.Build(), [.. CliTestHelper.Self, "--expected", dir.Path, "--actual", dir.Path]);

        Assert.Equal(CliExitCode.Usage, shotExit);
        Assert.Contains("is a folder", shotErr);
        Assert.Equal(CliExitCode.Usage, imgExit);
        Assert.Contains("not folders", imgErr);
    }
}
