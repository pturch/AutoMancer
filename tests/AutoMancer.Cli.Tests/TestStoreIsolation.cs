// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.CompilerServices;

namespace AutoMancer.Cli.Tests;

// Points the CLI state file at a throwaway path for the whole test run, so tests never read or change the user's real current target and settings.
internal static class TestStoreIsolation
{
    // Runs once when the test assembly loads, before any test.
    [ModuleInitializer]
    internal static void Init()
    {
        CliState.FilePath = Path.Combine(Path.GetTempPath(), $"automancer-cli-tests-state-{Environment.ProcessId}", "state.json");
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { Directory.Delete(Path.GetDirectoryName(CliState.FilePath)!, recursive: true); }
            catch (IOException) { }
        };
    }
}
