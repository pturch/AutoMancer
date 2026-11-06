// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Testing.XUnit;
using Xunit.Abstractions;

namespace ConsumerNotepadTests;

// Shows ResourceMonitor: what a block of work costs the app, and readings at checkpoints you choose.
[Collection("ConsumerNotepad")]
public sealed class ResourceMeasureDemoTests(NotepadFixture fixture, ITestOutputHelper output) : AutoMancerTest(fixture)
{
    private static readonly Locator Document = Locator.ByControlType("Document");

    // MeasureAsync reads Notepad's counters before and after the work; the summary's deltas are what a leak check compares, and ToString is a readable table.
    [Fact]
    public async Task MeasureAsync_ReportsWhatTheWorkCostNotepad()
    {
        var summary = await ResourceMonitor.MeasureAsync(App, async () =>
        {
            await App.ClickAsync(Document);
            await App.TypeAsync(Document, "Measuring resources while typing.");
            await App.ClearAsync(Document);
        });

        output.WriteLine(summary.ToString());
        Assert.True(summary.UserDelta < 50, "typing should not add dozens of USER objects");
    }

    // Capture takes a reading at any point; DiffTo compares any two, so a multi-step flow can be checked at every checkpoint instead of wrapped in one lambda.
    [Fact]
    public async Task Capture_ComparesAnyTwoCheckpoints()
    {
        var start = ResourceMonitor.Capture(App);
        await App.ClickAsync(Document);
        await App.TypeAsync(Document, "Checkpoint one.");
        var typed = ResourceMonitor.Capture(App);
        await App.ClearAsync(Document);
        var cleared = ResourceMonitor.Capture(App);

        output.WriteLine("start -> typed\n" + start.DiffTo(typed));
        output.WriteLine("start -> cleared\n" + start.DiffTo(cleared));
        Assert.True(start.DiffTo(cleared).UserDelta < 50);
    }
}
