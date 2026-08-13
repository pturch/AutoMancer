// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.ComponentModel;

namespace AutoMancer.Engine.Errors;

// Translates a raw Win32 error code (from Marshal.GetLastWin32Error()) into plain text: the OS's own message, plus an AutoMancer-specific hint for the handful of codes that mean something particular in an automation context.
public static class Win32ErrorCodes
{
    // Sparse by design — only codes actually observed coming out of this engine's own P/Invoke calls, not a speculative catalog of the whole Win32 error space.
    private static readonly Dictionary<int, string> AutomationHints = new()
    {
        [5] = "likely UIPI: the target process is running at a higher privilege level than AutoMancer (elevated/UAC), which blocks synthetic input across that boundary. Try running AutoMancer elevated too.",
        [1400] = "the window is no longer valid — it was likely closed, or the owning process exited, since AutoMancer last resolved it.",
    };

    // Returns "{OS message} (Win32 error {code})", with an AutoMancer-specific hint appended when the code is a known one.
    public static string Describe(int win32Error)
    {
        var message = $"{new Win32Exception(win32Error).Message} (Win32 error {win32Error})";
        return AutomationHints.TryGetValue(win32Error, out var hint) ? $"{message} — {hint}" : message;
    }
}
