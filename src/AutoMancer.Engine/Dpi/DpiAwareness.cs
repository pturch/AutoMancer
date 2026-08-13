// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Dpi;

// Declares the process DPI-aware once at startup so coordinate APIs report true physical pixels.
internal static class DpiAwareness
{
    // Called once from AppSession's static constructor, before any coordinate query runs; safe to call more than once.
    internal static void EnsureConfigured() =>
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessContextPerMonitorAwareV2);
}
