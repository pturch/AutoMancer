// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using System.Text;

namespace AutoMancer.Engine.Providers;

// Centralizes every Win32 P/Invoke declaration used by providers and actions; magic numbers here are the Win32 ABI contract.
internal static class NativeMethods
{
    // Callback invoked by EnumChildWindows for each child window; return false to stop enumeration.
    internal delegate bool EnumChildProc(IntPtr windowHandle, IntPtr lParam);

    // Enumerates the immediate and nested child windows of parentWindowHandle, invoking lpEnumFunc for each.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool EnumChildWindows(IntPtr parentWindowHandle, EnumChildProc lpEnumFunc, IntPtr lParam);

    // Reads the window title text of windowHandle into lpString.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr windowHandle, StringBuilder lpString, int nMaxCount);

    // Reads the window class name of windowHandle into lpClassName.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr windowHandle, StringBuilder lpClassName, int nMaxCount);

    // Reports whether windowHandle is currently visible.
    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr windowHandle);

    // Reports whether windowHandle is currently minimized.
    [DllImport("user32.dll")]
    internal static extern bool IsIconic(IntPtr windowHandle);

    // Reports whether windowHandle is currently maximized.
    [DllImport("user32.dll")]
    internal static extern bool IsZoomed(IntPtr windowHandle);

    // Brings windowHandle to the foreground so synthesized input is delivered to it.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetForegroundWindow(IntPtr windowHandle);

    // Returns the DPI associated with windowHandle's monitor (Windows 10 1607+); 96 means 100% scaling.
    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(IntPtr windowHandle);

    // Reads windowHandle's bounding rectangle in physical screen coordinates.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetWindowRect(IntPtr windowHandle, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // Reads the current physical-screen position of the mouse cursor.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
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

    // Submits one or more INPUT events to SendInput as a single batch; every action funnels through here instead of computing cbSize itself.
    internal static void SendInputs(params INPUT[] inputs) => SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());

    // Reports whether vKey is currently down (high bit of the return value) — global OS state, independent of which window has focus.
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

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
    internal const uint MouseEventMiddleDown = 0x0020;
    internal const uint MouseEventMiddleUp = 0x0040;
    internal const uint MouseEventXDown = 0x0080;
    internal const uint MouseEventXUp = 0x0100;
    internal const uint MouseEventWheel = 0x0800;
    internal const uint MouseEventHWheel = 0x01000;
    internal const uint MouseEventAbsolute = 0x8000;

    // mouseData values for MouseEventXDown/XUp — identifies which X button (Back/Forward).
    internal const uint XButton1 = 0x0001;
    internal const uint XButton2 = 0x0002;

    // One notch of a standard mouse wheel, per the Win32 WHEEL_DELTA contract.
    internal const int WheelDelta = 120;

    internal const uint KeyEventExtendedKey = 0x0001;
    internal const uint KeyEventKeyUp = 0x0002;
    internal const uint KeyEventUnicode = 0x0004;
    internal const uint KeyEventScancode = 0x0008;

    internal const ushort VirtualKeyBackspace = 0x08;
    internal const ushort VirtualKeyTab       = 0x09;
    internal const ushort VirtualKeyReturn    = 0x0D;
    internal const ushort VirtualKeyShift     = 0x10;
    internal const ushort VirtualKeyControl   = 0x11;
    internal const ushort VirtualKeyMenu      = 0x12;  // Alt
    internal const ushort VirtualKeyEscape    = 0x1B;
    internal const ushort VirtualKeySpace     = 0x20;
    internal const ushort VirtualKeyPageUp    = 0x21;  // Win32 name: VK_PRIOR
    internal const ushort VirtualKeyPageDown  = 0x22;  // Win32 name: VK_NEXT
    internal const ushort VirtualKeyEnd       = 0x23;
    internal const ushort VirtualKeyHome      = 0x24;
    internal const ushort VirtualKeyLeft      = 0x25;
    internal const ushort VirtualKeyUp        = 0x26;
    internal const ushort VirtualKeyRight     = 0x27;
    internal const ushort VirtualKeyDown      = 0x28;
    internal const ushort VirtualKeyInsert    = 0x2D;
    internal const ushort VirtualKeyDelete    = 0x2E;
    internal const ushort VirtualKeyA         = 0x41;
    internal const ushort VirtualKeyLWin      = 0x5B;

    // Sends a left-button click at a physical screen coordinate.
    internal static void SendMouseClick(int x, int y)
    {
        var w  = GetSystemMetrics(SmCxScreen);
        var h  = GetSystemMetrics(SmCyScreen);
        var nx = (int)(x * 65536L / w);
        var ny = (int)(y * 65536L / h);
        INPUT[] inputs =
        [
            // MOVE first — WinUI3 hit-testing needs the pointer over the target before LEFTDOWN, or the click is swallowed.
            new() { Type = InputTypeMouse, Data = new InputUnion { Mouse = new MOUSEINPUT { Dx = nx, Dy = ny, Flags = MouseEventMove     | MouseEventAbsolute } } },
            new() { Type = InputTypeMouse, Data = new InputUnion { Mouse = new MOUSEINPUT { Dx = nx, Dy = ny, Flags = MouseEventLeftDown | MouseEventAbsolute } } },
            new() { Type = InputTypeMouse, Data = new InputUnion { Mouse = new MOUSEINPUT { Dx = nx, Dy = ny, Flags = MouseEventLeftUp   | MouseEventAbsolute } } },
        ];
        SendInputs(inputs);
    }

    // Callback invoked by EnumWindows for each top-level window; return false to stop enumeration.
    internal delegate bool EnumWindowsProc(IntPtr windowHandle, IntPtr lParam);

    // Enumerates all top-level windows on the desktop, invoking lpEnumFunc for each.
    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    // Returns the PID of the process that created windowHandle.
    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint lpdwProcessId);

    // Moves, resizes, or repositions windowHandle without changing its Z-order or foreground state.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr windowHandle, IntPtr insertAfterWindowHandle, int X, int Y, int cx, int cy, uint uFlags);

    // Changes the show state of windowHandle (minimize, maximize, restore, etc.).
    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr windowHandle, int nCmdShow);

    // Posts a message to windowHandle's message queue without waiting for it to be processed; used for WM_CLOSE.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool PostMessage(IntPtr windowHandle, uint msg, IntPtr wParam, IntPtr lParam);

    internal const uint WmClose = 0x0010;

    internal const uint SwpNoSize     = 0x0001;
    internal const uint SwpNoMove     = 0x0002;
    internal const uint SwpNoZOrder   = 0x0004;
    internal const uint SwpNoActivate = 0x0010;

    internal const int SwRestore  = 9;
    internal const int SwMinimize = 2;
    internal const int SwMaximize = 3;

    // Returns a device context for windowHandle (or the whole screen when windowHandle is IntPtr.Zero); pair with ReleaseDeviceContext.
    [DllImport("user32.dll", EntryPoint = "GetDC")]
    internal static extern IntPtr GetDeviceContext(IntPtr windowHandle);

    // Releases a device context obtained from GetDeviceContext.
    [DllImport("user32.dll", EntryPoint = "ReleaseDC")]
    internal static extern int ReleaseDeviceContext(IntPtr windowHandle, IntPtr deviceContext);

    // Creates an in-memory device context compatible with deviceContext; pair with DeleteDeviceContext.
    [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")]
    internal static extern IntPtr CreateCompatibleDeviceContext(IntPtr deviceContext);

    // Creates a bitmap compatible with deviceContext, sized (width x height); pair with DeleteObject.
    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateCompatibleBitmap(IntPtr deviceContext, int width, int height);

    // Selects a GDI object (bitmap, pen, brush) into deviceContext, returning the previously selected object.
    [DllImport("gdi32.dll")]
    internal static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr gdiObject);

    // Copies a block of pixels from sourceDeviceContext to destDeviceContext — the core of a GDI screenshot.
    [DllImport("gdi32.dll", EntryPoint = "BitBlt", SetLastError = true)]
    internal static extern bool BitBlockTransfer(IntPtr destDeviceContext, int destX, int destY, int width, int height, IntPtr sourceDeviceContext, int sourceX, int sourceY, uint rasterOperation);

    // Deletes a device context created by CreateCompatibleDeviceContext.
    [DllImport("gdi32.dll", EntryPoint = "DeleteDC")]
    internal static extern bool DeleteDeviceContext(IntPtr deviceContext);

    // Deletes a GDI object (bitmap, pen, brush) created by CreateCompatible*.
    [DllImport("gdi32.dll")]
    internal static extern bool DeleteObject(IntPtr hObject);

    // BitBlt raster-operation code for a direct copy (no blending), per the Win32 SRCCOPY contract.
    internal const uint SrcCopy = 0x00CC0020;
}
