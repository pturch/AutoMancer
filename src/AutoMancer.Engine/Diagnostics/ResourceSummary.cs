// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Diagnostics;

// The readings taken before and after a block of work, with after-minus-before deltas — what a leak check compares.
public sealed record ResourceSummary(ResourceSample Before, ResourceSample After)
{
    // Change in GDI object count.
    public int GdiDelta => After.GdiObjects - Before.GdiObjects;

    // Change in USER object count.
    public int UserDelta => After.UserObjects - Before.UserObjects;

    // Change in kernel handle count.
    public int HandleDelta => After.HandleCount - Before.HandleCount;

    // Change in thread count.
    public int ThreadDelta => After.ThreadCount - Before.ThreadCount;

    // Change in working set bytes.
    public long WorkingSetDelta => After.WorkingSetBytes - Before.WorkingSetBytes;

    // Change in private bytes.
    public long PrivateDelta => After.PrivateBytes - Before.PrivateBytes;

    // Table of before/after/delta per metric, readable in a test failure message.
    public override string ToString() =>
        $"              before     after     delta\n" +
        $"GDI       {Before.GdiObjects,9} {After.GdiObjects,9} {GdiDelta,9:+0;-0;0}\n" +
        $"USER      {Before.UserObjects,9} {After.UserObjects,9} {UserDelta,9:+0;-0;0}\n" +
        $"Handles   {Before.HandleCount,9} {After.HandleCount,9} {HandleDelta,9:+0;-0;0}\n" +
        $"Threads   {Before.ThreadCount,9} {After.ThreadCount,9} {ThreadDelta,9:+0;-0;0}\n" +
        $"WS (MB)   {Before.WorkingSetBytes / 1048576.0,9:F1} {After.WorkingSetBytes / 1048576.0,9:F1} {WorkingSetDelta / 1048576.0,9:+0.0;-0.0;0.0}\n" +
        $"Priv (MB) {Before.PrivateBytes / 1048576.0,9:F1} {After.PrivateBytes / 1048576.0,9:F1} {PrivateDelta / 1048576.0,9:+0.0;-0.0;0.0}";
}
