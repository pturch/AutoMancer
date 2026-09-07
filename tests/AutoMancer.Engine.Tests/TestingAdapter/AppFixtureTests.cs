// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;

namespace AutoMancer.Engine.Tests.TestingAdapter;

public sealed class AppFixtureTests
{
    // Wrapped in AppSession.CreateForTesting and already exited by the time it's used, so App.Kill()'s
    // HasExited check always short-circuits — this must never risk a real Process.Kill() call landing
    // on anything still running (e.g. the test host itself, if Process.GetCurrentProcess() were used instead).
    private static Process ExitedProcess()
    {
        var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        process.WaitForExit(2000);
        return process;
    }

    private sealed class RecordingFixture : AppFixture
    {
        public List<string> Events { get; } = [];

        protected override Task<App> CreateAppAsync()
        {
            Events.Add("CreateApp");
            var session = AppSession.CreateForTesting(ExitedProcess(), (IntPtr)1);
            return Task.FromResult(App.CreateForTesting(session, [], new AppOptions { Logger = null }));
        }

        protected override Task OnKilledAsync()
        {
            Events.Add("OnKilled");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task InitializeAsync_CallsCreateAppAsync_AndExposesTheResultAsApp()
    {
        var fixture = new RecordingFixture();

        await fixture.InitializeAsync();

        Assert.NotNull(fixture.App);
        Assert.Equal(["CreateApp"], fixture.Events);
    }

    // OnKilledAsync exists specifically for app-specific teardown timing (e.g. a single-instance app's
    // re-launch race, per its own doc comment) — DisposeAsync must actually call it, not just Kill().
    [Fact]
    public async Task DisposeAsync_CallsOnKilledAsync()
    {
        var fixture = new RecordingFixture();
        await fixture.InitializeAsync();

        await fixture.DisposeAsync();

        Assert.Equal(["CreateApp", "OnKilled"], fixture.Events);
    }

    private sealed class ThrowingBeforeKillFixture : AppFixture
    {
        public Process? Process { get; private set; }

        protected override Task<App> CreateAppAsync()
        {
            Process = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 30 127.0.0.1 >nul")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;
            var session = AppSession.CreateForTesting(Process, (IntPtr)1);
            return Task.FromResult(App.CreateForTesting(session, [], new AppOptions { Logger = null }));
        }

        protected override Task OnBeforeKillAsync() => throw new InvalidOperationException("boom");
    }

    // OnBeforeKillAsync throwing must not skip the kill itself, or a live-running process leaks past DisposeAsync.
    [Fact]
    public async Task DisposeAsync_StillKillsTheProcess_WhenOnBeforeKillAsyncThrows()
    {
        var fixture = new ThrowingBeforeKillFixture();
        await fixture.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.DisposeAsync());

        fixture.Process!.Refresh();
        Assert.True(fixture.Process.HasExited);
    }
}
