// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Clears a resolved element's value: tries ValuePattern.SetValue("") first, falling back to Ctrl+A + Delete.
public static class ClearAction
{
    // Clears the element's content; logs via whatever logger the element was resolved with (see ElementHandle.Logger).
    public static Task ExecuteAsync(ElementHandle element, CancellationToken ct = default)
        => ExecuteCoreAsync(element, element.Logger, ct);

    // Same as ExecuteAsync, with an explicit logger override — for App and the test suite to inject/inspect logging directly.
    internal static async Task ExecuteCoreAsync(ElementHandle element, IEngineLogger? logger, CancellationToken ct)
    {
        if (element.Operator is not null)
            if (await element.Operator.TrySetValueAsync(element, "", ct).ConfigureAwait(false))
            {
                logger?.Info("Cleared via native pattern", new { elementId = element.Id });
                return;
            }

        await Task.Run(() =>
        {
            ClickAction.EnsureForeground(element);
            SendCtrlADelete(logger);
        }, ct).ConfigureAwait(false);
        logger?.Info("Cleared via synthesized input", new { elementId = element.Id });
    }

    // Sends Ctrl+A to select all, then Delete to erase the selection.
    private static void SendCtrlADelete(IEngineLogger? logger)
    {
        var inputs = new NativeMethods.INPUT[6];
        inputs[0] = VkInput(NativeMethods.VirtualKeyControl, isKeyUp: false, extended: false);
        inputs[1] = VkInput(NativeMethods.VirtualKeyA, isKeyUp: false, extended: false);
        inputs[2] = VkInput(NativeMethods.VirtualKeyA, isKeyUp: true, extended: false);
        inputs[3] = VkInput(NativeMethods.VirtualKeyControl, isKeyUp: true, extended: false);
        inputs[4] = VkInput(NativeMethods.VirtualKeyDelete, isKeyUp: false, extended: true);
        inputs[5] = VkInput(NativeMethods.VirtualKeyDelete, isKeyUp: true, extended: true);
        NativeMethods.SendInputs(inputs, logger);
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
