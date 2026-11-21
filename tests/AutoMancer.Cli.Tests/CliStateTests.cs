// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;

namespace AutoMancer.Cli.Tests;

public sealed class CliStateTests
{
    // A saved target and settings read back unchanged.
    [Fact]
    public void SaveLoad_RoundTrips()
    {
        var state = new CliStateData
        {
            Target = new CliTarget(123, new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), "notepad"),
            Settings = new CliSettings { ImplicitWaitMs = 1000, ReresolveOnStale = false, ProviderChain = ["uia3", "win32"] },
        };

        CliState.Save(state);
        var loaded = CliState.Load();

        Assert.Equal(state.Target, loaded.Target);
        Assert.Equal(1000, loaded.Settings.ImplicitWaitMs);
        Assert.False(loaded.Settings.ReresolveOnStale);
        Assert.Equal(["uia3", "win32"], loaded.Settings.ProviderChain!);
    }

    // A missing, empty, or corrupt state file gives the defaults instead of an error.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ not json")]
    public void Load_MissingOrCorrupt_GivesDefaults(string? content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CliState.FilePath)!);
        File.Delete(CliState.FilePath);
        if (content is not null)
            File.WriteAllText(CliState.FilePath, content);

        var loaded = CliState.Load();

        Assert.Null(loaded.Target);
        Assert.Null(loaded.Settings.ImplicitWaitMs);
    }

    // Saving over an existing state replaces it and leaves no temp files behind.
    [Fact]
    public void Save_Overwrites_AndLeavesNoTempFiles()
    {
        CliState.Save(new CliStateData { Settings = new CliSettings { ActionDelayMs = 1 } });
        CliState.Save(new CliStateData { Settings = new CliSettings { ActionDelayMs = 2 } });

        Assert.Equal(2, CliState.Load().Settings.ActionDelayMs);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(CliState.FilePath)!, "*.tmp"));
    }

    // A target recorded from the running test process is alive; one with a different start time is a reused PID and is not.
    [Fact]
    public void Target_IsAlive_ChecksStartTime()
    {
        var self = CliTarget.ForProcess(Environment.ProcessId);
        var reused = self with { StartUtc = self.StartUtc.AddDays(-1) };

        Assert.True(self.IsAlive());
        Assert.False(reused.IsAlive());
        Assert.False(new CliTarget(2_000_000_000, DateTime.UtcNow, "gone").IsAlive());
    }

    // Saved settings win over engine defaults, the per-call implicit wait wins over both, and unset values keep the defaults.
    [Fact]
    public void ToAppOptions_AppliesPrecedence()
    {
        var settings = new CliSettings { ImplicitWaitMs = 1000, ActionDelayMs = 7 };

        var saved = settings.ToAppOptions();
        var overridden = settings.ToAppOptions(implicitWaitMs: 50, arguments: "--x");
        var none = new CliSettings().ToAppOptions();

        Assert.Equal(1000, saved.ImplicitWaitMs);
        Assert.Equal(7, saved.ActionDelayMs);
        Assert.Equal(AppOptions.Default.PollIntervalMs, saved.PollIntervalMs);
        Assert.Equal(50, overridden.ImplicitWaitMs);
        Assert.Equal("--x", overridden.Arguments);
        Assert.Equal(AppOptions.Default.ImplicitWaitMs, none.ImplicitWaitMs);
        Assert.Equal(AppOptions.Default.ProviderChain, none.ProviderChain);
    }
}
