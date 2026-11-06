// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies ResourceMonitor against a live Notepad (the app's counters) and against the engine's own process across repeated launch/kill cycles.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class ResourceMeasureIntegrationTests
{
    // Measuring the app returns plausible before and after readings around the work.
    [Fact]
    public async Task MeasureAsync_App_ReturnsPlausibleReadings()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var summary = await ResourceMonitor.MeasureAsync(app,
                () => app.TypeAsync(Locator.ByControlType("Document"), "AutoMancer measure"));

            Assert.All(new[] { summary.Before, summary.After }, s =>
            {
                Assert.InRange(s.GdiObjects, 1, 100_000);
                Assert.InRange(s.UserObjects, 1, 100_000);
                Assert.True(s.HandleCount > 0 && s.ThreadCount > 0 && s.PrivateBytes > 0);
                Assert.InRange(s.WorkingSetBytes, 1L << 20, 4L << 30);
            });
            await app.ClearAsync(Locator.ByControlType("Document"));
        }
        finally
        {
            await app.KillAsync();
            await app.DisposeAsync();
        }
    }

    // The engine's own process doesn't accumulate handles across repeated launch/kill cycles; one warm-up cycle runs first so one-time UIA/COM/thread-pool setup isn't counted.
    [Fact]
    public async Task MeasureHostAsync_LaunchKillCycles_DoNotLeakHandles()
    {
        static async Task LaunchFindKillAsync()
        {
            var app = await App.LaunchAsync("notepad.exe");
            try { await app.FindAsync(Locator.ByControlType("Document")); }
            finally { await app.KillAsync(); await app.DisposeAsync(); }
        }

        await LaunchFindKillAsync();

        var summary = await ResourceMonitor.MeasureHostAsync(async () =>
        {
            for (var cycle = 0; cycle < 10; cycle++) await LaunchFindKillAsync();
        });

        Assert.True(summary.GdiDelta <= 10 && summary.UserDelta <= 10 && summary.HandleDelta <= 20, summary.ToString());
    }
}
