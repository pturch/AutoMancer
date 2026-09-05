// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Shared Task Manager instance for a test class — launched once in InitializeAsync, killed in DisposeAsync.
public sealed class TaskManagerFixture : IAsyncLifetime
{
    public App App { get; private set; } = null!;

    // Launches Task Manager and warms up on a snapshot having real descendants, not just a resolved root window.
    public async Task InitializeAsync()
    {
        App = await App.LaunchAsync("taskmgr.exe");
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            var snapshot = await App.SnapshotAsync();
            if (snapshot is [{ Children.Count: > 0 }]) return;
            await Task.Delay(200);
        }
    }

    // Kills Task Manager; doesn't wait for confirmed exit since Task Manager can throw "Access is denied" right after Kill().
    public async Task DisposeAsync()
    {
        App.Kill();
        await App.DisposeAsync();
    }
}
