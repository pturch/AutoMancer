// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using System.Text;

namespace AutoMancer.Engine.Providers;

// Centralizes every Win32 P/Invoke declaration used by providers and actions.
// These magic numbers are defined by the Win32 ABI contract
internal static class NativeMethods
{
    // Callback invoked by EnumChildWindows for each child window; return false to stop enumeration.
    internal delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    // Enumerates the immediate and nested child windows of hWndParent, invoking lpEnumFunc for each.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    // Reads the window title text of hWnd into lpString.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    // Reads the window class name of hWnd into lpClassName.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    // Reports whether hWnd is currently visible.
    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    // Brings hWnd to the foreground so synthesized input is delivered to it.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    // Returns the DPI associated with hWnd's monitor (Windows 10 1607+); 96 means 100% scaling.
    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(IntPtr hWnd);

    // Reads hWnd's bounding rectangle in physical screen coordinates.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // Reads a system display metric (e.g. primary screen width/height in physical pixels).
    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    internal const int SmCxScreen = 0;
    internal const int SmCyScreen = 1;

    // Declares the calling process's DPI awareness; used once at startup so coordinate APIs report true physical pixels.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    internal static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    // Synthesizes mouse and keyboard input events via the system input queue.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT Mouse;
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
        [FieldOffset(0)] public HARDWAREINPUT Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
    }

    internal const uint InputTypeMouse = 0;
    internal const uint InputTypeKeyboard = 1;
    internal const uint InputTypeHardware = 2;

    internal const uint MouseEventMove = 0x0001;
    internal const uint MouseEventLeftDown = 0x0002;
    internal const uint MouseEventLeftUp = 0x0004;
    internal const uint MouseEventRightDown = 0x0008;
    internal const uint MouseEventRightUp = 0x0010;
    internal const uint MouseEventAbsolute = 0x8000;

    internal const uint KeyEventExtendedKey = 0x0001;
    internal const uint KeyEventKeyUp = 0x0002;
    internal const uint KeyEventUnicode = 0x0004;
    internal const uint KeyEventScancode = 0x0008;

    internal const ushort VirtualKeyReturn  = 0x0D;
    internal const ushort VirtualKeyMenu    = 0x12;  // Alt
    internal const ushort VirtualKeyControl = 0x11;
    internal const ushort VirtualKeyA       = 0x41;
    internal const ushort VirtualKeyDelete  = 0x2E;

    // Sends a left-button click (move, down, up) at a physical screen coordinate.
    internal static void SendMouseClick(int x, int y)
    {
        var w  = GetSystemMetrics(SmCxScreen);
        var h  = GetSystemMetrics(SmCyScreen);
        var nx = (int)(x * 65536L / w);
        var ny = (int)(y * 65536L / h);
        INPUT[] inputs =
        [
            new() { Type = InputTypeMouse, Data = new InputUnion { Mouse = new MOUSEINPUT { Dx = nx, Dy = ny, Flags = MouseEventMove | MouseEventAbsolute } } },
            new() { Type = InputTypeMouse, Data = new InputUnion { Mouse = new MOUSEINPUT { Dx = nx, Dy = ny, Flags = MouseEventLeftDown | MouseEventAbsolute } } },
            new() { Type = InputTypeMouse, Data = new InputUnion { Mouse = new MOUSEINPUT { Dx = nx, Dy = ny, Flags = MouseEventLeftUp | MouseEventAbsolute } } },
        ];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    // Sends a virtual-key keydown followed by keyup to the current foreground window.
    internal static void SendVkKey(ushort vk)
    {
        INPUT[] inputs =
        [
            new() { Type = InputTypeKeyboard, Data = new InputUnion { Keyboard = new KEYBDINPUT { Vk = vk } } },
            new() { Type = InputTypeKeyboard, Data = new InputUnion { Keyboard = new KEYBDINPUT { Vk = vk, Flags = KeyEventKeyUp } } },
        ];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    // Sends a chord: modifier down, key down+up, modifier up (e.g. Alt+D or Ctrl+A).
    internal static void SendVkChord(ushort modifier, ushort key)
    {
        INPUT[] inputs =
        [
            new() { Type = InputTypeKeyboard, Data = new InputUnion { Keyboard = new KEYBDINPUT { Vk = modifier } } },
            new() { Type = InputTypeKeyboard, Data = new InputUnion { Keyboard = new KEYBDINPUT { Vk = key } } },
            new() { Type = InputTypeKeyboard, Data = new InputUnion { Keyboard = new KEYBDINPUT { Vk = key, Flags = KeyEventKeyUp } } },
            new() { Type = InputTypeKeyboard, Data = new InputUnion { Keyboard = new KEYBDINPUT { Vk = modifier, Flags = KeyEventKeyUp } } },
        ];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    // Callback invoked by EnumWindows for each top-level window; return false to stop enumeration.
    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // Enumerates all top-level windows on the desktop, invoking lpEnumFunc for each.
    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    // Returns the PID of the process that created hWnd.
    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // Moves, resizes, or repositions hWnd without changing its Z-order or foreground state.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // Changes the show state of hWnd (minimize, maximize, restore, etc.).
    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    internal const uint SwpNoSize     = 0x0001;
    internal const uint SwpNoMove     = 0x0002;
    internal const uint SwpNoZOrder   = 0x0004;
    internal const uint SwpNoActivate = 0x0010;

    internal const int SwRestore  = 9;
    internal const int SwMinimize = 2;
    internal const int SwMaximize = 3;
}
