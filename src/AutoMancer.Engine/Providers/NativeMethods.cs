// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;

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

    // Reports whether windowHandle currently accepts mouse/keyboard input (not disabled).
    [DllImport("user32.dll")]
    internal static extern bool IsWindowEnabled(IntPtr windowHandle);

    // Reports whether windowHandle is currently minimized.
    [DllImport("user32.dll")]
    internal static extern bool IsIconic(IntPtr windowHandle);

    // Reports whether windowHandle is currently maximized.
    [DllImport("user32.dll")]
    internal static extern bool IsZoomed(IntPtr windowHandle);

    // Brings windowHandle to the foreground so synthesized input is delivered to it; return value is documented as unreliable, so callers should verify via GetForegroundWindow rather than trust it.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetForegroundWindow(IntPtr windowHandle);

    // Returns the handle of whichever window currently actually has focus — the ground truth SetForegroundWindow's own return value can't be trusted to reflect.
    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    // Walks up from windowHandle to its top-level owning window; GetForegroundWindow only ever reports top-level windows, so a child HWND must be resolved to this before comparing against it.
    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr windowHandle, uint gaFlags);

    // GetAncestor's GA_ROOT flag: walk parent windows only (not owners) and return the top-level result.
    private const uint GaRoot = 2;

    // Resolves windowHandle to its top-level ancestor (itself, if GetAncestor can't resolve one) — exposed publicly via App.GetTopLevelWindow.
    internal static IntPtr GetTopLevelWindow(IntPtr windowHandle)
    {
        var ancestor = GetAncestor(windowHandle, GaRoot);
        return ancestor != IntPtr.Zero ? ancestor : windowHandle;
    }

    // Foregrounds windowHandle, retrying against GetForegroundWindow until it lands or timeoutMs elapses; throws WindowActivationError if it never lands.
    // Compares exactly the handle it's given, with no ancestor resolution — callers skip calling this altogether (via a 0 ForegroundActivationTimeoutMs) for a window whose hierarchy defeats a direct comparison.
    internal static void EnsureForegroundOrThrow(IntPtr windowHandle, int timeoutMs = 3_000, int pollIntervalMs = 100)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (true)
        {
            SetForegroundWindow(windowHandle);
            if (GetForegroundWindow() == windowHandle)
                return;
            if (Environment.TickCount64 >= deadline)
                throw new WindowActivationError(windowHandle);
            Thread.Sleep(pollIntervalMs);
        }
    }

    // Reads a window's title via GetWindowText; shared by every title-matching call site.
    internal static string GetWindowTitle(IntPtr windowHandle)
    {
        var sb = new StringBuilder(512);
        GetWindowText(windowHandle, sb, sb.Capacity);
        return sb.ToString();
    }

    // Reads a window's class name via GetClassName; shared by every class-matching call site.
    internal static string GetWindowClassName(IntPtr windowHandle)
    {
        var sb = new StringBuilder(256);
        GetClassName(windowHandle, sb, sb.Capacity);
        return sb.ToString();
    }

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

    // Submits one or more INPUT events to SendInput as a single batch; throws InputDeliveryError if the OS delivers fewer than requested (e.g. blocked by UIPI).
    internal static void SendInputs(INPUT[] inputs, IEngineLogger? logger = null)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            logger?.Warn("SendInput delivered fewer events than requested", new { requested = inputs.Length, sent });
            throw new InputDeliveryError(inputs.Length, (int)sent);
        }
    }

    // Throws Win32CallError naming operation and the current GetLastWin32Error() code when a bool-returning P/Invoke call reports failure.
    internal static void ThrowIfFailed(this bool success, string operation)
    {
        if (!success)
            throw new Win32CallError(operation, Marshal.GetLastWin32Error());
    }

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
        public MouseEventFlags Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public KeyEventFlags Flags;
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

    // MOUSEINPUT.Flags — bit flags describing which mouse event(s) an INPUT carries, per the Win32 MOUSEEVENTF_* contract.
    [Flags]
    internal enum MouseEventFlags : uint
    {
        Move       = 0x0001,
        LeftDown   = 0x0002,
        LeftUp     = 0x0004,
        RightDown  = 0x0008,
        RightUp    = 0x0010,
        MiddleDown = 0x0020,
        MiddleUp   = 0x0040,
        XDown      = 0x0080,
        XUp        = 0x0100,
        Wheel      = 0x0800,
        HWheel     = 0x1000,
        Absolute   = 0x8000,
    }

    // mouseData values for MouseEventFlags.XDown/XUp — identifies which X button (Back/Forward).
    internal const uint XButton1 = 0x0001;
    internal const uint XButton2 = 0x0002;

    // One notch of a standard mouse wheel, per the Win32 WHEEL_DELTA contract.
    internal const int WheelDelta = 120;

    // KEYBDINPUT.Flags — bit flags describing how a keyboard INPUT should be interpreted, per the Win32 KEYEVENTF_* contract.
    [Flags]
    internal enum KeyEventFlags : uint
    {
        ExtendedKey = 0x0001,
        KeyUp       = 0x0002,
        Unicode     = 0x0004,
        Scancode    = 0x0008,
    }

    // VK_* codes stay plain consts (not a MouseEventFlags/KeyEventFlags-style enum) since they're never OR'd and VirtualKeyA needs plain arithmetic for the A-Z range.
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
    internal static void SendMouseClick(int x, int y, IEngineLogger? logger = null)
    {
        var w  = GetSystemMetrics(SmCxScreen);
        var h  = GetSystemMetrics(SmCyScreen);
        var nx = (int)(x * 65536L / w);
        var ny = (int)(y * 65536L / h);
        INPUT[] inputs =
        [
            // MOVE first — WinUI3 hit-testing needs the pointer over the target before LEFTDOWN, or the click is swallowed.
            new() { Type = InputTypeMouse, Data = new InputUnion { Mouse = new MOUSEINPUT { Dx = nx, Dy = ny, Flags = MouseEventFlags.Move     | MouseEventFlags.Absolute } } },
            new() { Type = InputTypeMouse, Data = new InputUnion { Mouse = new MOUSEINPUT { Dx = nx, Dy = ny, Flags = MouseEventFlags.LeftDown | MouseEventFlags.Absolute } } },
            new() { Type = InputTypeMouse, Data = new InputUnion { Mouse = new MOUSEINPUT { Dx = nx, Dy = ny, Flags = MouseEventFlags.LeftUp   | MouseEventFlags.Absolute } } },
        ];
        SendInputs(inputs, logger);
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
    internal static extern bool SetWindowPos(IntPtr windowHandle, IntPtr insertAfterWindowHandle, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

    // Changes the show state of windowHandle (minimize, maximize, restore, etc.).
    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr windowHandle, int nCmdShow);

    // Posts a message to windowHandle's message queue without waiting for it to be processed; used for WM_CLOSE.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool PostMessage(IntPtr windowHandle, uint msg, IntPtr wParam, IntPtr lParam);

    internal const uint WmClose = 0x0010;

    // SetWindowPos's uFlags — bit flags controlling which aspects of the window are left unchanged, per the Win32 SWP_* contract.
    [Flags]
    internal enum SetWindowPosFlags : uint
    {
        None       = 0,
        NoSize     = 0x0001,
        NoMove     = 0x0002,
        NoZOrder   = 0x0004,
        NoActivate = 0x0010,
    }

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
