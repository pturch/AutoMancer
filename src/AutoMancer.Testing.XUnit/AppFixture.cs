// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;

namespace AutoMancer.Testing.XUnit;

// Shared App instance for an xUnit collection; launched once in InitializeAsync, killed in DisposeAsync.
public abstract class AppFixture : IAsyncLifetime
{
    // The App instance shared by every test in this fixture's collection.
    public App App { get; private set; } = null!;

    // Launches the app under test via the derived fixture's CreateAppAsync.
    public async Task InitializeAsync() => App = await CreateAppAsync();

    // Kills the app, runs any app-specific teardown, then releases the process handle.
    public async Task DisposeAsync()
    {
        App.Kill();
        await OnKilledAsync();
        await App.DisposeAsync();
    }

    // Launches or attaches to the app under test; implemented per test suite.
    protected abstract Task<App> CreateAppAsync();

    // Defaults to the process-wide AutoMancerTestOptions policy; override only for a fixture that needs to diverge from it.
    protected virtual string LogDirectory => AutoMancerTestOptions.LogDirectory;

    // Runs after Kill() and before the process handle is released; override for app-specific teardown timing (e.g. a single-instance app's re-launch race).
    protected virtual Task OnKilledAsync() => Task.CompletedTask;
}
