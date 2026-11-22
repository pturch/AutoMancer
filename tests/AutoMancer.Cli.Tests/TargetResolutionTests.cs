// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.CommandLine.Parsing;

namespace AutoMancer.Cli.Tests;

public sealed class TargetResolutionTests
{
    // A probe command that reports which PID target resolution picked, without attaching to anything.
    private static Command Probe()
    {
        var command = new Command("probe");
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, (pid, _) => Task.FromResult(new CliResult($"pid={pid}"))));
        return command;
    }

    // --pid wins over the saved current target.
    [Fact]
    public async Task ExplicitPid_BeatsCurrentTarget()
    {
        CliState.Save(new CliStateData { Target = CliTarget.ForProcess(Environment.ProcessId) });

        var (stdout, _, exitCode) = await CliTestHelper.RunAsync(Probe(), "--pid", "4242");

        Assert.Equal(0, exitCode);
        Assert.Equal("pid=4242", stdout.Trim());
    }

    // With no --pid, the current target is used when it's still the same running process.
    [Fact]
    public async Task NoPid_UsesLiveCurrentTarget()
    {
        CliState.Save(new CliStateData { Target = CliTarget.ForProcess(Environment.ProcessId) });

        var (stdout, _, exitCode) = await CliTestHelper.RunAsync(Probe());

        Assert.Equal(0, exitCode);
        Assert.Equal($"pid={Environment.ProcessId}", stdout.Trim());
    }

    // With neither, the command fails with exit 3 and says how to set a target.
    [Fact]
    public async Task NoPidNoTarget_FailsWithHint()
    {
        CliTestHelper.ClearState();

        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(Probe());

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Contains("launch", stderr);
        Assert.Contains("configure --attach", stderr);
    }

    // A current target whose process is gone fails with exit 3 and is forgotten, but saved settings are kept.
    [Fact]
    public async Task DeadCurrentTarget_FailsAndIsCleared_KeepingSettings()
    {
        CliState.Save(new CliStateData
        {
            Target = new CliTarget(2_000_000_000, DateTime.UtcNow, "gone"),
            Settings = new CliSettings { ImplicitWaitMs = 123 },
        });

        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(Probe());

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Contains("no longer running", stderr);
        Assert.Null(CliState.Load().Target);
        Assert.Equal(123, CliState.Load().Settings.ImplicitWaitMs);
    }

    // A current target whose PID now belongs to a different process (different start time) is treated as dead.
    [Fact]
    public async Task ReusedPid_IsTreatedAsDead()
    {
        var self = CliTarget.ForProcess(Environment.ProcessId);
        CliState.Save(new CliStateData { Target = self with { StartUtc = self.StartUtc.AddDays(-1) } });

        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(Probe());

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Contains("no longer running", stderr);
    }

    // The global --pid option works before the subcommand as well as after it.
    [Fact]
    public async Task Pid_WorksBeforeTheSubcommand()
    {
        var originalOut = Console.Out;
        var stdout = new StringWriter();
        Console.SetOut(stdout);
        try
        {
            var root = new RootCommand();
            root.Add(CliSession.JsonOption);
            root.Add(CliSession.PidOption);
            root.Add(Probe());

            var exitCode = await CliSession.InvokeAsync(root, ["--pid", "777", "probe"]);

            Assert.Equal(0, exitCode);
            Assert.Equal("pid=777", stdout.ToString().Trim());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    // The global --verbose flag is seen by the command, and is off when not given.
    [Fact]
    public async Task Verbose_IsSetOnlyWhenGiven()
    {
        var command = new Command("probe");
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, ct => Task.FromResult(new CliResult($"verbose={CliSession.Verbose}"))));

        var (off, _, _) = await CliTestHelper.RunAsync(command);
        var (on, _, _) = await CliTestHelper.RunAsync(command, "--verbose");

        Assert.Equal("verbose=False", off.Trim());
        Assert.Equal("verbose=True", on.Trim());
    }
}
