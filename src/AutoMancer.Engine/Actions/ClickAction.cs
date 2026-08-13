// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

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
            EnsureForeground(element);

            // modifiers, if any, are held down for the duration of the synthesized click.
            var (x, y) = GetCenter(element);
            SendModifiedClick(x, y, button, modifiers, logger);
        }, ct).ConfigureAwait(false);
        logger?.Info("Clicked via synthesized input", new { elementId = element.Id, button });
    }

    // Brings the element's owning window to the foreground so synthesized input reaches it; no-op for non-UIA handles. Logs (doesn't throw) if the OS declines — SetForegroundWindow's failure signal is known to be unreliable, so this isn't treated as conclusive.
    internal static void EnsureForeground(ElementHandle element)
    {
        if (element.NativeHandle is IUIAutomationElement uiaElement && uiaElement.CurrentNativeWindowHandle != IntPtr.Zero)
            if (!NativeMethods.SetForegroundWindow(uiaElement.CurrentNativeWindowHandle))
                element.Logger?.Warn("SetForegroundWindow declined");
    }

    // Throws ElementNotInteractableError when IsEnabled/IsOffscreen say a synthesized action can't land meaningfully; shared by every action that falls back to SendInput (click, type, clear), not just click.
    internal static void EnsureInteractable(ElementHandle element)
    {
        if (element.IsEnabled == false)
            throw new ElementNotInteractableError(element, $"Element \"{element.Name}\" (AutomationId={element.AutomationId}) is disabled (IsEnabled=false) and cannot be interacted with.");
        if (element.IsOffscreen == true)
            throw new ElementNotInteractableError(element, $"Element \"{element.Name}\" (AutomationId={element.AutomationId}) is currently offscreen (IsOffscreen=true) and cannot be interacted with.");
    }

    // Computes the absolute physical-screen point at the center of the element's bounding rect (already physical pixels; see DpiAwareness)
    internal static (int X, int Y) GetCenter(ElementHandle element)
    {
        EnsureInteractable(element);
        var rect = element.BoundingRect;
        return ((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
    }

    // Sends a leading move, then modifier-down, the button click, then modifier-up (reverse order), all as one SendInput batch.
    private static void SendModifiedClick(int x, int y, MouseButton button, KeyModifiers modifiers, IEngineLogger? logger)
    {
        var (normX, normY) = Normalize(x, y);
        var modifierKeys = modifiers.ToVirtualKeys().ToList();
        // Leading move required — see NativeMethods.SendMouseClick's comment on WinUI3 hit-testing.
        var inputs = new List<NativeMethods.INPUT> { MouseInputAt(normX, normY, NativeMethods.MouseEventFlags.Move) };

        foreach (var vk in modifierKeys)
            inputs.Add(VkInput(vk, isKeyUp: false));

        var (downFlag, upFlag, mouseData) = ButtonFlags(button);
        inputs.Add(MouseInputAt(normX, normY, downFlag, mouseData));
        inputs.Add(MouseInputAt(normX, normY, upFlag, mouseData));

        for (var i = modifierKeys.Count - 1; i >= 0; i--)
            inputs.Add(VkInput(modifierKeys[i], isKeyUp: true));

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

    // Builds a single virtual-key keyboard INPUT event; shared with KeyboardAction, which has the same need.
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

    // Normalizes a physical screen point to SendInput's 0-65535 absolute coordinate space (primary monitor).
    internal static (int X, int Y) Normalize(int x, int y)
    {
        var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen);
        var screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen);
        return ((int)(x * 65536L / screenWidth), (int)(y * 65536L / screenHeight));
    }
}
