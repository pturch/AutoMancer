// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Synthesizes named-key presses, modifier chords, and held key-down/up pairs via SendInput.
public static class KeyboardAction
{
    // Callers must foreground-activate the target window first (see App.PressKeyAsync/HotkeyAsync etc.) — these are pure input builders.

    // Presses modifiers, presses and releases key, then releases modifiers in reverse order in one batch.
    public static Task PressKeyAsync(Key key, KeyModifiers modifiers = default, CancellationToken ct = default)
        => HotkeyCoreAsync(modifiers, [key], null, ct);

    // Same as PressKeyAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task PressKeyCoreAsync(Key key, KeyModifiers modifiers, IEngineLogger? logger, CancellationToken ct)
        => HotkeyCoreAsync(modifiers, [key], logger, ct);

    // Presses modifiers, then every key in keys down together as a true simultaneous chord, then releases all in reverse order.
    public static Task HotkeyAsync(KeyModifiers modifiers, IReadOnlyList<Key> keys, CancellationToken ct = default)
        => HotkeyCoreAsync(modifiers, keys, null, ct);

    // Same as HotkeyAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task HotkeyCoreAsync(KeyModifiers modifiers, IReadOnlyList<Key> keys, IEngineLogger? logger, CancellationToken ct) => Task.Run(() =>
    {
        var modifierKeys = modifiers.ToVirtualKeys().ToList();
        var inputs = new List<NativeMethods.INPUT>();

        foreach (var vk in modifierKeys)
            inputs.Add(ClickAction.VkInput(vk, isKeyUp: false));

        // All key-downs happen before any key-up, so multi-key chords (e.g. Ctrl+A+K) register as held together.
        foreach (var key in keys)
            inputs.Add(KeyInput(key, isKeyUp: false));

        // Released in reverse order (last pressed, first released) to mirror the press order above.
        for (var i = keys.Count - 1; i >= 0; i--)
            inputs.Add(KeyInput(keys[i], isKeyUp: true));

        for (var i = modifierKeys.Count - 1; i >= 0; i--)
            inputs.Add(ClickAction.VkInput(modifierKeys[i], isKeyUp: true));

        NativeMethods.SendInputs(inputs.ToArray(), logger);
    }, ct);

    // Presses a key down without releasing it — pair with KeyUpAsync for hold sequences (e.g. Shift+Arrow selection).
    public static Task KeyDownAsync(Key key, CancellationToken ct = default) =>
        Task.Run(() => KeyDownNow(key, null), ct);

    // Synchronously sends a single key-down — split out so App.KeyDownAsync can send and mark the key held in the same synchronous unit, with no async-continuation gap between "physically down" and "tracked."
    internal static void KeyDownNow(Key key, IEngineLogger? logger = null) => NativeMethods.SendInputs([KeyInput(key, isKeyUp: false)], logger);

    // Releases a previously held key.
    public static Task KeyUpAsync(Key key, CancellationToken ct = default)
        => KeyUpCoreAsync(key, null, ct);

    // Same as KeyUpAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static Task KeyUpCoreAsync(Key key, IEngineLogger? logger, CancellationToken ct) =>
        Task.Run(() => NativeMethods.SendInputs([KeyInput(key, isKeyUp: true)], logger), ct);

    // Synchronously releases every key in keys as one batched SendInput call — for teardown paths (App.Kill/DisposeAsync) that need a best-effort release without a thread-pool hop or an async/await chain.
    internal static void ReleaseKeysNow(IReadOnlyList<Key> keys, IEngineLogger? logger = null)
    {
        if (keys.Count == 0) return;
        var inputs = new NativeMethods.INPUT[keys.Count];
        for (var i = 0; i < keys.Count; i++)
            inputs[i] = KeyInput(keys[i], isKeyUp: true);
        NativeMethods.SendInputs(inputs, logger);
    }

    // Builds a keyboard INPUT event for a named Key, applying the extended-key flag where the VK contract requires it.
    private static NativeMethods.INPUT KeyInput(Key key, bool isKeyUp) =>
        ClickAction.VkInput(key.ToVirtualKey(), isKeyUp);
}
