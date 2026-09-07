// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine;

// Configures the App façade: which providers to use, how long to wait for elements, and how long to pause after each action.
public sealed class AppOptions
{
    // Command-line arguments passed to the launched process by LaunchAsync/LaunchPackagedAsync; null launches with none. Has no effect on AttachByPidAsync/AttachByTitleAsync/FindDialogAsync — nothing to launch.
    public string? Arguments { get; init; }

    // When true, KillAsync/Kill/DisposeAsync terminate the entire process tree rather than just the tracked process — needed for apps (Electron, some Java apps) that spawn helper processes otherwise left running. Default false preserves the single-process kill every existing caller already gets.
    public bool KillEntireProcessTree { get; init; } = false;

    // Provider lookup order — each name must match a known IElementProvider.ProviderName.
    public IReadOnlyList<string> ProviderChain { get; init; } = ["uia3", "uia2", "win32"];

    // Total milliseconds to retry the provider chain before throwing ElementNotFoundError.
    public int ImplicitWaitMs { get; init; } = 5_000;

    // Milliseconds between retry attempts during implicit wait.
    public int PollIntervalMs { get; init; } = 500;

    // Milliseconds to pause after each ClickAsync/TypeAsync so the UI can settle (SendInput latency + render time); 0 disables it.
    public int ActionDelayMs { get; init; } = 150;

    // Milliseconds to retry foregrounding the target window (verified via GetForegroundWindow) before actions throw WindowActivationError.
    // 0 skips the check entirely, for apps whose window hierarchy defeats it — pair with App.RootWindowHandle to manage activation yourself.
    public int ForegroundActivationTimeoutMs { get; init; } = 3_000;

    // Milliseconds to wait for a launched/activated process's window to appear before LaunchAsync/LaunchPackagedAsync throw AppLaunchError.
    public int LaunchTimeoutMs { get; init; } = 15_000;

    // Trace of the engine's own retry/resolve process, e.g. for debugging flaky element timing. Defaults to plain text on stderr so it never pollutes a CLI command's stdout; pass null to disable, or your own IEngineLogger to redirect it.
    public IEngineLogger? Logger { get; init; } = new EngineLogger(Console.Error);

    // When set, AutoMancer queries the Windows Application/System event logs for Critical/Error/Warning entries since the run started and writes them here at DisposeAsync, in the same JSON-lines shape as Logger — a second, independent artifact from the OS itself rather than an inferred Win32 return code. Null (the default) skips the query entirely.
    public IEngineLogger? WindowsEventLogger { get; init; }

    // When set, merges this run's AutoMancer issues (Logger's own Warn/Error entries) with the Windows Event Log entries (see WindowsEventLogger) into one timestamp-sorted timeline artifact, written here at DisposeAsync — a third artifact for reviewing both together. Null (the default) skips it.
    public IEngineLogger? CombinedLogger { get; init; }

    // Sensible defaults for most automation scenarios. 
    // Note: every App created without explicit options shares this one Default.Logger instance — see EngineLogger's _issues comment on why that overlaps PullIssuesSince results across concurrent Apps; pass an explicit Logger per App if you're also using WindowsEventLogger/CombinedLogger.
    public static AppOptions Default { get; } = new();

    // Tuned for CI/test-runner use: a longer implicit wait tolerates slower CI machines, and a shorter action delay is safe because tests retry-assert via WaitForAsync/Expect() instead of relying on a fixed settle pause.
    public static AppOptions TestDefaults { get; } = new() { ImplicitWaitMs = 10_000, ActionDelayMs = 50 };
}
