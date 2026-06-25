// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Types text into a resolved element: delegates to the provider's native set-value first, falling back to Unicode SendInput (layout-agnostic).
public static class TypeAction
{
    // Types the given text into the element; returns once all input is delivered.
    public static async Task ExecuteAsync(ElementHandle element, string text, CancellationToken ct = default)
    {
        if (element.Provider is not null)
            if (await element.Provider.TrySetValueAsync(element, text, ct).ConfigureAwait(false))
                return;

        await Task.Run(() =>
        {
            if (element.NativeHandle is IUIAutomationElement uiaElement && uiaElement.CurrentNativeWindowHandle != IntPtr.Zero)
                NativeMethods.SetForegroundWindow(uiaElement.CurrentNativeWindowHandle);

            SendUnicodeText(text);
        }, ct).ConfigureAwait(false);
    }

    // Sends each UTF-16 code unit as a Unicode keyboard event pair; KEYEVENTF_UNICODE with wScan=codeunit is layout-agnostic.
    private static void SendUnicodeText(string text)
    {
        var inputs = new NativeMethods.INPUT[text.Length * 2];
        for (var i = 0; i < text.Length; i++)
        {
            inputs[i * 2] = UnicodeKeyInput((ushort)text[i], isKeyUp: false);
            inputs[i * 2 + 1] = UnicodeKeyInput((ushort)text[i], isKeyUp: true);
        }

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    // Builds a single KEYBDINPUT event; Vk=0, Scan=codeUnit per the KEYEVENTF_UNICODE contract.
    private static NativeMethods.INPUT UnicodeKeyInput(ushort codeUnit, bool isKeyUp)
    {
        var flags = NativeMethods.KeyEventUnicode;
        if (isKeyUp) flags |= NativeMethods.KeyEventKeyUp;

        return new NativeMethods.INPUT
        {
            Type = NativeMethods.InputTypeKeyboard,
            Data = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KEYBDINPUT
                {
                    Vk = 0,
                    Scan = codeUnit,
                    Flags = flags,
                },
            },
        };
    }
}
