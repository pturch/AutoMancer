// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine;

// High-level automation façade: holds a session and provider chain, exposes action-first methods
// that read like manual test steps rather than raw engine API calls.
public sealed class App : IAsyncDisposable
{
    private readonly AppSession _session;
    private readonly ElementResolver _resolver;
    private readonly int _actionDelayMs;

    // The PID of the target process — useful for re-attaching after a session change.
    public int ProcessId => _session.ProcessId;

    // Private — callers use the static factory methods.
    private App(AppSession session, AppOptions options)
    {
        _session = session;
        _actionDelayMs = options.ActionDelayMs;
        _resolver = new ElementResolver(
            BuildProviders(options.ProviderChain),
            new ElementProviderOptions
            {
                ProviderChain = [.. options.ProviderChain],
                ImplicitWaitMs = options.ImplicitWaitMs,
                PollIntervalMs = options.PollIntervalMs,
            });
    }

    // Starts the target executable and waits until its main window is visible.
    public static async Task<App> LaunchAsync(string executablePath, AppOptions? options = null, CancellationToken ct = default)
    {
        var session = await AppSession.LaunchAsync(executablePath, ct: ct);
        return new App(session, options ?? AppOptions.Default);
    }

    // Wraps an already-running process identified by PID.
    public static async Task<App> AttachByPidAsync(int pid, AppOptions? options = null, CancellationToken ct = default)
    {
        var session = await AppSession.AttachByPidAsync(pid, ct);
        return new App(session, options ?? AppOptions.Default);
    }

    // Finds the first windowed process whose title contains the given string (case-insensitive).
    public static async Task<App> AttachByTitleAsync(string title, AppOptions? options = null, CancellationToken ct = default)
    {
        var session = await AppSession.AttachByTitleAsync(title, ct);
        return new App(session, options ?? AppOptions.Default);
    }

    // Returns a new App bound to the same session but with different options — useful for warmup or provider-specific operations without creating a new process.
    public App WithOptions(AppOptions options) => new(_session, options);

    // Finds the first element matching the locator; waits up to ImplicitWaitMs before throwing.
    public Task<ElementHandle> FindAsync(Locator locator, CancellationToken ct = default)
        => _resolver.FindAsync(locator, _session, ct);

    // Finds all elements matching the locator in a single pass; returns empty if none match.
    public Task<IReadOnlyList<ElementHandle>> FindAllAsync(Locator locator, CancellationToken ct = default)
        => _resolver.FindAllAsync(locator, _session, ct);

    // Snapshots the element tree from the first provider in the chain that returns a non-empty result; null if all providers return empty.
    public Task<IReadOnlyList<ElementSnapshot>?> SnapshotAsync(CancellationToken ct = default)
        => _resolver.TrySnapshotAsync(_session, ct);

    // Finds the element and clicks it; waits ActionDelayMs after the click for the UI to settle.
    public async Task ClickAsync(Locator locator, ClickType clickType = ClickType.Left, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        await ClickAction.ExecuteAsync(element, clickType, ct);
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and types the given text into it; waits ActionDelayMs after typing for the UI to settle.
    public async Task TypeAsync(Locator locator, string text, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        await TypeAction.ExecuteAsync(element, text, ct);
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Terminates the target process immediately; no-op if it has already exited.
    public void Kill() => _session.KillApp();

    // Kills the app (if still running) and releases the underlying process handle.
    public ValueTask DisposeAsync()
    {
        _session.KillApp();
        return _session.DisposeAsync();
    }

    // Instantiates one provider per name in the chain; unknown names are silently dropped.
    private static IReadOnlyList<IElementProvider> BuildProviders(IReadOnlyList<string> chain)
    {
        var all = new Dictionary<string, IElementProvider>(StringComparer.OrdinalIgnoreCase)
        {
            ["uia3"] = new Uia3Provider(),
            ["uia2"] = new Uia2Provider(),
            ["win32"] = new Win32Provider(),
        };
        return chain.Where(all.ContainsKey).Select(n => all[n]).ToList();
    }
}
