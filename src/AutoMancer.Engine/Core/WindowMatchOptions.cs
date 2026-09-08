// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// Criteria for picking the right window among multiple candidates — e.g. a reused single-instance app, or another process sharing the same name. Used by every AppSession window-locating method.
public sealed record WindowMatchOptions
{
    // Only accept a window whose title contains this substring (case-insensitive); null accepts any title.
    public string? TitleContains { get; init; }

    // Only accept a window whose class name equals this value (case-insensitive); null accepts any class.
    public string? ClassName { get; init; }

    // When true, rejects handing off to a window that already existed before the call started. Only honored by LaunchAsync; ignored elsewhere. Default false — apps like Notepad legitimately reuse an existing window.
    public bool RequireNewWindow { get; init; }

    // Only accept a window owned by this exact process ID; null accepts any PID — unlike title/class name, a PID can't collide with an unrelated process sharing the same executable name.
    public int? ExpectedPid { get; init; }
}
