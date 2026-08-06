// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Named keyboard keys for KeyboardAction — spares callers from hardcoding VK hex codes.
public enum Key
{
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Enter, Escape, Tab, Space, Backspace, Delete, Insert,
    Home, End, PageUp, PageDown, Left, Up, Right, Down,
    LeftShift, LeftControl, LeftAlt, LeftWindows,
}

// A VK code paired with whether it needs KEYEVENTF_EXTENDEDKEY, so the two can never be passed separately and get mismatched.
internal readonly record struct VirtualKey(ushort Code, bool Extended);

// Maps Key values to their Win32 VK codes.
internal static class KeyExtensions
{
    // Returns the VK code and extended-key flag SendInput needs for this key.
    internal static VirtualKey ToVirtualKey(this Key key) => new(key.ToVirtualKeyCode(), key.IsExtended());

    // Returns the VK code SendInput expects for this key.
    internal static ushort ToVirtualKeyCode(this Key key) => key switch
    {
        >= Key.A and <= Key.Z => (ushort)(NativeMethods.VirtualKeyA + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (ushort)(0x30 + (key - Key.D0)),  // VK_0..VK_9 — no NativeMethods constant needed, ASCII '0' happens to equal the VK code
        >= Key.F1 and <= Key.F12 => (ushort)(0x70 + (key - Key.F1)),  // VK_F1..VK_F12 are contiguous starting at 0x70
        Key.Enter => NativeMethods.VirtualKeyReturn,
        Key.Escape => NativeMethods.VirtualKeyEscape,
        Key.Tab => NativeMethods.VirtualKeyTab,
        Key.Space => NativeMethods.VirtualKeySpace,
        Key.Backspace => NativeMethods.VirtualKeyBackspace,
        Key.Delete => NativeMethods.VirtualKeyDelete,
        Key.Insert => NativeMethods.VirtualKeyInsert,
        Key.Home => NativeMethods.VirtualKeyHome,
        Key.End => NativeMethods.VirtualKeyEnd,
        Key.PageUp => NativeMethods.VirtualKeyPageUp,
        Key.PageDown => NativeMethods.VirtualKeyPageDown,
        Key.Left => NativeMethods.VirtualKeyLeft,
        Key.Up => NativeMethods.VirtualKeyUp,
        Key.Right => NativeMethods.VirtualKeyRight,
        Key.Down => NativeMethods.VirtualKeyDown,
        Key.LeftShift => NativeMethods.VirtualKeyShift,
        Key.LeftControl => NativeMethods.VirtualKeyControl,
        Key.LeftAlt => NativeMethods.VirtualKeyMenu,
        Key.LeftWindows => NativeMethods.VirtualKeyLWin,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unmapped key."),
    };

    // Keys whose PS/2 scan code carries the 0xE0 prefix, per the Win32 extended-key table — SendInput needs KEYEVENTF_EXTENDEDKEY.
    internal static bool IsExtended(this Key key) => key is Key.Delete or Key.Insert or Key.Home or Key.End
        or Key.PageUp or Key.PageDown or Key.Left or Key.Up or Key.Right or Key.Down or Key.LeftWindows;
}
