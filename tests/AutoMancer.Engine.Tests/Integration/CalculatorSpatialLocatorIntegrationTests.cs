// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies Locator.Near resolves against a live UIA3 tree end-to-end, then drives real input with the resolved handles: Calculator's number pad is a tight, evenly-spaced grid, so "Five"'s four cardinal neighbors (Eight/Two/Four/Six) are unambiguous nearest-candidate matches.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class CalculatorSpatialLocatorIntegrationTests : IAsyncLifetime
{
    private const string CalculatorAumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
    private const int MaxDistancePx = 120; // covers the ~110px grid pitch to each orthogonal neighbor, excludes the ~128px diagonal neighbors

    private App? _app;

    public async Task InitializeAsync()
    {
        _app = await App.LaunchPackagedAsync(CalculatorAumid);
        var warmup = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 8_000 });
        await warmup.FindAsync(Locator.ByAutomationId("CalculatorResults"));
    }

    public async Task DisposeAsync()
    {
        if (_app is not null) await _app.KillAsync();
    }

    // Resolves all four of Five's grid neighbors by direction, then clicks Above (Eight) + plus + Below (Two) + equals through the spatial locators themselves and checks the display for 8 + 2 = 10 — proving the resolved handles are genuinely interactable, not just correctly identified.
    [Fact]
    public async Task NearFive_FindsAllFourAdjacentDigits_AndComputesEightPlusTwo()
    {
        var five = Locator.ByAutomationId("num5Button");

        var above = await _app!.FindAsync(Locator.Near(five, SpatialDirection.Above, MaxDistancePx));
        var below = await _app.FindAsync(Locator.Near(five, SpatialDirection.Below, MaxDistancePx));
        var left = await _app.FindAsync(Locator.Near(five, SpatialDirection.LeftOf, MaxDistancePx));
        var right = await _app.FindAsync(Locator.Near(five, SpatialDirection.RightOf, MaxDistancePx));

        Assert.Equal("num8Button", above.AutomationId);
        Assert.Equal("num2Button", below.AutomationId);
        Assert.Equal("num4Button", left.AutomationId);
        Assert.Equal("num6Button", right.AutomationId);

        await _app.ClickAsync(Locator.Near(five, SpatialDirection.Above, MaxDistancePx));
        await _app.ClickAsync(Locator.ByAutomationId("plusButton"));
        await _app.ClickAsync(Locator.Near(five, SpatialDirection.Below, MaxDistancePx));
        await _app.ClickAsync(Locator.ByAutomationId("equalButton"));

        var result = await _app.FindAsync(Locator.ByAutomationId("CalculatorResults"));
        Assert.Contains("10", result.Name);
    }
}
