// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Errors;

// Thrown by ElementResolver when no provider in the chain finds a match before the implicit wait expires.
public sealed class ElementNotFoundError : Exception
{
    public Locator Locator { get; }
    public string[] AttemptedProviders { get; }
    public int ElapsedMs { get; }
    public ClosestMatch? ClosestMatch { get; }

    // Thrown by ElementResolver after the implicit wait expires with no match across all providers.
    public ElementNotFoundError(Locator locator, string[] attemptedProviders, int elapsedMs, ClosestMatch? closestMatch = null)
        : base(BuildMessage(locator, attemptedProviders, elapsedMs, closestMatch))
    {
        Locator = locator;
        AttemptedProviders = attemptedProviders;
        ElapsedMs = elapsedMs;
        ClosestMatch = closestMatch;
    }

    // Builds the human-readable exception message, appending a "Did you mean?" hint when a close match exists.
    private static string BuildMessage(Locator locator, string[] attemptedProviders, int elapsedMs, ClosestMatch? closestMatch)
    {
        var sb = new StringBuilder();
        sb.Append($"Element not found: {locator.Strategy}={locator.Value} after {elapsedMs}ms");
        if (attemptedProviders.Length > 0)
            sb.Append($" (tried: {string.Join(", ", attemptedProviders)})");
        if (closestMatch is not null)
            sb.Append($". Did you mean \"{closestMatch.ElementName}\" (confidence: {closestMatch.Confidence:P0})?");
        return sb.ToString();
    }
}
