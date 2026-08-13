// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// A consumer-authored AppFixture — everything here is public API, nothing borrowed from the solution's own internal test suite.
public sealed class NotepadFixture : AppFixture
{
    protected override async Task<App> CreateAppAsync()
    {
        var app = await App.LaunchAsync("notepad.exe");
        await app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 5_000, PollIntervalMs = 200 })
                 .FindAsync(Locator.ByControlType("Document"));
        return app;
    }

    protected override Task OnKilledAsync() => Task.Delay(800);
}
