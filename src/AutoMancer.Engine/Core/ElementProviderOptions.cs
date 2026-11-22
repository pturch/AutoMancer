// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// Configures which UI providers ElementResolver tries, in what order, and how long/often it retries before giving up.
public sealed class ElementProviderOptions
{
    public IReadOnlyList<string> ProviderChain { get; init; } = ["uia3", "uia2", "win32"];
    public int ImplicitWaitMs { get; init; } = 5000;
    public int PollIntervalMs { get; init; } = 500;

    // Mirrors AppOptions.ReresolveOnStale for the resolver's own Locator-scope paths: true re-resolves a scope that goes stale mid-call, false lets StaleElementError propagate.
    public bool ReresolveOnStale { get; init; } = true;

    // Mirrors AppOptions.AnnotateFailureScreenshots: true saves a window screenshot with a red overlay to %TEMP% when ElementNotFoundError is thrown from a provider-chain find.
    public bool AnnotateFailureScreenshots { get; init; } = false;

    // Preconfigured defaults suitable for most automation scenarios.
    public static ElementProviderOptions Default { get; } = new();
}
