// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine;

// High-level automation façade: holds a session and provider chain, exposes action-first methods that read like manual test steps.
public sealed class App : IAsyncDisposable
{
    private readonly AppSession _session;
    private readonly ElementResolver _resolver;
    private readonly int _actionDelayMs;
    private readonly HeldKeyTracker _heldKeys = new();

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

    // Polls for a dialog owned by ownerPid whose title contains titleContains; use for modal dialogs, which don't change Process.MainWindowTitle.
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

    // Captures the root window's current on-screen pixels as a PNG.
    public Task<byte[]> ScreenshotAsync(CancellationToken ct = default)
        => ScreenshotAction.CaptureAsync(_session.RootWindowHandle, ct);

    // Waits until every provider in the chain returns null for the locator; throws ElementStillPresentError if it's still found after ImplicitWaitMs.
    public Task WaitUntilGoneAsync(Locator locator, CancellationToken ct = default)
        => _resolver.WaitUntilGoneAsync(locator, _session, ct);

    // Waits until the located element satisfies condition; throws ElementConditionTimeoutError if it never does within ImplicitWaitMs.
    public Task<ElementHandle> WaitForAsync(Locator locator, Func<ElementHandle, bool> condition, CancellationToken ct = default)
        => _resolver.WaitForAsync(locator, condition, _session, ct);

    // Finds the element and clicks it; waits ActionDelayMs after the click for the UI to settle.
    public async Task ClickAsync(Locator locator, MouseButton button = MouseButton.Left, KeyModifiers modifiers = default, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        await ClickAction.ExecuteAsync(element, button, modifiers, ct);
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and right-clicks it; waits ActionDelayMs after the click for the UI to settle.
    public Task RightClickAsync(Locator locator, CancellationToken ct = default)
        => ClickAsync(locator, MouseButton.Right, ct: ct);

    // Finds the element and double-clicks it; waits ActionDelayMs after the click for the UI to settle.
    public async Task DoubleClickAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        await DoubleClickAction.ExecuteAsync(element, ct);
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and moves the mouse to its center without clicking, to trigger hover states/tooltips.
    public async Task HoverAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        await HoverAction.ExecuteAsync(element, ct);
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

    // Finds the element and clicks its screen center via SendInput; use for WinUI3 buttons where InvokePattern skips the pointer-event pipeline.
    public async Task ClickAtAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        var rect = element.BoundingRect;
        await ClickAtAsync((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2), ct);
    }

    // Clicks at a physical screen coordinate without finding a UIA element; useful for tools like the fill bucket that have no accessible target.
    public async Task ClickAtAsync(int x, int y, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
            NativeMethods.SendMouseClick(x, y);
        }, ct);
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
    }

    // Sends text as Unicode keystrokes to whichever element currently has focus in this window; bypasses element search.
    public Task TypeDirectAsync(string text, CancellationToken ct = default)
        => Task.Run(() =>
        {
            NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
            TypeAction.SendUnicodeText(text);
        }, ct);

    // Presses a named key against the root window, optionally with modifiers held down (e.g. Ctrl+A).
    public async Task PressKeyAsync(Key key, KeyModifiers modifiers = default, CancellationToken ct = default)
    {
        NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
        await KeyboardAction.PressKeyAsync(key, modifiers, ct);
    }

    // Presses modifiers plus every key in keys simultaneously against the root window — for chords needing more than one non-modifier key (e.g. Ctrl+A+K).
    public async Task HotkeyAsync(KeyModifiers modifiers, IReadOnlyList<Key> keys, CancellationToken ct = default)
    {
        NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
        await KeyboardAction.HotkeyAsync(modifiers, keys, ct);
    }

    // Presses a key down without releasing it; tracked so Kill/Dispose can release it even if KeyUpAsync is never called. The send and the tracking happen in the same synchronous unit — no async-continuation gap where Kill/Dispose could drain an untracked-but-already-physically-down key.
    public Task KeyDownAsync(Key key, CancellationToken ct = default)
    {
        NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
        return Task.Run(() =>
        {
            KeyboardAction.KeyDownNow(key);
            _heldKeys.Add(key);
        }, ct);
    }

    // Releases a previously held key; untracks it only once the release actually lands, so a cancelled/failed send leaves it tracked rather than silently forgotten.
    public async Task KeyUpAsync(Key key, CancellationToken ct = default)
    {
        await KeyboardAction.KeyUpAsync(key, ct);
        _heldKeys.Remove(key);
    }

    // Moves the mouse by a relative (dx, dy) pixel offset against the root window — for camera-look style input rather than absolute positioning.
    public async Task MoveMouseRelativeAsync(int dx, int dy, CancellationToken ct = default)
    {
        NativeMethods.SetForegroundWindow(_session.RootWindowHandle);
        await MouseMoveAction.MoveRelativeAsync(dx, dy, ct);
    }

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

    // Finds the element and scrolls under it via mouse wheel; deltaY/deltaX are in wheel notches (positive deltaY scrolls up).
    public async Task ScrollWheelAsync(Locator locator, int deltaX, int deltaY, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        await ScrollWheelAction.ExecuteAsync(element, deltaX, deltaY, ct);
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and sets keyboard focus to it.
    public async Task SetFocusAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        await SetFocusAction.ExecuteAsync(element, ct);
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

    // Closes the root window; tries WindowPattern.Close() first, falls back to posting WM_CLOSE.
    public Task CloseWindowAsync(CancellationToken ct = default)
        => WindowAction.CloseAsync(_session.RootWindowHandle, ct);

    // Terminates the target process immediately; no-op if it has already exited.
    public void Kill()
    {
        ReleaseHeldKeys();
        _session.KillApp();
    }

    // Kills the app (if still running) and releases the underlying process handle.
    public ValueTask DisposeAsync()
    {
        ReleaseHeldKeys();
        _session.KillApp();
        return _session.DisposeAsync();
    }

    // Sends KeyUp for every key still recorded as held, in one synchronous batched SendInput call, so a crash or early Kill/Dispose never leaves a key stuck down at the OS level — killing the target process does not do this on its own, since held-key state lives in the OS, not the process.
    private void ReleaseHeldKeys() => KeyboardAction.ReleaseKeysNow(_heldKeys.DrainHeld());

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
