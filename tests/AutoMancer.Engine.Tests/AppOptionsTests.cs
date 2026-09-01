// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;

namespace AutoMancer.Engine.Tests;

public sealed class AppOptionsTests
{
    [Fact]
    public void Default_HasFullProviderChainAndNonZeroActionDelay()
    {
        var options = AppOptions.Default;

        Assert.Equal(["uia3", "uia2", "win32"], options.ProviderChain);
        Assert.Equal(5_000, options.ImplicitWaitMs);
        Assert.Equal(500, options.PollIntervalMs);
        Assert.Equal(150, options.ActionDelayMs);
        Assert.NotNull(options.Logger);
        Assert.Null(options.WindowsEventLogger);
        Assert.Null(options.CombinedLogger);
    }

    // TestDefaults trades a longer implicit wait (tolerate slow CI) for a shorter action delay
    // (tests retry-assert via WaitForAsync/Expect() instead of relying on a fixed settle pause).
    [Fact]
    public void TestDefaults_LongerWaitShorterActionDelay()
    {
        var options = AppOptions.TestDefaults;

        Assert.Equal(10_000, options.ImplicitWaitMs);
        Assert.Equal(50, options.ActionDelayMs);
    }
}
