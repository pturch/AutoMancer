// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.CompilerServices;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Demonstrates the "set once" policy pattern: runs before any test in this assembly, no per-class configuration needed.
internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Configure() => AutoMancerTestOptions.CaptureScreenshotsOnFailure = true;
}
