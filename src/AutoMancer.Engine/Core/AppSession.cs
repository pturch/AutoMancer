// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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

    // Starts the target executable and waits until its main window is visible.
    public static async Task<AppSession> LaunchAsync(
        string executablePath,
        string? arguments = null,
        int timeoutMs = 10_000,
        IEngineLogger? logger = null,
        CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo(executablePath, arguments ?? string.Empty)
        {
            UseShellExecute = true
        };

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            throw new AppLaunchError($"Failed to start process: {executablePath}", ex);
        }

        if (process is null)
            throw new AppLaunchError($"Failed to start process: {executablePath}");

        var processName = process.ProcessName;
        var stopwatch = Stopwatch.StartNew();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (!process.HasExited)
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    logger?.Info("App launched", new { executablePath, pid = process.Id, elapsedMs = stopwatch.ElapsedMilliseconds });
                    return FromProcess(process);
                }
            }
            else
            {
                // Packaged apps (e.g. Windows 11's Notepad) exit their launcher stub once activation hands off to the real windowed host process, which may be a pre-existing instance reused as a new tab.
                var windowed = Process.GetProcessesByName(processName).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
                if (windowed is not null)
                {
                    logger?.Info("App launched (reused existing window)", new { executablePath, pid = windowed.Id, elapsedMs = stopwatch.ElapsedMilliseconds });
                    return FromProcess(windowed);
                }
            }

            await Task.Delay(200, ct).ConfigureAwait(false);
        }

        throw new AppLaunchError(
            $"Process started (PID {process.Id}) but no window appeared within {timeoutMs} ms: {executablePath}");
    }

    // Wraps an already-running process identified by PID.
    public static Task<AppSession> AttachByPidAsync(int pid, IEngineLogger? logger = null, CancellationToken ct = default)
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

        logger?.Info("Attached to process", new { pid = process.Id, processName = process.ProcessName });
        return Task.FromResult(FromProcess(process));
    }

    // Finds the first windowed process whose title contains the given string (case-insensitive).
    public static Task<AppSession> AttachByTitleAsync(string title, IEngineLogger? logger = null, CancellationToken ct = default)
    {
        var match = Process.GetProcesses()
            .FirstOrDefault(p =>
                p.MainWindowHandle != IntPtr.Zero &&
                p.MainWindowTitle.Contains(title, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            throw new AppLaunchError($"No process with a window title containing '{title}' found.");

        logger?.Info("Attached to process", new { pid = match.Id, processName = match.ProcessName });
        return Task.FromResult(FromProcess(match));
    }

    // Terminates the target process immediately; no-op if it has already exited.
    public void KillApp()
    {
        if (!_process.HasExited)
            _process.Kill();
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
    public static async Task<AppSession?> FindDialogAsync(
        int ownerPid,
        string titleContains,
        int timeoutMs = 3_000,
        IEngineLogger? logger = null,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var windowHandle = FindWindowByPidAndTitle(ownerPid, titleContains);
            if (windowHandle != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(windowHandle, out var pid);
                logger?.Info("Dialog found", new { ownerPid, titleContains, elapsedMs = stopwatch.ElapsedMilliseconds });
                return new AppSession(Guid.NewGuid().ToString("N"), Process.GetProcessById((int)pid), windowHandle);
            }
            await Task.Delay(200, ct).ConfigureAwait(false);
        }
        return null;
    }

    // Enumerates top-level windows to find one belonging to targetPid whose title contains the search string.
    private static IntPtr FindWindowByPidAndTitle(int targetPid, string titleContains)
    {
        IntPtr found = IntPtr.Zero;
        NativeMethods.EnumWindows((windowHandle, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(windowHandle, out var pid);
            if (pid == (uint)targetPid && NativeMethods.IsWindowVisible(windowHandle))
            {
                var sb = new StringBuilder(512);
                NativeMethods.GetWindowText(windowHandle, sb, sb.Capacity);
                if (sb.ToString().Contains(titleContains, StringComparison.OrdinalIgnoreCase))
                {
                    found = windowHandle;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    // Activates a UWP/MSIX packaged app by AUMID and waits until its host process shows a window.
    public static async Task<AppSession> LaunchPackagedAsync(
        string aumid,
        int timeoutMs = 10_000,
        IEngineLogger? logger = null,
        CancellationToken ct = default)
    {
        var managerType = Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"))
            ?? throw new AppLaunchError($"ApplicationActivationManager COM class not registered on this system.");
        var manager = (IApplicationActivationManager)Activator.CreateInstance(managerType)!;

        var hr = manager.ActivateApplication(aumid, null, 0, out var pid);
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
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    logger?.Info("Packaged app launched", new { aumid, pid, elapsedMs = stopwatch.ElapsedMilliseconds });
                    return FromProcess(process);
                }
            }
            catch (ArgumentException) { /* process not yet visible */ }

            await Task.Delay(200, ct).ConfigureAwait(false);
        }

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
