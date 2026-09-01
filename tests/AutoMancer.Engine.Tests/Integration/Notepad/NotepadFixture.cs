// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Shared Notepad instance for a test class — launched once in InitializeAsync, killed in DisposeAsync.
public sealed class NotepadFixture : IAsyncLifetime
{
    public App App { get; private set; } = null!;

    // Launches Notepad and warms up the UIA3 tree so the first test doesn't hit a cold-start miss.
    public async Task InitializeAsync()
    {
        App = await App.LaunchAsync("notepad.exe");
        await App.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 5_000, PollIntervalMs = 200 })
                 .FindAsync(Locator.ByControlType("Document"));
    }

    // Dismisses any open popup, kills Notepad, and waits for the process to fully exit.
    public async Task DisposeAsync()
    {
        SendEscape();
        await App.KillAsync();
        await App.DisposeAsync();
    }

    // Sends a bare Escape to dismiss any open menu or popup; shared by test classes.
    internal static void SendEscape()
    {
        KeybdEvent(0x1B, 0, 0, IntPtr.Zero);
        KeybdEvent(0x1B, 0, 2, IntPtr.Zero);
    }

    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void KeybdEvent(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
}
