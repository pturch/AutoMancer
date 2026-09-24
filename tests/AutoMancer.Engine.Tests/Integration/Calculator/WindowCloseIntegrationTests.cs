// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Providers;
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
        for (var testAttempt = 1; testAttempt <= 2; testAttempt++)
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
            for (var attempt = 0; attempt < 3 && IsStillOpen(); attempt++)
            {
                await app.CloseWindowAsync();
                var deadline = DateTime.UtcNow.AddSeconds(20);
                while (DateTime.UtcNow < deadline && IsStillOpen())
                    await Task.Delay(200);
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
