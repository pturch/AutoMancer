// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Core;

public sealed class AppSession : IAsyncDisposable
{
    public string SessionId { get; }
    public int ProcessId => _process.Id;
    public IntPtr RootWindowHandle { get; }

    private readonly Process _process;

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

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (process.MainWindowHandle == IntPtr.Zero && DateTime.UtcNow < deadline)
        {
            await Task.Delay(200, ct).ConfigureAwait(false);
            process.Refresh();
        }

        if (process.MainWindowHandle == IntPtr.Zero)
            throw new AppLaunchError(
                $"Process started (PID {process.Id}) but no window appeared within {timeoutMs} ms: {executablePath}");

        return FromProcess(process);
    }

    // Wraps an already-running process identified by PID.
    public static Task<AppSession> AttachByPidAsync(int pid, CancellationToken ct = default)
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

        return Task.FromResult(FromProcess(process));
    }

    // Finds the first windowed process whose title contains the given string (case-insensitive).
    public static Task<AppSession> AttachByTitleAsync(string title, CancellationToken ct = default)
    {
        var match = Process.GetProcesses()
            .FirstOrDefault(p =>
                p.MainWindowHandle != IntPtr.Zero &&
                p.MainWindowTitle.Contains(title, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            throw new AppLaunchError($"No process with a window title containing '{title}' found.");

        return Task.FromResult(FromProcess(match));
    }

    // Terminates the target process immediately; no-op if it has already exited.
    public void KillApp()
    {
        if (!_process.HasExited)
            _process.Kill();
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
}
