// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Diagnostics;

// One reading of a process's GDI/USER objects, kernel handles, threads, and working-set/private memory.
public readonly record struct ResourceSample(int GdiObjects, int UserObjects, int HandleCount, int ThreadCount, long WorkingSetBytes, long PrivateBytes)
{
    // Reads the counters for processId right now; throws if the process has exited.
    internal static ResourceSample Capture(int processId)
    {
        using var process = Process.GetProcessById(processId);
        var handle = process.Handle;
        return new((int)NativeMethods.GetGuiResources(handle, NativeMethods.GR_GDIOBJECTS), (int)NativeMethods.GetGuiResources(handle, NativeMethods.GR_USEROBJECTS),
            process.HandleCount, process.Threads.Count, process.WorkingSet64, process.PrivateMemorySize64);
    }

    // Compares this reading (the earlier one) to a later reading; the summary's deltas are later minus this.
    public ResourceSummary DiffTo(ResourceSample later) => new(this, later);

    // One-line readout, e.g. "GDI=45 USER=30 Handles=210 Threads=18 WS=52.3 MB Private=30.1 MB".
    public override string ToString() =>
        $"GDI={GdiObjects} USER={UserObjects} Handles={HandleCount} Threads={ThreadCount} WS={WorkingSetBytes / 1048576.0:F1} MB Private={PrivateBytes / 1048576.0:F1} MB";
}
