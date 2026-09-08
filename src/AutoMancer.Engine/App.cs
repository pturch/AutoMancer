// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.ComponentModel;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine;

// High-level automation façade: holds a session and provider chain, exposes action-first methods that read like manual test steps.
public sealed class App : IAsyncDisposable
{
    private readonly AppSession _session;
    private readonly ElementResolver _resolver;
    private readonly int _actionDelayMs;
    private readonly int _foregroundActivationTimeoutMs;
    private readonly bool _killEntireProcessTree;
    private readonly HeldKeyTracker _heldKeys = new();
    private readonly HeldMouseButtonTracker _heldMouseButtons = new();
    private readonly IEngineLogger? _logger;
    private readonly IEngineLogger? _windowsEventLogger;
    private readonly IEngineLogger? _combinedLogger;
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;

    // The PID of the target process — useful for re-attaching after a session change.
    public int ProcessId => _session.ProcessId;

    // The target app's top-level window handle — for driving foreground activation yourself (e.g. via your own SetForegroundWindow/GetAncestor calls) on an app whose window hierarchy defeats the built-in check; pair with AppOptions.ForegroundActivationTimeoutMs = 0 to disable that check.
    public IntPtr RootWindowHandle => _session.RootWindowHandle;

    // Resolves windowHandle to its top-level ancestor — useful when managing foreground activation yourself for an element whose NativeHandle (cast to IUIAutomationElement).CurrentNativeWindowHandle is a child window rather than the app's own top-level window.
    public static IntPtr GetTopLevelWindow(IntPtr windowHandle) => NativeMethods.GetTopLevelWindow(windowHandle);

    // The logger this App was configured with (via AppOptions.Logger), if any — lets a test bridge it into its own output/DI logging.
    public IEngineLogger? Logger => _logger;

    // The implicit wait this App was configured with (via AppOptions.ImplicitWaitMs) — the default timeout callers can reuse for their own polling loops.
    public int ImplicitWaitMs { get; }

    // The poll interval this App was configured with (via AppOptions.PollIntervalMs) — the default cadence callers can reuse for their own polling loops.
    public int PollIntervalMs { get; }

    // Private — callers use the static factory methods.
    private App(AppSession session, IEnumerable<IElementProvider> providers, AppOptions options)
    {
        _session = session;
        _actionDelayMs = options.ActionDelayMs;
        _foregroundActivationTimeoutMs = options.ForegroundActivationTimeoutMs;
        _killEntireProcessTree = options.KillEntireProcessTree;
        _logger = options.Logger;
        _windowsEventLogger = options.WindowsEventLogger;
        _combinedLogger = options.CombinedLogger;
        ImplicitWaitMs = options.ImplicitWaitMs;
        PollIntervalMs = options.PollIntervalMs;
        _resolver = new ElementResolver(
            providers,
            new ElementProviderOptions
            {
                ProviderChain = [.. options.ProviderChain],
                ImplicitWaitMs = options.ImplicitWaitMs,
                PollIntervalMs = options.PollIntervalMs,
            },
            logger: options.Logger,
            foregroundActivationTimeoutMs: options.ForegroundActivationTimeoutMs);
    }

    // Starts the target executable and waits until its main window is visible; windowMatch disambiguates which window counts as "launched" when the executable might have multiple candidate windows.
    public static async Task<App> LaunchAsync(string executablePath, AppOptions? options = null, WindowMatchOptions? windowMatch = null, CancellationToken ct = default)
    {
        options ??= AppOptions.Default;
        var session = await AppSession.LaunchAsync(executablePath, arguments: options.Arguments, timeoutMs: options.LaunchTimeoutMs, windowMatch: windowMatch, logger: options.Logger, ct: ct);
        return new App(session, BuildProviders(options.ProviderChain), options);
    }

    // Wraps an already-running process identified by PID; windowMatch optionally verifies its main window's title/class before accepting it.
    public static async Task<App> AttachByPidAsync(int pid, AppOptions? options = null, WindowMatchOptions? windowMatch = null, CancellationToken ct = default)
    {
        options ??= AppOptions.Default;
        var session = await AppSession.AttachByPidAsync(pid, windowMatch, options.Logger, ct);
        return new App(session, BuildProviders(options.ProviderChain), options);
    }

    // Polls for a dialog owned by ownerPid whose title contains titleContains; use for modal dialogs, which don't change Process.MainWindowTitle.
    public static Task<App?> FindDialogAsync(int ownerPid, string titleContains, AppOptions? options = null, int timeoutMs = 3_000, CancellationToken ct = default) =>
        FindDialogAsync(new WindowMatchOptions { ExpectedPid = ownerPid, TitleContains = titleContains }, options, timeoutMs, ct);

    // Polls for a dialog matching windowMatch (must set ExpectedPid + TitleContains); use this overload for extra disambiguation (e.g. ClassName) beyond owner + title.
    public static async Task<App?> FindDialogAsync(WindowMatchOptions windowMatch, AppOptions? options = null, int timeoutMs = 3_000, CancellationToken ct = default)
    {
        options ??= AppOptions.Default;
        var session = await AppSession.FindDialogAsync(windowMatch, timeoutMs, options.Logger, ct);
        return session is null ? null : new App(session, BuildProviders(options.ProviderChain), options);
    }

    // Activates a UWP/MSIX packaged app by its Application User Model ID (AUMID) and waits for a window matching windowMatch (if given).
    public static async Task<App> LaunchPackagedAsync(string aumid, AppOptions? options = null, WindowMatchOptions? windowMatch = null, CancellationToken ct = default)
    {
        options ??= AppOptions.Default;
        var session = await AppSession.LaunchPackagedAsync(aumid, arguments: options.Arguments, timeoutMs: options.LaunchTimeoutMs, windowMatch: windowMatch, logger: options.Logger, ct: ct);
        return new App(session, BuildProviders(options.ProviderChain), options);
    }

    // Finds the first windowed process whose title contains the given string (case-insensitive).
    public static Task<App> AttachByTitleAsync(string title, AppOptions? options = null, CancellationToken ct = default) =>
        AttachByTitleAsync(new WindowMatchOptions { TitleContains = title }, options, ct);

    // Finds the first windowed process whose window matches windowMatch (must set TitleContains — that's this method's whole purpose); use this overload for extra disambiguation (e.g. ClassName) beyond a bare title.
    public static async Task<App> AttachByTitleAsync(WindowMatchOptions windowMatch, AppOptions? options = null, CancellationToken ct = default)
    {
        options ??= AppOptions.Default;
        var session = await AppSession.AttachByTitleAsync(windowMatch, options.Logger, ct);
        return new App(session, BuildProviders(options.ProviderChain), options);
    }

    // Returns a new App bound to the same session but with different options — useful for warmup or provider-specific operations without creating a new process.
    public App WithOptions(AppOptions options) => new(_session, BuildProviders(options.ProviderChain), options);

    // Bypasses real provider construction to build an App around fake providers, for unit tests exercising App/Expect() retry and diagnostics logic without a live window.
    internal static App CreateForTesting(AppSession session, IEnumerable<IElementProvider> providers, AppOptions? options = null) =>
        new(session, providers, options ?? AppOptions.Default);

    // Finds the first element matching the locator; waits up to ImplicitWaitMs before throwing.
    public Task<ElementHandle> FindAsync(Locator locator, CancellationToken ct = default)
        => _resolver.FindAsync(locator, _session, ct);

    // Finds all elements matching the locator in a single pass; returns empty if none match.
    public Task<IReadOnlyList<ElementHandle>> FindAllAsync(Locator locator, CancellationToken ct = default)
        => _resolver.FindAllAsync(locator, _session, ct);

    // Finds the element and reads its current value via ValuePattern or TextPattern; returns null when the resolved provider has no operator (e.g. Win32) or neither pattern is supported.
    public async Task<string?> GetValueAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        return element.Operator is null ? null : await element.Operator.TryGetValueAsync(element, ct);
    }

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
        EnsureWindowNotMinimized();
        await ClickAction.ExecuteAsync(element, button, modifiers, ct);
        _logger?.Info("Clicked", new { locator.Strategy, locator.Value });
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
        EnsureWindowNotMinimized();
        await DoubleClickAction.ExecuteAsync(element, ct);
        _logger?.Info("Double-clicked", new { locator.Strategy, locator.Value });
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and moves the mouse to its center without clicking, to trigger hover states/tooltips.
    public async Task HoverAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        EnsureWindowNotMinimized();
        await HoverAction.ExecuteAsync(element, ct);
        _logger?.Info("Hovered", new { locator.Strategy, locator.Value });
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and types the given text into it, replacing its whole value if the provider supports native set-value rather than typing at the caret or over a selection — see TypeDirectAsync for real keystroke behavior; waits ActionDelayMs after for the UI to settle.
    public async Task TypeAsync(Locator locator, string text, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        EnsureWindowNotMinimized();
        await TypeAction.ExecuteAsync(element, text, ct);
        _logger?.Info("Typed", new { locator.Strategy, locator.Value });
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and clears its content; waits ActionDelayMs after clearing for the UI to settle.
    public async Task ClearAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        EnsureWindowNotMinimized();
        await ClearAction.ExecuteAsync(element, ct);
        _logger?.Info("Cleared", new { locator.Strategy, locator.Value });
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and clicks its screen center via SendInput; use for WinUI3 buttons where InvokePattern skips the pointer-event pipeline.
    public async Task ClickAtAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        var (x, y) = ElementInputHelpers.GetCenter(element);
        await ClickAtAsync(x, y, ct);
        _logger?.Info("Clicked at element center", new { locator.Strategy, locator.Value });
    }

    // Clicks at a physical screen coordinate without finding a UIA element; useful for tools like the fill bucket that have no accessible target.
    public async Task ClickAtAsync(int x, int y, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            EnsureWindowReady();
            NativeMethods.SendMouseClick(x, y, _logger);
        }, ct);
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
    }

    // Sends text as Unicode keystrokes to whichever element currently has focus in this window; bypasses element search.
    public Task TypeDirectAsync(string text, CancellationToken ct = default)
        => Task.Run(() =>
        {
            EnsureWindowReady();
            TypeAction.SendUnicodeText(text, _logger);
        }, ct);

    // Presses a named key against the root window, optionally with modifiers held down (e.g. Ctrl+A).
    public async Task PressKeyAsync(Key key, KeyModifiers modifiers = default, CancellationToken ct = default)
    {
        await Task.Run(EnsureWindowReady, ct);
        await KeyboardAction.PressKeyCoreAsync(key, modifiers, _logger, ct);
    }

    // Presses modifiers plus every key in keys simultaneously against the root window — for chords needing more than one non-modifier key (e.g. Ctrl+A+K).
    public async Task HotkeyAsync(KeyModifiers modifiers, IReadOnlyList<Key> keys, CancellationToken ct = default)
    {
        await Task.Run(EnsureWindowReady, ct);
        await KeyboardAction.HotkeyCoreAsync(modifiers, keys, _logger, ct);
    }

    // Presses a key down without releasing it; tracked so Kill/Dispose can release it even if KeyUpAsync is never called. The send and the tracking happen in the same synchronous unit — no async-continuation gap where Kill/Dispose could drain an untracked-but-already-physically-down key.
    public async Task KeyDownAsync(Key key, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            EnsureWindowReady();
            KeyboardAction.KeyDownNow(key, _logger);
            _heldKeys.Add(key);
        }, ct);
    }

    // Releases a previously held key; untracks it only once the release actually lands, so a cancelled/failed send leaves it tracked rather than silently forgotten.
    public async Task KeyUpAsync(Key key, CancellationToken ct = default)
    {
        await KeyboardAction.KeyUpCoreAsync(key, _logger, ct);
        _heldKeys.Remove(key);
    }

    // Moves the mouse by a relative (dx, dy) pixel offset against the root window — for camera-look style input rather than absolute positioning.
    public async Task MoveMouseRelativeAsync(int dx, int dy, CancellationToken ct = default)
    {
        await Task.Run(EnsureWindowReady, ct);
        await MouseMoveAction.MoveRelativeCoreAsync(dx, dy, _logger, ct);
    }

    // Drags in a straight line between two physical screen coordinates; waits ActionDelayMs after completion.
    public async Task DragAsync(int fromX, int fromY, int toX, int toY, CancellationToken ct = default)
    {
        await Task.Run(EnsureWindowReady, ct);
        await DragAction.DragCoreAsync(fromX, fromY, toX, toY, 20, _logger, ct, _heldMouseButtons);
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
    }

    // Drags through a sequence of physical screen coordinates in one continuous press; waits ActionDelayMs after completion.
    public async Task DragThroughAsync(IReadOnlyList<(int X, int Y)> waypoints, CancellationToken ct = default)
    {
        await Task.Run(EnsureWindowReady, ct);
        await DragAction.DragThroughCoreAsync(waypoints, _logger, ct, _heldMouseButtons);
        if (_actionDelayMs > 0) await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and scrolls it into view; waits ActionDelayMs after scrolling for the UI to settle. 
    // No EnsureWindowNotMinimized() call — ScrollAction only ever uses ScrollItemPattern, never SendInput or SetForegroundWindow, so window state doesn't affect it.
    public async Task ScrollIntoViewAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        await ScrollAction.ExecuteAsync(element, ct);
        _logger?.Info("Scrolled into view", new { locator.Strategy, locator.Value });
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and scrolls under it via mouse wheel; deltaY/deltaX are in wheel notches (positive deltaY scrolls up).
    public async Task ScrollWheelAsync(Locator locator, int deltaX, int deltaY, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        EnsureWindowNotMinimized();
        await ScrollWheelAction.ExecuteAsync(element, deltaX, deltaY, ct);
        _logger?.Info("Scrolled", new { locator.Strategy, locator.Value, deltaX, deltaY });
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Finds the element and sets keyboard focus to it.
    public async Task SetFocusAsync(Locator locator, CancellationToken ct = default)
    {
        var element = await _resolver.FindAsync(locator, _session, ct);
        EnsureWindowNotMinimized();
        await SetFocusAction.ExecuteAsync(element, ct);
        _logger?.Info("Focused", new { locator.Strategy, locator.Value });
        if (_actionDelayMs > 0)
            await Task.Delay(_actionDelayMs, ct);
    }

    // Returns the window's current bounding rectangle in physical screen coordinates.
    public Task<Rect> GetWindowSizeAsync(CancellationToken ct = default)
        => WindowAction.GetSizeAsync(_session.RootWindowHandle, ct);

    // Moves the window's top-left corner to (x, y) in physical screen coordinates; size is unchanged.
    public Task MoveWindowAsync(int x, int y, CancellationToken ct = default)
        => WindowAction.MoveCoreAsync(_session.RootWindowHandle, x, y, _logger, ct);

    // Resizes the window to (width × height) in physical pixels; position is unchanged.
    public Task ResizeWindowAsync(int width, int height, CancellationToken ct = default)
        => WindowAction.ResizeCoreAsync(_session.RootWindowHandle, width, height, _logger, ct);

    // Changes the window's visual state (Normal, Maximized, Minimized).
    public Task SetWindowStateAsync(WindowState state, CancellationToken ct = default)
        => WindowAction.SetVisualStateCoreAsync(_session.RootWindowHandle, state, _logger, ct);

    // Reports whether the window is currently minimized — lets a caller check and recover (e.g. via SetWindowStateAsync) before an action would otherwise throw WindowMinimizedError.
    public bool IsWindowMinimized() => NativeMethods.IsIconic(_session.RootWindowHandle);

    // Closes the root window; tries WindowPattern.Close() first, falls back to posting WM_CLOSE.
    public Task CloseWindowAsync(CancellationToken ct = default)
        => WindowAction.CloseCoreAsync(_session.RootWindowHandle, _logger, ct);

    // Terminates the target process immediately; no-op if it has already exited.
    public void Kill()
    {
        ReleaseHeldInputBestEffort();
        KillAppBestEffort();
    }

    // Terminates the target process and waits until it has actually exited — use in test teardown instead of Kill() plus a guessed settle delay, since a single-instance app's next launch can otherwise reuse this process's still-closing window as a new tab.
    public async Task KillAsync(int timeoutMs = 5_000, CancellationToken ct = default)
    {
        ReleaseHeldInputBestEffort();
        KillAppBestEffort();
        await _session.WaitForExitAsync(timeoutMs, ct);
    }

    // Waits until the process has actually exited, or timeoutMs elapses — use after a plain Kill() call instead of a guessed settle delay.
    public Task WaitForExitAsync(int timeoutMs = 5_000, CancellationToken ct = default)
        => _session.WaitForExitAsync(timeoutMs, ct);

    // Kills the app (if still running), writes the Windows Event Log / combined-timeline artifacts if configured, and releases the underlying process handle.
    public ValueTask DisposeAsync()
    {
        ReleaseHeldInputBestEffort();
        KillAppBestEffort();
        TryWriteWindowsEventLogArtifact();
        return _session.DisposeAsync();
    }

    // Kills the underlying process, swallowing a failure to terminate it (e.g. access denied against an elevated target) so that alone can never block the rest of Kill/KillAsync/DisposeAsync from running.
    private void KillAppBestEffort()
    {
        try { _session.KillApp(_killEntireProcessTree); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { _logger?.Warn("KillApp failed to terminate the process", new { error = ex.Message }); }
    }

    // Writes the Windows Event Log / combined-timeline artifacts if configured, swallowing any failure (no permission to read the Application/System logs, a transient Event Log service issue, etc.) so a diagnostics-only step can never prevent _session.DisposeAsync() from running and releasing the process handle.
    private void TryWriteWindowsEventLogArtifact()
    {
        try { WindowsEventLogArtifact.Write(_startedAtUtc, _logger, _windowsEventLogger, _combinedLogger); }
        catch (Exception ex) { _logger?.Warn("Windows Event Log artifact write failed", new { error = ex.Message }); }
    }

    // Releases held keys and the held mouse button for teardown, swallowing InputDeliveryError from either so a partially-delivered release can never block Kill/Dispose from actually terminating the process — matching ReleaseHeldKeys/ReleaseHeldMouseButtons' own best-effort intent, which SendInputs now throwing on partial delivery would otherwise violate.
    private void ReleaseHeldInputBestEffort()
    {
        try { ReleaseHeldKeys(); }
        catch (InputDeliveryError ex) { _logger?.Warn("ReleaseHeldKeys only partially delivered during teardown", new { ex.RequestedCount, ex.DeliveredCount }); }

        try { ReleaseHeldMouseButtons(); }
        catch (InputDeliveryError ex) { _logger?.Warn("ReleaseHeldMouseButtons only partially delivered during teardown", new { ex.RequestedCount, ex.DeliveredCount }); }
    }

    // Sends KeyUp for every key still recorded as held, in one synchronous batched SendInput call, so a crash or early Kill/Dispose never leaves a key stuck down at the OS level — killing the target process does not do this on its own, since held-key state lives in the OS, not the process.
    private void ReleaseHeldKeys() => KeyboardAction.ReleaseKeysNow(_heldKeys.DrainHeld(), _logger);

    // Sends LeftUp if the left mouse button is still recorded as held from an aborted DragAsync/DragThroughAsync, so Kill/Dispose don't leave it stuck down at the OS level.
    private void ReleaseHeldMouseButtons()
    {
        if (_heldMouseButtons.DrainLeftDown())
            NativeMethods.SendInputs([SendInputBuilders.MouseInputAt(0, 0, NativeMethods.MouseEventFlags.LeftUp)], _logger);
    }

    // Throws WindowMinimizedError if minimized, else foregrounds the root window (throwing WindowActivationError if that fails); used where there's no resolved element to foreground instead. Skips the foreground check when ForegroundActivationTimeoutMs is 0 — see RootWindowHandle for managing activation yourself.
    private void EnsureWindowReady()
    {
        EnsureWindowNotMinimized();
        if (_foregroundActivationTimeoutMs > 0)
            NativeMethods.EnsureForegroundOrThrow(_session.RootWindowHandle, _foregroundActivationTimeoutMs);
    }

    // Throws WindowMinimizedError if the root window is minimized; SendInput can't target its client area.
    private void EnsureWindowNotMinimized()
    {
        // Checked against root, not the element's window — locator methods foreground the element themselves instead.
        if (NativeMethods.IsIconic(_session.RootWindowHandle))
            throw new WindowMinimizedError(_session.RootWindowHandle);
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

    // Tracks keys held via KeyDownAsync so Kill/Dispose can release them all even after a crash; locked since App's key methods can be called concurrently. Private to App — no other class needs to see a held key mid-hold.
    internal sealed class HeldKeyTracker
    {
        private readonly object _gate = new();
        private readonly HashSet<Key> _held = [];

        // Records key as held.
        public void Add(Key key) { lock (_gate) _held.Add(key); }

        // Forgets key — call after releasing it normally via KeyUpAsync.
        public void Remove(Key key) { lock (_gate) _held.Remove(key); }

        // Returns every key still recorded as held and forgets them all; returns empty on a second call.
        public IReadOnlyList<Key> DrainHeld()
        {
            lock (_gate)
            {
                var keys = _held.ToList();
                _held.Clear();
                return keys;
            }
        }
    }
}
