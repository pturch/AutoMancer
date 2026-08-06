// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Physical mouse buttons ClickAction can synthesize via SendInput.
public enum MouseButton { Left, Right, Middle, Back, Forward }

// Clicks a resolved element: delegates to the provider's native click first, falling back to a synthesized SendInput click.
public static class ClickAction
{
    // Performs the click; tries the element's provider first (plain left click only), then falls through to synthesized mouse input.
    public static async Task ExecuteAsync(ElementHandle element, MouseButton button = MouseButton.Left, KeyModifiers modifiers = default, CancellationToken ct = default)
    {
        if (button == MouseButton.Left && modifiers == KeyModifiers.None && element.Operator is not null)
            if (await element.Operator.TryClickAsync(element, ct).ConfigureAwait(false))
                return;

        await Task.Run(() =>
        {
            EnsureForeground(element);

            // modifiers, if any, are held down for the duration of the synthesized click.
            var (x, y) = GetCenter(element);
            SendModifiedClick(x, y, button, modifiers);
        }, ct).ConfigureAwait(false);
    }

    // Brings the element's owning window to the foreground so synthesized input reaches it; no-op for non-UIA handles.
    internal static void EnsureForeground(ElementHandle element)
    {
        if (element.NativeHandle is IUIAutomationElement uiaElement && uiaElement.CurrentNativeWindowHandle != IntPtr.Zero)
            NativeMethods.SetForegroundWindow(uiaElement.CurrentNativeWindowHandle);
    }

    // Computes the absolute physical-screen point at the center of the element's bounding rect (already physical pixels; see DpiAwareness).
    internal static (int X, int Y) GetCenter(ElementHandle element)
    {
        var rect = element.BoundingRect;
        return ((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
    }

    // Sends a leading move, then modifier-down, the button click, then modifier-up (reverse order), all as one SendInput batch.
    private static void SendModifiedClick(int x, int y, MouseButton button, KeyModifiers modifiers)
    {
        var (normX, normY) = Normalize(x, y);
        var modifierKeys = modifiers.ToVirtualKeys().ToList();
        // Leading move required — see NativeMethods.SendMouseClick's comment on WinUI3 hit-testing.
        var inputs = new List<NativeMethods.INPUT> { MouseInputAt(normX, normY, NativeMethods.MouseEventMove) };

        foreach (var vk in modifierKeys)
            inputs.Add(VkInput(vk, isKeyUp: false));

        var (downFlag, upFlag, mouseData) = ButtonFlags(button);
        inputs.Add(MouseInputAt(normX, normY, downFlag, mouseData));
        inputs.Add(MouseInputAt(normX, normY, upFlag, mouseData));

        for (var i = modifierKeys.Count - 1; i >= 0; i--)
            inputs.Add(VkInput(modifierKeys[i], isKeyUp: true));

        NativeMethods.SendInputs(inputs.ToArray());
    }

    // Maps a MouseButton to its SendInput down/up flags and mouseData (nonzero only for Back/Forward).
    internal static (uint Down, uint Up, uint MouseData) ButtonFlags(MouseButton button) => button switch
    {
        MouseButton.Left => (NativeMethods.MouseEventLeftDown, NativeMethods.MouseEventLeftUp, 0u),
        MouseButton.Right => (NativeMethods.MouseEventRightDown, NativeMethods.MouseEventRightUp, 0u),
        MouseButton.Middle => (NativeMethods.MouseEventMiddleDown, NativeMethods.MouseEventMiddleUp, 0u),
        MouseButton.Back => (NativeMethods.MouseEventXDown, NativeMethods.MouseEventXUp, NativeMethods.XButton1),
        MouseButton.Forward => (NativeMethods.MouseEventXDown, NativeMethods.MouseEventXUp, NativeMethods.XButton2),
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unhandled mouse button."),
    };

    // Builds a single absolute-positioned mouse INPUT event at the given physical screen point; normalizes once.
    internal static NativeMethods.INPUT MouseInput(int x, int y, uint flags, uint mouseData = 0)
    {
        var (normX, normY) = Normalize(x, y);
        return MouseInputAt(normX, normY, flags, mouseData);
    }

    // Builds a mouse INPUT event from an already-normalized coordinate; prefer over MouseInput to avoid renormalizing per event.
    internal static NativeMethods.INPUT MouseInputAt(int normX, int normY, uint flags, uint mouseData = 0) => new()
    {
        Type = NativeMethods.InputTypeMouse,
        Data = new NativeMethods.InputUnion
        {
            Mouse = new NativeMethods.MOUSEINPUT
            {
                Dx = normX,
                Dy = normY,
                MouseData = mouseData,
                Flags = flags | NativeMethods.MouseEventAbsolute,
            },
        },
    };

    // Builds a single virtual-key keyboard INPUT event; shared with KeyboardAction, which has the same need.
    internal static NativeMethods.INPUT VkInput(VirtualKey vk, bool isKeyUp)
    {
        var flags = isKeyUp ? NativeMethods.KeyEventKeyUp : 0u;
        if (vk.Extended) flags |= NativeMethods.KeyEventExtendedKey;
        return new NativeMethods.INPUT
        {
            Type = NativeMethods.InputTypeKeyboard,
            Data = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KEYBDINPUT { Vk = vk.Code, Flags = flags },
            },
        };
    }

    // Normalizes a physical screen point to SendInput's 0-65535 absolute coordinate space (primary monitor).
    internal static (int X, int Y) Normalize(int x, int y)
    {
        var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen);
        var screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen);
        return ((int)(x * 65536L / screenWidth), (int)(y * 65536L / screenHeight));
    }
}
