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

    // Proves the thin delegate reaches the real registrar even with an empty provider chain — RegisterCustomPropertyAsync isn't part of IElementProvider and never goes through ElementResolver, so session/providers are irrelevant to this call.
    [Fact]
    public async Task RegisterCustomPropertyAsync_WorksWithNoProvidersConfigured()
    {
        var session = AppSession.CreateForTesting(ExitedProcess(), (IntPtr)42);
        var app = App.CreateForTesting(session, [], new AppOptions { Logger = null });

        var id = await app.RegisterCustomPropertyAsync(Guid.NewGuid(), "AutoMancer.Test.Property", UiaAutomationType.String);

        Assert.True(id > 0);
    }

    // A windowless process that stays alive for 30s, so a test can tell whether disposing an App killed it.
    private static Process RunningProcess() =>
        Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 30 127.0.0.1 > nul") { UseShellExecute = false, CreateNoWindow = true })!;

    // An App that doesn't own its process (e.g. one sharing another App's session) must leave that process running when disposed.
    [Fact]
    public async Task DisposeAsync_AppNotOwningItsProcess_LeavesProcessRunning()
    {
        using var process = RunningProcess();
        try
        {
            var dialog = App.CreateForTesting(AppSession.CreateForTesting(process, IntPtr.Zero), [], new AppOptions { Logger = null }, ownsProcess: false);

            await dialog.DisposeAsync();

            Assert.False(process.HasExited);
        }
        finally { process.Kill(); }
    }

    // A WithOptions view shares its parent's session: disposing the view leaves the process to the parent, whose own dispose still kills it.
    [Fact]
    public async Task DisposeAsync_WithOptionsView_LeavesProcessToItsOwner()
    {
        var process = RunningProcess();
        // A second handle on the same process, since the owner's dispose disposes the session's Process object.
        using var watcher = Process.GetProcessById(process.Id);
        try
        {
            var owner = App.CreateForTesting(AppSession.CreateForTesting(process, IntPtr.Zero), [], new AppOptions { Logger = null });
            var view = owner.WithOptions(new AppOptions { Logger = null, ProviderChain = ["win32"] });

            await view.DisposeAsync();
            Assert.False(watcher.HasExited);

            await owner.DisposeAsync();
            Assert.True(watcher.WaitForExit(5_000));
        }
        finally { if (!watcher.HasExited) watcher.Kill(); }
    }
}
