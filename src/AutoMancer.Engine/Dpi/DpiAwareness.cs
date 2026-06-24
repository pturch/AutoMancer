// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Dpi;

internal static class DpiAwareness
{
    // Declares the process Per-Monitor-V2 DPI aware — called once from AppSession's static constructor, before any
    // UIA or Win32 coordinate query runs — so bounding rects and SendInput coordinates agree on true physical pixels
    // with no per-process virtualization (the default for an unmanifested process would otherwise scale every rect
    // as if every monitor were 96 DPI). Safe to call more than once: SetProcessDpiAwarenessContext just fails after
    // the first successful call.
    internal static void EnsureConfigured() =>
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessContextPerMonitorAwareV2);
}
