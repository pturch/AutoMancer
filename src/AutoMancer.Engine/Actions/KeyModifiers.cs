// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Modifier keys optionally held down for the duration of a click or hotkey.
public readonly record struct KeyModifiers(bool Shift = false, bool Control = false, bool Alt = false, bool Win = false)
{
    // No modifiers held — the default for click/hotkey calls that don't need any.
    public static readonly KeyModifiers None = new();
}

// Maps KeyModifiers flags to their VK codes for building SendInput keyboard events.
internal static class KeyModifiersExtensions
{
    // Returns the VK code and extended-key flag for each set modifier, in a stable Shift/Control/Alt/Win order.
    internal static IEnumerable<VirtualKey> ToVirtualKeys(this KeyModifiers modifiers)
    {
        if (modifiers.Shift) yield return new VirtualKey(NativeMethods.VirtualKeyShift, false);
        if (modifiers.Control) yield return new VirtualKey(NativeMethods.VirtualKeyControl, false);
        if (modifiers.Alt) yield return new VirtualKey(NativeMethods.VirtualKeyMenu, false);
        // Win is the only modifier that's E0-prefixed (see Key.IsExtended) and needs KEYEVENTF_EXTENDEDKEY.
        if (modifiers.Win) yield return new VirtualKey(NativeMethods.VirtualKeyLWin, true);
    }
}
