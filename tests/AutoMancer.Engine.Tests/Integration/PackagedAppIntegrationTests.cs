// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies that LaunchPackagedAsync can activate a UWP/MSIX app by AUMID and find elements in it.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class PackagedAppIntegrationTests : IAsyncLifetime
{
    private const string CalculatorAumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";

    private App? _app;

    // Launches Calculator via AUMID before each test.
    public async Task InitializeAsync()
    {
        _app = await App.LaunchPackagedAsync(CalculatorAumid);
        // Warmup: wait for the UIA tree to populate after WinUI3 initialization.
        var warmup = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 8_000 });
        await warmup.FindAsync(Locator.ByAutomationId("CalculatorResults"));
    }

    // Kills Calculator and waits for the process to fully exit before the next test.
    public async Task DisposeAsync()
    {
        _app?.Kill();
        await Task.Delay(800);
    }

    // Verifies that the Calculator result display can be found by AutomationId via UIA3.
    [Fact]
    public async Task FindDisplay_ByAutomationId_ResolvesViaUia3()
    {
        var el = await _app!.FindAsync(Locator.ByAutomationId("CalculatorResults"));

        Assert.Equal("uia3", el.ResolvedVia);
        Assert.Equal("CalculatorResults", el.AutomationId);
    }

    // Verifies that the Calculator result display can be found by control type.
    [Fact]
    public async Task FindDisplay_ByControlType_ReturnsTextElement()
    {
        var el = await _app!.FindAsync(Locator.ByAutomationId("CalculatorResults"));

        Assert.Equal("Text", el.ControlType);
    }
}
