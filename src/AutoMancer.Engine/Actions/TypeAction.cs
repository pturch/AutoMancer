// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Types text into a resolved element: delegates to the provider's native set-value first (replaces the whole value, not just a selection), falling back to Unicode SendInput (layout-agnostic, respects caret/selection like real keystrokes).
public static class TypeAction
{
    // Types the given text into the element; logs via whatever logger the element was resolved with (see ElementHandle.Logger).
    public static Task ExecuteAsync(ElementHandle element, string text, CancellationToken ct = default)
        => ExecuteCoreAsync(element, text, element.Logger, ct);

    // Same as ExecuteAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static async Task ExecuteCoreAsync(ElementHandle element, string text, IEngineLogger? logger, CancellationToken ct)
    {
        // ValuePattern.SetValue always replaces the whole value — it has no concept of caret position or an active selection.
        if (element.Operator is not null)
            if (await element.Operator.TrySetValueAsync(element, text, ct).ConfigureAwait(false))
            {
                logger?.Info("Typed via native pattern", new { elementId = element.Id, textLength = text.Length });
                return;
            }

        await Task.Run(() =>
        {
            ClickAction.EnsureForeground(element);
            ClickAction.EnsureInteractable(element);
            SendUnicodeText(text, logger);
        }, ct).ConfigureAwait(false);
        logger?.Info("Typed via synthesized input", new { elementId = element.Id, textLength = text.Length });
    }

    // Sends each UTF-16 code unit as a Unicode keyboard event pair; KEYEVENTF_UNICODE with wScan=codeunit is layout-agnostic.
    internal static void SendUnicodeText(string text, IEngineLogger? logger = null)
    {
        var inputs = new NativeMethods.INPUT[text.Length * 2];
        for (var i = 0; i < text.Length; i++)
        {
            inputs[i * 2] = UnicodeKeyInput((ushort)text[i], isKeyUp: false);
            inputs[i * 2 + 1] = UnicodeKeyInput((ushort)text[i], isKeyUp: true);
        }

        NativeMethods.SendInputs(inputs, logger);
    }

    // Builds a single KEYBDINPUT event; Vk=0, Scan=codeUnit per the KEYEVENTF_UNICODE contract.
    private static NativeMethods.INPUT UnicodeKeyInput(ushort codeUnit, bool isKeyUp)
    {
        var flags = NativeMethods.KeyEventFlags.Unicode;
        if (isKeyUp) flags |= NativeMethods.KeyEventFlags.KeyUp;

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
