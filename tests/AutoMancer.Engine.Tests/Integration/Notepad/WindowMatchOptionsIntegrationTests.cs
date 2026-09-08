// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies WindowMatchOptions disambiguates which window LaunchAsync attaches to; in the "Notepad" collection to serialize foreground/window use.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class WindowMatchOptionsIntegrationTests
{
    [Fact]
    public async Task LaunchAsync_TitleContains_MatchesLaunchedWindow()
    {
        var app = await App.LaunchAsync("notepad.exe", windowMatch: new WindowMatchOptions { TitleContains = "Notepad" });
        try
        {
            Assert.Contains("Notepad", Process.GetProcessById(app.ProcessId).MainWindowTitle);
        }
        finally
        {
            await app.KillAsync();
        }
    }

    // A title that can never match forces the poll loop to exhaust its budget; AppSession's own orphan-kill cleans up the spawned process.
    [Fact]
    public async Task LaunchAsync_TitleContains_NoMatchingWindow_ThrowsAppLaunchError()
    {
        var options = new AppOptions { LaunchTimeoutMs = 3_000, Logger = null };
        await Assert.ThrowsAsync<AppLaunchError>(() =>
            App.LaunchAsync("notepad.exe", options, new WindowMatchOptions { TitleContains = "__AutoMancer_NoSuchWindow__" }));
    }

    // With a Notepad window already open, RequireNewWindow must not accept it as "the" launched window — single-instance reuse never produces a genuinely new top-level window, so this launch should time out rather than silently attach to the existing one.
    [Fact]
    public async Task LaunchAsync_RequireNewWindow_DoesNotReuseAlreadyOpenWindow()
    {
        var first = await App.LaunchAsync("notepad.exe");
        try
        {
            var options = new AppOptions { LaunchTimeoutMs = 3_000, Logger = null };
            await Assert.ThrowsAsync<AppLaunchError>(() =>
                App.LaunchAsync("notepad.exe", options, new WindowMatchOptions { RequireNewWindow = true }));
        }
        finally
        {
            await first.KillAsync();
        }
    }

    // ExpectedPid pins the match to a specific, already-known process — the strongest filter, since title/class can coincidentally collide with an unrelated process sharing the same executable name.
    [Fact]
    public async Task LaunchAsync_ExpectedPid_MatchesTheGivenProcess()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var reattached = await App.LaunchAsync("notepad.exe", windowMatch: new WindowMatchOptions { ExpectedPid = app.ProcessId });

            Assert.Equal(app.ProcessId, reattached.ProcessId);
        }
        finally
        {
            await app.KillAsync();
        }
    }

    // A PID that belongs to no window forces the poll loop to exhaust its budget rather than falling back to matching by name alone.
    [Fact]
    public async Task LaunchAsync_ExpectedPid_WrongPid_ThrowsAppLaunchError()
    {
        var options = new AppOptions { LaunchTimeoutMs = 3_000, Logger = null };
        await Assert.ThrowsAsync<AppLaunchError>(() =>
            App.LaunchAsync("notepad.exe", options, new WindowMatchOptions { ExpectedPid = 999_999_999 }));
    }
}
