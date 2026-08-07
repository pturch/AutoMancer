// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Errors;

// Thrown by ElementResolver.WaitUntilGoneAsync when a provider still finds the element after the implicit wait expires.
public sealed class ElementStillPresentError : Exception
{
    public Locator Locator { get; }
    public int ElapsedMs { get; }

    // Thrown by ElementResolver after the implicit wait expires with the element still resolvable by at least one provider.
    public ElementStillPresentError(Locator locator, int elapsedMs)
        : base($"Element still present: {locator.Strategy}={locator.Value} after {elapsedMs}ms")
    {
        Locator = locator;
        ElapsedMs = elapsedMs;
    }
}
