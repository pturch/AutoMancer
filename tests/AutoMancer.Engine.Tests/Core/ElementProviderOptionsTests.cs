// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Core;

public sealed class ElementProviderOptionsTests
{
    [Fact]
    public void Default_HasFullProviderChainAndStandardTimings()
    {
        var options = ElementProviderOptions.Default;

        Assert.Equal(["uia3", "uia2", "win32"], options.ProviderChain);
        Assert.Equal(5000, options.ImplicitWaitMs);
        Assert.Equal(500, options.PollIntervalMs);
    }
}
