// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class KeyTests
{
    // A/Z, D0/D9, and F1/F12 are boundary checks on computed ranges (a formula, trusted in between); every other row is a distinct hand-written switch arm with no formula backing it, so each one is covered individually.
    [Theory]
    [InlineData(Key.A, 0x41)]
    [InlineData(Key.Z, 0x5A)]
    [InlineData(Key.D0, 0x30)]
    [InlineData(Key.D9, 0x39)]
    [InlineData(Key.F1, 0x70)]
    [InlineData(Key.F12, 0x7B)]
    [InlineData(Key.Enter, 0x0D)]
    [InlineData(Key.Escape, 0x1B)]
    [InlineData(Key.Tab, 0x09)]
    [InlineData(Key.Space, 0x20)]
    [InlineData(Key.Backspace, 0x08)]
    [InlineData(Key.Delete, 0x2E)]
    [InlineData(Key.Insert, 0x2D)]
    [InlineData(Key.Home, 0x24)]
    [InlineData(Key.End, 0x23)]
    [InlineData(Key.PageUp, 0x21)]
    [InlineData(Key.PageDown, 0x22)]
    [InlineData(Key.Left, 0x25)]
    [InlineData(Key.Up, 0x26)]
    [InlineData(Key.Right, 0x27)]
    [InlineData(Key.Down, 0x28)]
    [InlineData(Key.LeftShift, 0x10)]
    [InlineData(Key.LeftControl, 0x11)]
    [InlineData(Key.LeftAlt, 0x12)]
    [InlineData(Key.LeftWindows, 0x5B)]
    public void ToVirtualKeyCode_MapsExpectedVkCode(Key key, ushort expectedVk)
    {
        Assert.Equal(expectedVk, key.ToVirtualKeyCode());
    }

    // Delete/Left/Home/LeftWindows are E0-prefixed per the Win32 scan-code table; plain modifiers and ordinary letters/Enter are not.
    [Theory]
    [InlineData(Key.Delete, true)]
    [InlineData(Key.Left, true)]
    [InlineData(Key.Home, true)]
    [InlineData(Key.LeftWindows, true)]
    [InlineData(Key.A, false)]
    [InlineData(Key.Enter, false)]
    [InlineData(Key.LeftShift, false)]
    [InlineData(Key.LeftControl, false)]
    [InlineData(Key.LeftAlt, false)]
    public void IsExtended_MatchesWin32ExtendedKeyTable(Key key, bool expectedExtended)
    {
        Assert.Equal(expectedExtended, key.IsExtended());
    }

    [Fact]
    public void ToVirtualKey_BundlesCodeAndExtendedFlag()
    {
        Assert.Equal(new VirtualKey(0x2E, true), Key.Delete.ToVirtualKey());
        Assert.Equal(new VirtualKey(0x41, false), Key.A.ToVirtualKey());
    }

    [Fact]
    public void ToVirtualKeys_ReturnsShiftControlAltWinInStableOrder()
    {
        var modifiers = new KeyModifiers(Shift: true, Control: true, Alt: true, Win: true);

        var vks = modifiers.ToVirtualKeys().ToList();

        // Only Win (0x5B) is extended — an E0-prefixed key per the Win32 scan-code table; Shift/Control/Alt aren't.
        Assert.Equal(
            [new VirtualKey(0x10, false), new VirtualKey(0x11, false), new VirtualKey(0x12, false), new VirtualKey(0x5B, true)],
            vks);
    }

    [Fact]
    public void ToVirtualKeys_None_ReturnsEmpty()
    {
        Assert.Empty(KeyModifiers.None.ToVirtualKeys());
    }
}
