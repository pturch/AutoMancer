// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Which mouse button(s)/sequence ClickAction synthesizes.
public enum ClickType { Left, Right, Double }

// Clicks a resolved element: tries UIA's InvokePattern first (no mouse movement), falling back to a synthesized
// SendInput click at the element's center. Bounding rects are already true physical pixels (see DpiAwareness).
public static class ClickAction
{
    // Performs the click; never throws on a missing pattern — falls straight through to the SendInput fallback.
    public static Task ExecuteAsync(ElementHandle element, ClickType clickType = ClickType.Left, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (clickType == ClickType.Left && TryInvoke(element))
                return;

            if (element.NativeHandle is IUIAutomationElement uiaElement && uiaElement.CurrentNativeWindowHandle != IntPtr.Zero)
                NativeMethods.SetForegroundWindow(uiaElement.CurrentNativeWindowHandle);

            var (x, y) = GetCenter(element);
            SendClick(x, y, clickType);
        }, ct);
    }

    // Invokes the element via UIA's InvokePattern if it exposes one; returns false when it doesn't.
    private static bool TryInvoke(ElementHandle element)
    {
        if (element.NativeHandle is not IUIAutomationElement uiaElement)
            return false;

        if (uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) is not IUIAutomationInvokePattern invoke)
            return false;

        invoke.Invoke();
        return true;
    }

    // Computes the absolute physical-screen point at the center of the element's bounding rect.
    private static (int X, int Y) GetCenter(ElementHandle element)
    {
        var rect = element.BoundingRect;
        return ((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
    }

    // Synthesizes a click at the given physical screen point via SendInput; Double sends two down/up pairs.
    private static void SendClick(int x, int y, ClickType clickType)
    {
        var (downFlag, upFlag) = clickType == ClickType.Right
            ? (NativeMethods.MouseEventRightDown, NativeMethods.MouseEventRightUp)
            : (NativeMethods.MouseEventLeftDown, NativeMethods.MouseEventLeftUp);

        var clicks = clickType == ClickType.Double ? 2 : 1;
        var inputs = new NativeMethods.INPUT[clicks * 2];
        for (var i = 0; i < clicks; i++)
        {
            inputs[i * 2] = MouseInput(x, y, downFlag);
            inputs[i * 2 + 1] = MouseInput(x, y, upFlag);
        }

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    // Builds a single absolute-positioned mouse INPUT event at the given physical screen point.
    private static NativeMethods.INPUT MouseInput(int x, int y, uint flags)
    {
        var (normX, normY) = Normalize(x, y);
        return new NativeMethods.INPUT
        {
            Type = NativeMethods.InputTypeMouse,
            Data = new NativeMethods.InputUnion
            {
                Mouse = new NativeMethods.MOUSEINPUT
                {
                    Dx = normX,
                    Dy = normY,
                    Flags = flags | NativeMethods.MouseEventAbsolute,
                },
            },
        };
    }

    // Normalizes a physical screen point to SendInput's 0-65535 absolute coordinate space (primary monitor).
    private static (int X, int Y) Normalize(int x, int y)
    {
        var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen);
        var screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen);
        return ((int)(x * 65536L / screenWidth), (int)(y * 65536L / screenHeight));
    }
}
