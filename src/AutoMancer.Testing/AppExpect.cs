// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Text.RegularExpressions;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Testing;

// Retry-asserting checks against the app window as a whole rather than one element.
public sealed class AppExpect
{
    private readonly App _app;
    private readonly ExpectOptions _options;

    // Constructed only via Expect(App, ExpectOptions).
    internal AppExpect(App app, ExpectOptions options)
    {
        _app = app;
        _options = options;
    }

    // Polls until the window's pixels match the baseline PNG within maxDifferenceRatio; a missing baseline is recorded from the current capture and fails so it gets reviewed. Throws ExpectFailedError on timeout.
    public Task ToMatchScreenshotAsync(string baselinePath, double maxDifferenceRatio = 0.01, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => ExpectScreenshot.MatchAsync(
            () => _app.TakeScreenshotAsync(baselinePath, ct),
            () => _app.CompareScreenshotAsync(baselinePath, maxDifferenceRatio, ct),
            () => ExpectDiagnostics.DescribeScreenshotAsync(_app, _options, ct),
            "window", baselinePath, maxDifferenceRatio, timeoutMs ?? _app.ImplicitWaitMs, pollIntervalMs ?? _app.PollIntervalMs, ct);

    // ---- State and geometry ----

    // Polls until the window's title equals expected exactly.
    public Task ToHaveTitleAsync(string expected, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(Title, title => title == expected, $"to have title \"{expected}\"", title => $"the title was \"{title}\"", timeoutMs, pollIntervalMs, ct);

    // Polls until the window's title matches pattern.
    public Task ToHaveTitleAsync(Regex pattern, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(Title, pattern.IsMatch, $"to have a title matching /{pattern}/", title => $"the title was \"{title}\"", timeoutMs, pollIntervalMs, ct);

    // Polls until the window is minimized.
    public Task ToBeMinimizedAsync(int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(() => NativeMethods.IsIconic(_app.RootWindowHandle), minimized => minimized, "to be minimized", _ => "it was not", timeoutMs, pollIntervalMs, ct);

    // Polls until the window is maximized.
    public Task ToBeMaximizedAsync(int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(() => NativeMethods.IsZoomed(_app.RootWindowHandle), maximized => maximized, "to be maximized", _ => "it was not", timeoutMs, pollIntervalMs, ct);

    // Polls until the window's top-level window is the foreground window.
    public Task ToBeForegroundAsync(int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(() => NativeMethods.GetForegroundWindow() == App.GetTopLevelWindow(_app.RootWindowHandle), foreground => foreground, "to be the foreground window", _ => "another window was in front", timeoutMs, pollIntervalMs, ct);

    // Polls until the window's bounds match expected to within tolerancePx on every edge, which absorbs DPI rounding.
    public Task ToHaveBoundsAsync(Rect expected, int tolerancePx = 2, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(() => _app.GetWindowSizeAsync(ct),
            actual => Math.Abs(actual.X - expected.X) <= tolerancePx && Math.Abs(actual.Y - expected.Y) <= tolerancePx
                   && Math.Abs(actual.Width - expected.Width) <= tolerancePx && Math.Abs(actual.Height - expected.Height) <= tolerancePx,
            $"to have bounds {expected} (±{tolerancePx}px)", actual => $"the bounds were {actual}", timeoutMs, pollIntervalMs, ct);

    // ---- Process and windows ----

    // Polls until the app's process is running.
    public Task ToBeRunningAsync(int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(() => !HasExited(), running => running, "to be running", _ => "the process had exited", timeoutMs, pollIntervalMs, ct);

    // Polls until the app's process has exited, e.g. after a Close or Exit action.
    public Task ToHaveExitedAsync(int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(HasExited, exited => exited, "to have exited", _ => "the process was still running", timeoutMs, pollIntervalMs, ct);

    // Polls until the app's process is processing window messages, i.e. not hung.
    public Task ToBeRespondingAsync(int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(IsResponding, responding => responding, "to be responding", _ => "it was not responding", timeoutMs, pollIntervalMs, ct);

    // Polls until the app has exactly count visible top-level windows, its main window included.
    public Task ToHaveWindowCountAsync(int count, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(() => VisibleWindows(_app.ProcessId).Count, actual => actual == count, $"to have {count} visible window(s)", actual => $"there were {actual}", timeoutMs, pollIntervalMs, ct);

    // Polls until a visible top-level window other than the main one matches match (pid defaults to the app's), using the same matching as WaitForWindowAsync.
    public Task ToHaveDialogAsync(WindowMatchOptions match, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
    {
        var pid = match.ExpectedPid ?? _app.ProcessId;
        return PollAsync(() => VisibleWindows(pid).Count(w => w != _app.RootWindowHandle && AppSession.WindowSatisfiesCriteria(w, pid, match)),
            matching => matching > 0, "to have a matching dialog open", _ => "none was", timeoutMs, pollIntervalMs, ct);
    }

    // Polls until the app has no visible top-level window besides its main one.
    public Task ToHaveNoDialogAsync(int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(() => VisibleWindows(_app.ProcessId).Where(w => w != _app.RootWindowHandle).Select(NativeMethods.GetWindowTitle).ToList(),
            others => others.Count == 0, "to have no dialog open", others => $"these were open: {string.Join(", ", others.Select(title => $"\"{title}\""))}", timeoutMs, pollIntervalMs, ct);

    // ---- Stability and resources ----

    // Waits until two consecutive window captures differ by at most maxDifferenceRatio of pixels; fails if they don't settle within the timeout.
    public async Task ToBeVisuallyStableAsync(double maxDifferenceRatio = 0.001, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
    {
        var timeout = timeoutMs ?? _app.ImplicitWaitMs;
        try
        {
            await _app.WaitForVisualStableAsync(pollInterval: TimeSpan.FromMilliseconds(pollIntervalMs ?? _app.PollIntervalMs), timeout: TimeSpan.FromMilliseconds(timeout), maxDifferenceRatio: maxDifferenceRatio, ct: ct);
        }
        catch (TimeoutException)
        {
            throw new ExpectFailedError($"Expected window to be visually stable within {maxDifferenceRatio:P2}, but it was still changing after {timeout}ms{await ExpectDiagnostics.DescribeScreenshotAsync(_app, _options, ct)}");
        }
    }

    // Polls until the app's private memory is below bytes, for leak-style checks at the end of a test.
    public Task ToHaveMemoryBelowAsync(long bytes, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(() => ResourceMonitor.Capture(_app).PrivateBytes, actual => actual < bytes, $"to use less than {bytes:N0} bytes of private memory", actual => $"it used {actual:N0}", timeoutMs, pollIntervalMs, ct);

    // Polls until the app's open handle count is below count, for leak-style checks at the end of a test.
    public Task ToHaveHandleCountBelowAsync(int count, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollAsync(() => ResourceMonitor.Capture(_app).HandleCount, actual => actual < count, $"to have fewer than {count} handles", actual => $"it had {actual}", timeoutMs, pollIntervalMs, ct);

    // ---- Helpers ----

    // Reads the main window's current title.
    private string Title() => NativeMethods.GetWindowTitle(_app.RootWindowHandle);

    // Returns every visible top-level window owned by processId.
    private static List<IntPtr> VisibleWindows(int processId) => NativeMethods.GetVisibleTopLevelWindows(new HashSet<int> { processId });

    // True once the app's process has exited (or its PID no longer resolves).
    private bool HasExited()
    {
        try { using var process = Process.GetProcessById(_app.ProcessId); return process.HasExited; }
        catch (ArgumentException) { return true; }
    }

    // True when the app's process is processing window messages; a process that has already exited reads as not responding.
    private bool IsResponding()
    {
        try { using var process = Process.GetProcessById(_app.ProcessId); return process.Responding; }
        catch (ArgumentException) { return false; }
    }

    // Polls a synchronous read until accept holds, failing with ExpectFailedError on timeout; every check above is this with its own read and wording.
    private Task PollAsync<T>(Func<T> read, Func<T, bool> accept, string description, Func<T, string> describeLast, int? timeoutMs, int? pollIntervalMs, CancellationToken ct)
        => PollAsync(() => Task.FromResult(read()), accept, description, describeLast, timeoutMs, pollIntervalMs, ct);

    // Async-read overload of PollAsync, for reads that are themselves async (window bounds).
    private Task PollAsync<T>(Func<Task<T>> read, Func<T, bool> accept, string description, Func<T, string> describeLast, int? timeoutMs, int? pollIntervalMs, CancellationToken ct)
        => ExpectPolling.PollOrFailAsync("window", read, accept, description, describeLast,
            timeoutMs ?? _app.ImplicitWaitMs, pollIntervalMs ?? _app.PollIntervalMs, () => ExpectDiagnostics.DescribeScreenshotAsync(_app, _options, ct), ct);
}
