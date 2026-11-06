// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Diagnostics;

// Reads resource counters (GDI/USER objects, handles, threads, memory) for an automated app, to check an operation for leaks.
public static class ResourceMonitor
{
    // Reads the app's counters right now; compare two readings with ResourceSample.DiffTo.
    public static ResourceSample Capture(App app) => ResourceSample.Capture(app.ProcessId);

    // Captures the app's counters before and after work and returns the deltas.
    public static async Task<ResourceSummary> MeasureAsync(App app, Func<Task> work)
    {
        var before = Capture(app);
        await work();
        return before.DiffTo(Capture(app));
    }
}
