// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Testing.XUnit;

namespace ConsumerVsCodeTests;

// One isolated VS Code shared by a test class, past the first-run walkthrough and with the chat panel hidden, as an AppFixture so tests get Expect() and failure screenshots.
public sealed class VsCodeFixture : AppFixture
{
    private VsCodeLauncher.VsCodeSandbox _sandbox = null!;

    // Launches into a fresh sandbox and clears the walkthrough and chat panel, which would otherwise take focus and keystrokes.
    protected override async Task<App> CreateAppAsync()
    {
        _sandbox = VsCodeLauncher.CreateSandbox();
        var app = await VsCodeLauncher.LaunchAsync(_sandbox);
        try
        {
            await VsCodeLauncher.PrepareWorkbenchAsync(app);
            return app;
        }
        catch
        {
            // AppFixture never receives an App that failed setup, so its DisposeAsync can't clean this one up.
            await VsCodeLauncher.KillAndDeleteAsync(app, _sandbox);
            throw;
        }
    }

    // AppFixture has already killed VS Code's whole process tree (KillEntireProcessTree), so only the sandbox is left.
    protected override Task OnKilledAsync() => VsCodeLauncher.DeleteSandboxAsync(_sandbox);
}
