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

    // Runs app-specific pre-kill cleanup, kills the app and waits for it to actually exit, runs any post-kill teardown, then releases the process handle.
    // No-ops if InitializeAsync itself failed to launch, so that failure surfaces on its own instead of being masked by a NullReferenceException here.
    // OnBeforeKillAsync throwing must not skip the kill itself — leaving the process running would leak it into the next test run.
    public async Task DisposeAsync()
    {
        if (App is null) return;
        try { await OnBeforeKillAsync(); }
        finally { await App.KillAsync(); }
        await OnKilledAsync();
        await App.DisposeAsync();
    }

    // Launches or attaches to the app under test; implemented per test suite.
    protected abstract Task<App> CreateAppAsync();

    // Defaults to the process-wide AutoMancerTestOptions policy; override only for a fixture that needs to diverge from it.
    protected virtual string LogDirectory => AutoMancerTestOptions.LogDirectory;

    // Runs while the app is still alive, right before it's killed; override for cleanup that needs a live app, e.g. discarding unsaved state.
    protected virtual Task OnBeforeKillAsync() => Task.CompletedTask;

    // Runs after the app has exited and before the process handle is released; override for app-specific teardown beyond waiting for exit.
    protected virtual Task OnKilledAsync() => Task.CompletedTask;
}
