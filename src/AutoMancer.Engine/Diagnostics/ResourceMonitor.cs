// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Diagnostics;

// Reads resource counters (GDI/USER objects, handles, threads, memory) for an automated app to check an operation for leaks; also has internal helpers to check the engine's own process.
public static class ResourceMonitor
{
    // Reads the app's counters right now; compare two readings with ResourceSample.DiffTo.
    public static ResourceSample Capture(App app) => ResourceSample.Capture(app.ProcessId);

    // Reads the counters of the process running AutoMancer (not the machine) right now, garbage-collecting first so unfinalized wrappers do not read as leaks; internal because it checks the engine itself, which is the maintainers' question, not a tester's.
    internal static ResourceSample CaptureHost()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return ResourceSample.Capture(Environment.ProcessId);
    }

    // Captures the app's counters before and after work and returns the deltas.
    public static async Task<ResourceSummary> MeasureAsync(App app, Func<Task> work)
    {
        var before = Capture(app);
        await work();
        return before.DiffTo(Capture(app));
    }

    // Captures the AutoMancer-hosting process's counters before and after work and returns the deltas.
    internal static async Task<ResourceSummary> MeasureHostAsync(Func<Task> work)
    {
        var before = CaptureHost();
        await work();
        return before.DiffTo(CaptureHost());
    }
}
