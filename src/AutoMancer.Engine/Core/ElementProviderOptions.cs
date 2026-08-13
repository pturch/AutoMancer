// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// Configures which UI providers ElementResolver tries, in what order, and how long/often it retries before giving up.
public sealed class ElementProviderOptions
{
    public IReadOnlyList<string> ProviderChain { get; init; } = ["uia3", "uia2", "win32"];
    public int ImplicitWaitMs { get; init; } = 5000;
    public int PollIntervalMs { get; init; } = 500;

    // Preconfigured defaults suitable for most automation scenarios.
    public static ElementProviderOptions Default { get; } = new();
}
