// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Clears a resolved element's value: tries ValuePattern.SetValue("") first, falling back to Ctrl+A + Delete.
public static class ClearAction
{
    // Clears the element's content; tries the operator's set-value path first, then synthesized keyboard input.
    public static async Task ExecuteAsync(ElementHandle element, CancellationToken ct = default)
    {
        if (element.Operator is not null)
            if (await element.Operator.TrySetValueAsync(element, "", ct).ConfigureAwait(false))
                return;

        await Task.Run(() =>
        {
            if (element.NativeHandle is IUIAutomationElement uiaElement && uiaElement.CurrentNativeWindowHandle != IntPtr.Zero)
                NativeMethods.SetForegroundWindow(uiaElement.CurrentNativeWindowHandle);

            SendCtrlADelete();
        }, ct).ConfigureAwait(false);
    }

    // Sends Ctrl+A to select all, then Delete to erase the selection.
    private static void SendCtrlADelete()
    {
        var inputs = new NativeMethods.INPUT[6];
        inputs[0] = VkInput(NativeMethods.VirtualKeyControl, isKeyUp: false, extended: false);
        inputs[1] = VkInput(NativeMethods.VirtualKeyA, isKeyUp: false, extended: false);
        inputs[2] = VkInput(NativeMethods.VirtualKeyA, isKeyUp: true, extended: false);
        inputs[3] = VkInput(NativeMethods.VirtualKeyControl, isKeyUp: true, extended: false);
        inputs[4] = VkInput(NativeMethods.VirtualKeyDelete, isKeyUp: false, extended: true);
        inputs[5] = VkInput(NativeMethods.VirtualKeyDelete, isKeyUp: true, extended: true);
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    // Builds a virtual-key keyboard INPUT event; extended=true is required for the non-numpad Delete key.
    private static NativeMethods.INPUT VkInput(ushort vk, bool isKeyUp, bool extended)
    {
        var flags = isKeyUp ? NativeMethods.KeyEventKeyUp : 0u;
        if (extended) flags |= NativeMethods.KeyEventExtendedKey;
        return new NativeMethods.INPUT
        {
            Type = NativeMethods.InputTypeKeyboard,
            Data = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KEYBDINPUT
                {
                    Vk = vk,
                    Scan = 0,
                    Flags = flags,
                },
            },
        };
    }
}
