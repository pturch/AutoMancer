// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Shared Task Manager instance for a test class — launched once in InitializeAsync, killed in DisposeAsync.
public sealed class TaskManagerFixture : IAsyncLifetime
{
    public App App { get; private set; } = null!;

    // Launches Task Manager and warms up on a snapshot having real descendants, not just a resolved root window.
    public async Task InitializeAsync()
    {
        App = await App.LaunchAsync("taskmgr.exe");
        await Expect(App, () => App.SnapshotAsync()).ToSatisfyAsync(snapshot => snapshot is [{ Children.Count: > 0 }], "to have real descendants", timeoutMs: 8_000);
    }

    // Kills Task Manager; doesn't wait for confirmed exit since Task Manager can throw "Access is denied" right after Kill(). No-ops if InitializeAsync itself failed to launch, so that failure surfaces on its own instead of being masked by a NullReferenceException here.
    public async Task DisposeAsync()
    {
        if (App is null) return;
        App.Kill();
        await App.DisposeAsync();
    }
}
