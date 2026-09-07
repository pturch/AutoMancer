// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests;

public sealed class AppTests
{
    private static Process ExitedProcess()
    {
        var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        process.WaitForExit(2000);
        return process;
    }

    [Fact]
    public void RootWindowHandle_ReturnsTheSessionsWindowHandle()
    {
        var session = AppSession.CreateForTesting(ExitedProcess(), (IntPtr)42);
        var app = App.CreateForTesting(session, [], new AppOptions { Logger = null });

        Assert.Equal((IntPtr)42, app.RootWindowHandle);
    }

    [Fact]
    public void IsWindowMinimized_NotARealWindow_ReturnsFalse()
    {
        var session = AppSession.CreateForTesting(ExitedProcess(), (IntPtr)42);
        var app = App.CreateForTesting(session, [], new AppOptions { Logger = null });

        Assert.False(app.IsWindowMinimized());
    }

    [Fact]
    public void GetTopLevelWindow_HandleWithNoResolvableAncestor_ReturnsTheHandleItself()
    {
        // Not a real window, so GetAncestor can't resolve anything — must fall back to the input handle rather than IntPtr.Zero.
        var bogusHandle = (IntPtr)0x7FFFFFFF;

        Assert.Equal(bogusHandle, App.GetTopLevelWindow(bogusHandle));
    }

    // Regression coverage for AppOptions.KillEntireProcessTree threading through to AppSession.KillApp(bool) — the already-exited process here means KillApp is a no-op either way, so this only proves the option doesn't crash the Kill/Dispose path, not that Process.Kill(true) actually reaches child processes (that's exercised for real in samples/ConsumerVsCodeTests, against an app that actually spawns helper processes).
    [Fact]
    public async Task KillAsync_WithKillEntireProcessTree_DoesNotThrow()
    {
        var session = AppSession.CreateForTesting(ExitedProcess(), (IntPtr)42);
        var app = App.CreateForTesting(session, [], new AppOptions { Logger = null, KillEntireProcessTree = true });

        await app.KillAsync();
    }
}
