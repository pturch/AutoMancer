// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Shared Notepad instance for a test class — launched once in InitializeAsync, killed in DisposeAsync.
public sealed class NotepadFixture : IAsyncLifetime
{
    public App App { get; private set; } = null!;

    // Launches Notepad, warms up the UIA3 tree so the first test doesn't hit a cold-start miss, and clears any text left over from a reused single-instance window.
    public async Task InitializeAsync()
    {
        App = await App.LaunchAsync("notepad.exe");
        await App.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 5_000, PollIntervalMs = 200 })
                 .FindAsync(Locator.ByControlType("Document"));
        await ClearDocumentAsync(App);
    }

    // Dismisses any open popup, discards any text the test classes typed, kills Notepad, and waits for the process to fully exit; no-ops if InitializeAsync itself failed to launch, so that failure surfaces on its own instead of being masked by a NullReferenceException here.
    public async Task DisposeAsync()
    {
        if (App is null) return;
        SendEscape();
        await ClearDocumentAsync(App);
        await App.KillAsync();
        await App.DisposeAsync();
    }

    // Sends a bare Escape to dismiss any open menu or popup; shared by test classes.
    internal static void SendEscape()
    {
        KeybdEvent(0x1B, 0, 0, IntPtr.Zero);
        KeybdEvent(0x1B, 0, 2, IntPtr.Zero);
    }

    // Clears the Document's text; a reused single-instance window never carries unsaved content forward, so a graceful window close never hits Notepad's "Save changes?" prompt. Shared by test classes that launch their own ad hoc Notepad instance.
    internal static Task ClearDocumentAsync(App app) => app.ClearAsync(Locator.ByControlType("Document"));

    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void KeybdEvent(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
}
