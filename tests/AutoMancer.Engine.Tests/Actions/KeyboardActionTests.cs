// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class KeyboardActionTests
{
    // App.Kill/DisposeAsync call this unconditionally even when no key is held; it must short-circuit
    // before building an INPUT array or touching SendInput, or teardown would synthesize a spurious keystroke.
    [Fact]
    public void ReleaseKeysNow_NoHeldKeys_DoesNotThrow()
        => KeyboardAction.ReleaseKeysNow([]);

    // KeyboardAction's private KeyInput delegates straight to this for every event it builds, so its KEYEVENTF_KEYUP/
    // KEYEVENTF_EXTENDEDKEY flag combination is what every real keystroke actually carries — worth pinning directly
    // since it's otherwise only exercised indirectly (via real SendInput) by the Integration-tagged Notepad tests.
    [Theory]
    [InlineData(false, false, 0u)]
    [InlineData(true, false, (uint)NativeMethods.KeyEventFlags.KeyUp)]
    [InlineData(false, true, (uint)NativeMethods.KeyEventFlags.ExtendedKey)]
    [InlineData(true, true, (uint)(NativeMethods.KeyEventFlags.ExtendedKey | NativeMethods.KeyEventFlags.KeyUp))]
    public void VkInput_CombinesKeyUpAndExtendedFlags(bool isKeyUp, bool extended, uint expectedFlags)
    {
        var input = SendInputBuilders.VkInput(new VirtualKey(0x41, extended), isKeyUp);

        Assert.Equal(NativeMethods.InputTypeKeyboard, input.Type);
        Assert.Equal((ushort)0x41, input.Data.Keyboard.Vk);
        Assert.Equal((NativeMethods.KeyEventFlags)expectedFlags, input.Data.Keyboard.Flags);
    }
}
