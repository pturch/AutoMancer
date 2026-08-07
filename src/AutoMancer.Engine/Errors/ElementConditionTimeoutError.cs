// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Errors;

// Thrown by ElementResolver.WaitForAsync when the condition never becomes true before the implicit wait expires, whether or not the element was ever found.
public sealed class ElementConditionTimeoutError : Exception
{
    public Locator Locator { get; }
    public int ElapsedMs { get; }

    // Thrown by ElementResolver after the implicit wait expires with the wait condition still unsatisfied.
    public ElementConditionTimeoutError(Locator locator, int elapsedMs)
        : base($"Wait condition not met: {locator.Strategy}={locator.Value} after {elapsedMs}ms")
    {
        Locator = locator;
        ElapsedMs = elapsedMs;
    }
}
