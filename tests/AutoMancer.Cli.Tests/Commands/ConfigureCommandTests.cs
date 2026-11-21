// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.Json;
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class ConfigureCommandTests
{
    // With no options, configure shows no target and every setting as a default.
    [Fact]
    public async Task Show_WithNothingSaved_ListsDefaults()
    {
        CliTestHelper.ClearState();

        var (stdout, _, exitCode) = await CliTestHelper.RunAsync(ConfigureCommand.Build());

        Assert.Equal(0, exitCode);
        Assert.Contains("Target: (none)", stdout);
        Assert.Contains("implicit-wait", stdout);
        Assert.Contains("5000", stdout);
        Assert.DoesNotContain("(saved)", stdout);
    }

    // Saving settings persists them and the output reports them as saved, in text and JSON.
    [Fact]
    public async Task SetSettings_PersistsAndReportsThemAsSaved()
    {
        CliTestHelper.ClearState();

        var (_, _, exitCode) = await CliTestHelper.RunAsync(
            ConfigureCommand.Build(), "--implicit-wait", "1500", "--action-delay", "10", "--reresolve-on-stale", "false", "--provider-chain", "uia3, win32");
        var (json, _, _) = await CliTestHelper.RunAsync(ConfigureCommand.Build(), "--show", "--json");

        var settings = CliState.Load().Settings;
        Assert.Equal(0, exitCode);
        Assert.Equal(1500, settings.ImplicitWaitMs);
        Assert.Equal(10, settings.ActionDelayMs);
        Assert.False(settings.ReresolveOnStale);
        Assert.Equal(["uia3", "win32"], settings.ProviderChain!);
        using var doc = JsonDocument.Parse(json);
        var all = doc.RootElement.GetProperty("data").GetProperty("settings");
        Assert.Equal(1500, all.GetProperty("implicitWaitMs").GetProperty("value").GetInt32());
        Assert.Equal("saved", all.GetProperty("implicitWaitMs").GetProperty("source").GetString());
        Assert.Equal("default", all.GetProperty("pollIntervalMs").GetProperty("source").GetString());
    }

    // --reset clears saved settings back to the defaults but leaves the current target alone.
    [Fact]
    public async Task Reset_ClearsSettings_KeepsTarget()
    {
        var target = CliTarget.ForProcess(Environment.ProcessId);
        CliState.Save(new CliStateData { Target = target, Settings = new CliSettings { ImplicitWaitMs = 9 } });

        var (_, _, exitCode) = await CliTestHelper.RunAsync(ConfigureCommand.Build(), "--reset");

        Assert.Equal(0, exitCode);
        Assert.Null(CliState.Load().Settings.ImplicitWaitMs);
        Assert.Equal(target, CliState.Load().Target);
    }

    // --clear forgets the target but leaves saved settings alone.
    [Fact]
    public async Task Clear_ForgetsTarget_KeepsSettings()
    {
        CliState.Save(new CliStateData { Target = CliTarget.ForProcess(Environment.ProcessId), Settings = new CliSettings { ImplicitWaitMs = 9 } });

        var (_, _, exitCode) = await CliTestHelper.RunAsync(ConfigureCommand.Build(), "--clear");

        Assert.Equal(0, exitCode);
        Assert.Null(CliState.Load().Target);
        Assert.Equal(9, CliState.Load().Settings.ImplicitWaitMs);
    }

    // The target is shown with its PID, name and whether it is still running.
    [Fact]
    public async Task Show_DescribesTheCurrentTarget()
    {
        var self = CliTarget.ForProcess(Environment.ProcessId);
        CliState.Save(new CliStateData { Target = self });

        var (text, _, _) = await CliTestHelper.RunAsync(ConfigureCommand.Build());
        var (json, _, _) = await CliTestHelper.RunAsync(ConfigureCommand.Build(), "--json");

        Assert.Contains($"PID {self.Pid} ({self.Name}), running", text);
        using var doc = JsonDocument.Parse(json);
        var target = doc.RootElement.GetProperty("data").GetProperty("target");
        Assert.Equal(self.Pid, target.GetProperty("pid").GetInt32());
        Assert.True(target.GetProperty("alive").GetBoolean());
    }

    // Bad values and conflicting options are usage errors, and nothing is saved.
    [Theory]
    [InlineData("--implicit-wait", "-1")]
    [InlineData("--poll-interval", "0")]
    [InlineData("--action-delay", "-5")]
    [InlineData("--provider-chain", "uia3,bogus")]
    [InlineData("--provider-chain", ",")]
    [InlineData("--reresolve-on-stale", "maybe")]
    public async Task BadValues_AreUsageErrors_AndSaveNothing(string option, string value)
    {
        CliTestHelper.ClearState();

        var (_, _, exitCode) = await CliTestHelper.RunAsync(ConfigureCommand.Build(), option, value);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Equal(new CliStateData().Settings, CliState.Load().Settings);
    }

    // --attach and --clear together are a usage error.
    [Fact]
    public async Task AttachAndClear_Together_IsUsageError()
    {
        var (_, _, exitCode) = await CliTestHelper.RunAsync(ConfigureCommand.Build(), "--attach", "123", "--clear");

        Assert.Equal(CliExitCode.Usage, exitCode);
    }

    // A few running apps for the attach matcher tests.
    private static readonly IReadOnlyList<ConfigureCommand.WindowedApp> Apps =
    [
        new(100, "notepad", "Untitled - Notepad"),
        new(200, "Code", "main.cs - Visual Studio Code"),
        new(300, "charmap", "Character Map"),
        new(400, "ApplicationFrameHost", "Calculator"),
        new(500, "chrome", "Inbox - Mail"),
        new(501, "chrome", "Docs - Google Chrome"),
    ];

    // A number is a PID even if no running app has it, so the engine's own attach error can explain.
    [Fact]
    public void Resolve_Number_IsPassedThrough()
    {
        Assert.Equal(123456, ConfigureCommand.ResolveAttachTarget("123456", Apps));
    }

    // An exact process name matches, ignoring case and an optional .exe.
    [Theory]
    [InlineData("notepad", 100)]
    [InlineData("NOTEPAD", 100)]
    [InlineData("notepad.exe", 100)]
    [InlineData("code", 200)]
    [InlineData("Charmap.EXE", 300)]
    public void Resolve_ProcessName_Matches(string text, int expectedPid)
    {
        Assert.Equal(expectedPid, ConfigureCommand.ResolveAttachTarget(text, Apps));
    }

    // With no process of that name, part of a window title matches, ignoring case.
    [Theory]
    [InlineData("Character", 300)]
    [InlineData("visual studio", 200)]
    [InlineData("calculator", 400)]
    public void Resolve_WindowTitle_Matches(string text, int expectedPid)
    {
        Assert.Equal(expectedPid, ConfigureCommand.ResolveAttachTarget(text, Apps));
    }

    // A process name takes priority over a window title that merely contains the same text.
    [Fact]
    public void Resolve_ProcessNameBeatsTitle()
    {
        IReadOnlyList<ConfigureCommand.WindowedApp> apps =
        [
            new(1, "code", "main.cs"),
            new(2, "notepad", "code notes - Notepad"),
        ];

        Assert.Equal(1, ConfigureCommand.ResolveAttachTarget("code", apps));
    }

    // Several matches, by name or by title, are a usage error that lists the candidates and says how to pick one.
    [Theory]
    [InlineData("chrome")]
    [InlineData("Chrome.exe")]
    [InlineData("-")]
    public void Resolve_Several_IsUsageErrorListingThem(string text)
    {
        var ex = Assert.Throws<ArgumentException>(() => ConfigureCommand.ResolveAttachTarget(text, Apps));

        Assert.Contains("matches several apps", ex.Message);
        Assert.Contains("--attach <pid>", ex.Message);
    }

    // No match is target-not-found with a hint.
    [Fact]
    public void Resolve_NoMatch_IsTargetNotFound()
    {
        var ex = Assert.Throws<TargetNotFoundException>(() => ConfigureCommand.ResolveAttachTarget("nonesuch", Apps));

        Assert.Contains("process name or window title", ex.Message);
    }

    // Attaching to a PID that doesn't exist, or to a title nothing matches, fails as target-not-found and leaves the current target alone.
    [Theory]
    [InlineData("2000000000", "owns the window")]
    [InlineData("no-window-has-this-title-9f3a", "process name or window title")]
    public async Task Attach_Unattachable_ExitsTargetNotFound_AndKeepsTarget(string what, string hint)
    {
        var existing = CliTarget.ForProcess(Environment.ProcessId);
        CliState.Save(new CliStateData { Target = existing });

        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(ConfigureCommand.Build(), "--attach", what);

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Contains(hint, stderr);
        Assert.Equal(existing, CliState.Load().Target);
    }
}
