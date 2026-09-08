// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Testing.XUnit;

namespace AutoMancer.Engine.Tests.Integration;

// AppFixture for Notepad, used by the AutoMancerTest/Expect() rewrite of a NotepadWorkflowTests scenario.
public sealed class NotepadAppFixture : AppFixture
{
    // Launches Notepad, wires a plain text-file logger, warms up the UIA3 tree so the first test doesn't hit a cold-start miss, and clears any text left over from a reused single-instance window.
    protected override async Task<App> CreateAppAsync()
    {
        var logger = new EngineLogger(new StreamWriter(Path.Combine(LogDirectory, "automancer-notepad-tests.log")) { AutoFlush = true });
        var app = await App.LaunchAsync("notepad.exe", new AppOptions { Logger = logger });
        await app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 5_000, PollIntervalMs = 200, Logger = logger })
                 .FindAsync(Locator.ByControlType("Document"));
        await app.ClearAsync(Locator.ByControlType("Document"));
        return app;
    }

    // Discards any text this fixture's test typed, so a reused single-instance window never carries unsaved content into the next launch.
    protected override Task OnBeforeKillAsync() => App.ClearAsync(Locator.ByControlType("Document"));
}
