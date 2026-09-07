// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Actions;

// Physical mouse buttons ClickAction can synthesize via SendInput.
public enum MouseButton { Left, Right, Middle, Back, Forward }

// Clicks a resolved element: delegates to the provider's native click first, falling back to a synthesized SendInput click.
public static class ClickAction
{
    // Performs the click; logs via whatever logger the element was resolved with (see ElementHandle.Logger).
    public static Task ExecuteAsync(ElementHandle element, MouseButton button = MouseButton.Left, KeyModifiers modifiers = default, CancellationToken ct = default)
        => ExecuteCoreAsync(element, button, modifiers, element.Logger, ct);

    // Same as ExecuteAsync, with an explicit logger override — for the test suite to inject/inspect logging without a full resolver.
    internal static async Task ExecuteCoreAsync(ElementHandle element, MouseButton button, KeyModifiers modifiers, IEngineLogger? logger, CancellationToken ct)
    {
        if (button == MouseButton.Left && modifiers == KeyModifiers.None && element.Operator is not null)
            if (await element.Operator.TryClickAsync(element, ct).ConfigureAwait(false))
            {
                logger?.Info("Clicked via native pattern", new { elementId = element.Id });
                return;
            }

        await Task.Run(() =>
        {
            ElementInputHelpers.EnsureForeground(element);

            // modifiers, if any, are held down for the duration of the synthesized click.
            var (x, y) = ElementInputHelpers.GetCenter(element);
            SendModifiedClick(x, y, button, modifiers, logger);
        }, ct).ConfigureAwait(false);
        logger?.Info("Clicked via synthesized input", new { elementId = element.Id, button });
    }

    // Sends a leading move, then modifier-down, the button click, then modifier-up (reverse order), all as one SendInput batch.
    private static void SendModifiedClick(int x, int y, MouseButton button, KeyModifiers modifiers, IEngineLogger? logger)
    {
        var (normX, normY) = SendInputBuilders.Normalize(x, y);
        var modifierKeys = modifiers.ToVirtualKeys().ToList();
        // Leading move required — see NativeMethods.SendMouseClick's comment on WinUI3 hit-testing.
        var inputs = new List<NativeMethods.INPUT> { SendInputBuilders.MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.Move) };

        foreach (var vk in modifierKeys)
            inputs.Add(SendInputBuilders.VkInput(vk, isKeyUp: false));

        var (downFlag, upFlag, mouseData) = ButtonFlags(button);
        inputs.Add(SendInputBuilders.MouseInputAt(normX, normY, downFlag, mouseData));
        inputs.Add(SendInputBuilders.MouseInputAt(normX, normY, upFlag, mouseData));

        for (var i = modifierKeys.Count - 1; i >= 0; i--)
            inputs.Add(SendInputBuilders.VkInput(modifierKeys[i], isKeyUp: true));

        NativeMethods.SendInputs(inputs.ToArray(), logger);
    }

    // Maps a MouseButton to its SendInput down/up flags and mouseData (nonzero only for Back/Forward).
    internal static (NativeMethods.MouseEventFlags Down, NativeMethods.MouseEventFlags Up, uint MouseData) ButtonFlags(MouseButton button) => button switch
    {
        MouseButton.Left => (NativeMethods.MouseEventFlags.LeftDown, NativeMethods.MouseEventFlags.LeftUp, 0u),
        MouseButton.Right => (NativeMethods.MouseEventFlags.RightDown, NativeMethods.MouseEventFlags.RightUp, 0u),
        MouseButton.Middle => (NativeMethods.MouseEventFlags.MiddleDown, NativeMethods.MouseEventFlags.MiddleUp, 0u),
        MouseButton.Back => (NativeMethods.MouseEventFlags.XDown, NativeMethods.MouseEventFlags.XUp, NativeMethods.XButton1),
        MouseButton.Forward => (NativeMethods.MouseEventFlags.XDown, NativeMethods.MouseEventFlags.XUp, NativeMethods.XButton2),
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unhandled mouse button."),
    };
}
