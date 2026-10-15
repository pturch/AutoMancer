// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;
using AutoMancer.Testing;
using static AutoMancer.Testing.Assertions;
using Xunit.Sdk;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies App.CloseWindowAsync against Calculator; in the "Notepad" collection to serialize foreground/SendInput use. Known flaky: UWP teardown timing after WM_CLOSE is OS-level, not a product defect.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class WindowCloseIntegrationTests
{
    private const string CalculatorAumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";

    // UWP teardown timing after WM_CLOSE is unbounded under load and can exceed even RunOnceAsync's generous budget; rerun the whole scenario once before failing.
    [Fact]
    public async Task CloseWindowAsync_ClosesTheWindow()
    {
        for (int testAttempt = 1; testAttempt <= 2; testAttempt++)
        {
            try
            {
                await RunOnceAsync();
                return;
            }
            catch (XunitException) when (testAttempt < 2)
            {
            }
        }
    }

    // Launches Calculator, retries CloseWindowAsync across a generous budget, and asserts the window is gone; always kills the process afterward.
    private static async Task RunOnceAsync()
    {
        var app = await App.LaunchPackagedAsync(CalculatorAumid);
        var windowHandle = Process.GetProcessById(app.ProcessId).MainWindowHandle;

        // Calculator has no WindowPattern support, so this hits the WM_CLOSE fallback, whose UWP teardown can exceed 30s under load; retries across a generous budget rather than a single short wait.
        bool IsStillOpen() => NativeMethods.IsWindowVisible(windowHandle) && !NativeMethods.IsIconic(windowHandle);

        try
        {
            // WM_CLOSE sent while the window is still coming up can be dropped, so close only once its UI is on screen.
            await app.WithOptions(new AppOptions { ImplicitWaitMs = 10_000 }).WaitForAsync(Locator.ByAutomationId("CalculatorResults"), WaitConditions.IsVisible());

            for (int attempt = 0; attempt < 3 && IsStillOpen(); attempt++)
            {
                await app.CloseWindowAsync();
                try { await Expect(app, () => Task.FromResult(IsStillOpen())).ToSatisfyAsync(open => !open, "the window to close", timeoutMs: 20_000); }
                catch (ExpectFailedError) { /* still open — the loop resends WM_CLOSE */ }
            }

            Assert.False(IsStillOpen());
        }
        finally
        {
            // Safety net: UWP hosts often stay resident after their window closes, so kill it to avoid lingering for the next test class — must run even when the assertion above fails.
            await app.KillAsync();
        }
    }
}
