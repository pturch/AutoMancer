// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Runtime.InteropServices;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Dpi;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Core;

// Wraps a running target application — launch, attach by PID/title, and the window handle used to find/act on its elements.
public sealed class AppSession : IAsyncDisposable
{
    public string SessionId { get; }
    public int ProcessId => _process.Id;
    public IntPtr RootWindowHandle { get; }

    private readonly Process _process;

    // Runs once per process, before any session is created, so every coordinate query that follows sees true physical pixels.
    static AppSession() => DpiAwareness.EnsureConfigured();

    // Direct construction is intentionally private — use the static factory methods.
    private AppSession(string sessionId, Process process, IntPtr rootWindowHandle)
    {
        SessionId = sessionId;
        RootWindowHandle = rootWindowHandle;
        _process = process;
    }

    // Starts the target executable and waits until its main window is visible
    // WindowMatch disambiguates which window counts as "launched" when the executable might have multiple candidate windows.
    public static async Task<AppSession> LaunchAsync(
        string executablePath,
        string? arguments = null,
        int timeoutMs = 15_000,
        WindowMatchOptions? windowMatch = null,
        IEngineLogger? logger = null,
        CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo(executablePath, arguments ?? string.Empty)
        {
            UseShellExecute = true
        };

        var startFailureMessage = $"Failed to start process: {executablePath}";
        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            throw new AppLaunchError(startFailureMessage, ex);
        }

        // UseShellExecute returns null instead of throwing when it hands off to an already-running instance rather than spawning a new one.
        if (process is null)
            throw new AppLaunchError(startFailureMessage);

        var processName = process.ProcessName;

        // Snapshot windows that already existed before this launch, so RequireNewWindow can tell a genuinely new window apart from a pre-existing one being reused.
        var preExistingPids = windowMatch?.RequireNewWindow == true
            ? Process.GetProcessesByName(processName).Where(p => p.Id != process.Id && p.MainWindowHandle != IntPtr.Zero).Select(p => p.Id).ToHashSet()
            : null;

        var stopwatch = Stopwatch.StartNew();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (!process.HasExited)
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero && WindowSatisfiesCriteria(process.MainWindowHandle, process.Id, windowMatch))
                {
                    logger?.Info("App launched", new { executablePath, pid = process.Id, elapsedMs = stopwatch.ElapsedMilliseconds });
                    return FromProcess(process);
                }
            }

            // Packaged apps (e.g. Windows 11's Notepad) can hand activation off to a pre-existing windowed instance without ever exiting or getting a window of their own, so this scan must run every iteration, not just after our own process exits.
            var windowed = Process.GetProcessesByName(processName).FirstOrDefault(p =>
                p.Id != process.Id
                && p.MainWindowHandle != IntPtr.Zero
                && WindowSatisfiesCriteria(p.MainWindowHandle, p.Id, windowMatch, preExistingPids));
            if (windowed is not null)
            {
                // We're returning windowed, not process — kill our own spawned launcher so it can't later surface its own separate, untracked window (observed in practice: it can win the handoff race and get a real window well after this method already returned).
                if (!process.HasExited)
                    try { process.Kill(); } catch { }

                logger?.Info("App launched (reused existing window)", new { executablePath, pid = windowed.Id, elapsedMs = stopwatch.ElapsedMilliseconds });
                return FromProcess(windowed);
            }

            await Task.Delay(200, ct).ConfigureAwait(false);
        }

        // Don't leave the spawned process orphaned on a failed launch — a leftover single-instance stub only confuses the next launch attempt.
        if (!process.HasExited)
            try { process.Kill(); } catch { }

        throw new AppLaunchError(
            $"Process started (PID {process.Id}) but no window appeared within {timeoutMs} ms: {executablePath}");
    }

    // Single predicate behind every window-locating method: rejects pid if it's in excludedPids (LaunchAsync's RequireNewWindow baseline — null everywhere else), then accepts if criteria is null or every non-null constraint in criteria holds.
    private static bool WindowSatisfiesCriteria(IntPtr windowHandle, int pid, WindowMatchOptions? criteria, IReadOnlySet<int>? excludedPids = null)
    {
        if (excludedPids?.Contains(pid) == true) return false; // This pid is excluded

        if (criteria is null) return true; // Accept anything

        if (criteria.ExpectedPid is not null && pid != criteria.ExpectedPid) // Rule out this window if it's the wrong process
            return false;

        if (criteria.TitleContains is not null && !NativeMethods.GetWindowTitle(windowHandle).Contains(criteria.TitleContains, StringComparison.OrdinalIgnoreCase)) // Rule out this window if the title doesn't match
            return false;

        if (criteria.ClassName is not null && !string.Equals(NativeMethods.GetWindowClassName(windowHandle), criteria.ClassName, StringComparison.OrdinalIgnoreCase)) // Rule out this window if the class doesn't match
            return false;

        return true; // This window works
    }

    // Wraps an already-running process identified by PID; windowMatch optionally verifies its main window's title/class before accepting it.
    public static Task<AppSession> AttachByPidAsync(int pid, WindowMatchOptions? windowMatch = null, IEngineLogger? logger = null, CancellationToken ct = default)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(pid);
        }
        catch (ArgumentException ex)
        {
            throw new AppLaunchError($"No process with PID {pid} found.", ex);
        }

        process.Refresh();

        if (process.MainWindowHandle == IntPtr.Zero)
            throw new AppLaunchError($"Process {pid} ({process.ProcessName}) has no main window handle.");

        if (!WindowSatisfiesCriteria(process.MainWindowHandle, process.Id, windowMatch))
            throw new AppLaunchError($"Process {pid} ({process.ProcessName})'s main window did not match the given window criteria.");

        logger?.Info("Attached to process", new { pid = process.Id, processName = process.ProcessName });
        return Task.FromResult(FromProcess(process));
    }

    // Finds the first windowed process whose title contains the given string (case-insensitive).
    public static Task<AppSession> AttachByTitleAsync(string title, IEngineLogger? logger = null, CancellationToken ct = default) =>
        AttachByTitleAsync(new WindowMatchOptions { TitleContains = title }, logger, ct);

    // Finds the first windowed process whose window matches windowMatch (must set TitleContains — that's this method's whole purpose); use this overload for extra disambiguation (e.g. ClassName) beyond a bare title.
    public static Task<AppSession> AttachByTitleAsync(WindowMatchOptions windowMatch, IEngineLogger? logger = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(windowMatch);
        if (windowMatch.TitleContains is null)
            throw new ArgumentException("windowMatch.TitleContains is required.", nameof(windowMatch));

        var match = Process.GetProcesses()
            .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero && WindowSatisfiesCriteria(p.MainWindowHandle, p.Id, windowMatch));

        if (match is null)
            throw new AppLaunchError($"No process with a window matching title '{windowMatch.TitleContains}' found.");

        logger?.Info("Attached to process", new { pid = match.Id, processName = match.ProcessName });
        return Task.FromResult(FromProcess(match));
    }

    // Terminates the target process immediately; no-op if it has already exited. When entireProcessTree is true, also terminates every process it spawned.
    public void KillApp(bool entireProcessTree = false)
    {
        if (!_process.HasExited)
            _process.Kill(entireProcessTree);
    }

    // Waits until the process has actually exited (event-based, not polled), or timeoutMs elapses
    // Use after KillApp() to know a single-instance app's next launch won't reuse its still-closing window as a new tab.
    public async Task WaitForExitAsync(int timeoutMs = 5_000, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try { await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
    }

    // Releases the underlying Process object without killing the app.
    public ValueTask DisposeAsync()
    {
        _process.Dispose();
        return ValueTask.CompletedTask;
    }

    // Creates a session from an already-validated process with a known window handle.
    private static AppSession FromProcess(Process process) =>
        new(Guid.NewGuid().ToString("N"), process, process.MainWindowHandle);

    // Polls all top-level windows for one owned by ownerPid whose title contains titleContains; returns null on timeout.
    public static Task<AppSession?> FindDialogAsync(int ownerPid, string titleContains, int timeoutMs = 3_000, IEngineLogger? logger = null, CancellationToken ct = default) =>
        FindDialogAsync(new WindowMatchOptions { ExpectedPid = ownerPid, TitleContains = titleContains }, timeoutMs, logger, ct);

    // Polls all top-level windows for one matching windowMatch (must set ExpectedPid and TitleContains — a dialog is always looked up by owner + title); use this overload for extra disambiguation (e.g. ClassName). Returns null on timeout.
    public static async Task<AppSession?> FindDialogAsync(
        WindowMatchOptions windowMatch,
        int timeoutMs = 3_000,
        IEngineLogger? logger = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(windowMatch);
        if (windowMatch.ExpectedPid is null)
            throw new ArgumentException("windowMatch.ExpectedPid is required.", nameof(windowMatch));
        if (windowMatch.TitleContains is null)
            throw new ArgumentException("windowMatch.TitleContains is required.", nameof(windowMatch));

        var stopwatch = Stopwatch.StartNew();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var windowHandle = FindWindow(windowMatch);
            if (windowHandle != IntPtr.Zero)
            {
                logger?.Info("Dialog found", new { windowMatch.ExpectedPid, windowMatch.TitleContains, elapsedMs = stopwatch.ElapsedMilliseconds });
                return new AppSession(Guid.NewGuid().ToString("N"), Process.GetProcessById(windowMatch.ExpectedPid.Value), windowHandle);
            }
            await Task.Delay(200, ct).ConfigureAwait(false);
        }
        return null;
    }

    // Enumerates visible top-level windows to find the first one matching criteria.
    private static IntPtr FindWindow(WindowMatchOptions criteria)
    {
        IntPtr found = IntPtr.Zero;
        NativeMethods.EnumWindows((windowHandle, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(windowHandle, out var pid);
            if (NativeMethods.IsWindowVisible(windowHandle) && WindowSatisfiesCriteria(windowHandle, (int)pid, criteria))
            {
                found = windowHandle;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    // Activates a UWP/MSIX packaged app by AUMID and waits until its host process shows a window matching windowMatch (if given).
    public static async Task<AppSession> LaunchPackagedAsync(
        string aumid,
        string? arguments = null,
        int timeoutMs = 15_000,
        WindowMatchOptions? windowMatch = null,
        IEngineLogger? logger = null,
        CancellationToken ct = default)
    {
        var managerType = Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"))
            ?? throw new AppLaunchError($"ApplicationActivationManager COM class not registered on this system.");
        var manager = (IApplicationActivationManager)Activator.CreateInstance(managerType)!;

        var hr = manager.ActivateApplication(aumid, arguments, 0, out var pid);
        if (hr < 0)
            throw new AppLaunchError($"ActivateApplication failed for AUMID '{aumid}' (HRESULT 0x{hr:X8}).");

        var stopwatch = Stopwatch.StartNew();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var process = Process.GetProcessById((int)pid);
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero && WindowSatisfiesCriteria(process.MainWindowHandle, process.Id, windowMatch))
                {
                    logger?.Info("Packaged app launched", new { aumid, pid, elapsedMs = stopwatch.ElapsedMilliseconds });
                    return FromProcess(process);
                }
            }
            catch (ArgumentException) { /* process not yet visible */ }

            await Task.Delay(200, ct).ConfigureAwait(false);
        }

        // Same reasoning as LaunchAsync — don't leave the activated process orphaned if it never shows a window.
        try
        {
            var process = Process.GetProcessById((int)pid);
            if (!process.HasExited) process.Kill();
        }
        catch { /* already gone, or couldn't be killed */ }

        throw new AppLaunchError($"Packaged app '{aumid}' (PID {pid}) did not show a window within {timeoutMs} ms.");
    }

    // Bypasses launch/attach validation to build a session for unit tests that mock providers and never touch a real window.
    internal static AppSession CreateForTesting(Process process, IntPtr rootWindowHandle) =>
        new(Guid.NewGuid().ToString("N"), process, rootWindowHandle);

    // COM interface for activating packaged (UWP/MSIX) apps by Application User Model ID; the Guid below is the IID QueryInterface resolves.
    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig] int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
            uint options,
            out uint processId);
        [PreserveSig] int ActivateForFile(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            IntPtr itemArray,
            [MarshalAs(UnmanagedType.LPWStr)] string? verb,
            out uint processId);
        [PreserveSig] int ActivateForProtocol(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            IntPtr itemArray,
            out uint processId);
    }
}
