// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine;

// Configures the App façade: which providers to use, how long to wait for elements, and how long to pause after each action.
public sealed class AppOptions
{
    // Provider lookup order — each name must match a known IElementProvider.ProviderName.
    public IReadOnlyList<string> ProviderChain { get; init; } = ["uia3", "uia2", "win32"];

    // Total milliseconds to retry the provider chain before throwing ElementNotFoundError.
    public int ImplicitWaitMs { get; init; } = 5_000;

    // Milliseconds between retry attempts during implicit wait.
    public int PollIntervalMs { get; init; } = 500;

    // Milliseconds to pause after each ClickAsync / TypeAsync call so the UI can settle.
    // Covers SendInput key-queue latency (~10 ms/key) and UI render time. Set to 0 to disable.
    public int ActionDelayMs { get; init; } = 150;

    // Sensible defaults for most automation scenarios.
    public static AppOptions Default { get; } = new();
}
