// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Builds SendInput mouse/keyboard INPUT structs and normalizes screen coordinates; shared by every action that synthesizes input.
internal static class SendInputBuilders
{
    // Normalizes a physical screen point to SendInput's 0-65535 absolute coordinate space (primary monitor).
    internal static (int X, int Y) Normalize(int x, int y)
    {
        var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen);
        var screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen);
        return ((int)(x * 65536L / screenWidth), (int)(y * 65536L / screenHeight));
    }

    // Builds a single absolute-positioned mouse INPUT event at the given physical screen point; normalizes once.
    internal static NativeMethods.INPUT MouseInput(int x, int y, NativeMethods.MouseEventFlags flags, uint mouseData = 0)
    {
        var (normX, normY) = Normalize(x, y);
        return MouseInputAt(normX, normY, flags, mouseData);
    }

    // Builds a mouse INPUT event from an already-normalized coordinate; prefer over MouseInput to avoid renormalizing per event.
    internal static NativeMethods.INPUT MouseInputAt(int normX, int normY, NativeMethods.MouseEventFlags flags, uint mouseData = 0) => new()
    {
        Type = NativeMethods.InputTypeMouse,
        Data = new NativeMethods.InputUnion
        {
            Mouse = new NativeMethods.MOUSEINPUT
            {
                Dx = normX,
                Dy = normY,
                MouseData = mouseData,
                Flags = flags | NativeMethods.MouseEventFlags.Absolute,
            },
        },
    };

    // Builds a single virtual-key keyboard INPUT event.
    internal static NativeMethods.INPUT VkInput(VirtualKey vk, bool isKeyUp)
    {
        var flags = isKeyUp ? NativeMethods.KeyEventFlags.KeyUp : 0;
        if (vk.Extended) flags |= NativeMethods.KeyEventFlags.ExtendedKey;
        return new NativeMethods.INPUT
        {
            Type = NativeMethods.InputTypeKeyboard,
            Data = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KEYBDINPUT { Vk = vk.Code, Flags = flags },
            },
        };
    }
}
