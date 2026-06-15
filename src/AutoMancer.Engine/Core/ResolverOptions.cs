
// Copyright (c) AutoMancer Contributors. Licensed under the MIT License.
namespace AutoMancer.Engine.Core;

public sealed class ResolverOptions
{
    public IReadOnlyList<string> ProviderChain { get; init; } = ["uia3", "uia2", "win32"];
    public int ImplicitWaitMs { get; init; } = 5000;
    public int PollIntervalMs { get; init; } = 500;
    public bool DpiNormalize { get; init; } = true;

    // Preconfigured defaults suitable for most automation scenarios.
    public static ResolverOptions Default { get; } = new();
}
