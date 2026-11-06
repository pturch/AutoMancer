// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies ResourceMonitor against a live Notepad's counters.
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
}
