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

    // Polls all top-level windows for a dialog owned by ownerPid whose title contains titleContains; returns null on timeout.
    // Use this instead of AttachByTitleAsync when a modal dialog opens within an already-running process — modal dialogs
    // don't change Process.MainWindowTitle, so AttachByTitleAsync cannot find them.
    public static async Task<App?> FindDialogAsync(int ownerPid, string titleContains, AppOptions? options = null, int timeoutMs = 3_000, CancellationToken ct = default)
    {
        var session = await AppSession.FindDialogAsync(ownerPid, titleContains, timeoutMs, ct);
        return session is null ? null : new App(session, options ?? AppOptions.Default);
    }

    // Activates a UWP/MSIX packaged app by its Application User Model ID (AUMID) and waits for its window.
    public static async Task<App> LaunchPackagedAsync(string aumid, AppOptions? options = null, CancellationToken ct = default)
    {
        var session = await AppSession.LaunchPackagedAsync(aumid, ct: ct);
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

    // Finds the element and clears its content; waits ActionDelayMs after clearing for the UI to settle.
    public async Task ClearAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        await ClearAction.ExecuteAsync(element, ct);
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Presses and releases a virtual-key code against the root window; use for non-printable keys like Enter (0x0D), Escape (0x1B), Tab (0x09).
    public Task PressKeyAsync(ushort vk, CancellationToken ct = default)
        => Task.Run(() =>
        {
            NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
            NativeMethods.SendVkKey(vk);
        }, ct);

    // Finds the element and clicks at its screen center via SendInput, bypassing InvokePattern.
    // Use for WinUI3 tool-palette buttons where InvokePattern fires the UIA event but does not
    // go through the pointer-event pipeline the app needs to switch active state.
    public async Task ClickAtAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        var rect = element.BoundingRect;
        await ClickAtAsync((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2), ct);
    }

    // Clicks at a physical screen coordinate without finding a UIA element; useful for tools like the
    // fill bucket where the target point has no accessible element.
    public async Task ClickAtAsync(int x, int y, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
            NativeMethods.SendMouseClick(x, y);
        }, ct);
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
    }

    // Presses a modifier+key chord against the root window; e.g. (0x12, 0x44) for Alt+D, (0x11, 0x41) for Ctrl+A.
    public Task PressChordAsync(ushort modifier, ushort key, CancellationToken ct = default)
        => Task.Run(() =>
        {
            NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
            NativeMethods.SendVkChord(modifier, key);
        }, ct);

    // Sends text as Unicode keystrokes to whichever element currently has focus in this window; bypasses element search.
    public Task TypeDirectAsync(string text, CancellationToken ct = default)
        => Task.Run(() =>
        {
            NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
            TypeAction.SendUnicodeText(text);
        }, ct);

    // Drags in a straight line between two physical screen coordinates; waits ActionDelayMs after completion.
    public async Task DragAsync(int fromX, int fromY, int toX, int toY, CancellationToken ct = default)
    {
        Providers.NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
        await DragAction.DragAsync(fromX, fromY, toX, toY, ct: ct);
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
    }

    // Drags through a sequence of physical screen coordinates in one continuous press; waits ActionDelayMs after completion.
    public async Task DragThroughAsync(IReadOnlyList<(int X, int Y)> waypoints, CancellationToken ct = default)
    {
        Providers.NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
        await DragAction.DragThroughAsync(waypoints, ct);
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and scrolls it into view; waits ActionDelayMs after scrolling for the UI to settle.
    public async Task ScrollIntoViewAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        await ScrollAction.ExecuteAsync(element, ct);
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Returns the window's current bounding rectangle in physical screen coordinates.
    public Task<Rect> GetWindowSizeAsync(CancellationToken ct = default)
        => WindowAction.GetSizeAsync(_session.RootWindowHandle, ct);

    // Moves the window's top-left corner to (x, y) in physical screen coordinates; size is unchanged.
    public Task MoveWindowAsync(int x, int y, CancellationToken ct = default)
        => WindowAction.MoveAsync(_session.RootWindowHandle, x, y, ct);

    // Resizes the window to (width × height) in physical pixels; position is unchanged.
    public Task ResizeWindowAsync(int width, int height, CancellationToken ct = default)
        => WindowAction.ResizeAsync(_session.RootWindowHandle, width, height, ct);

    // Changes the window's visual state (Normal, Maximized, Minimized).
    public Task SetWindowStateAsync(WindowState state, CancellationToken ct = default)
        => WindowAction.SetVisualStateAsync(_session.RootWindowHandle, state, ct);

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
