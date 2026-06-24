// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Core;

// Orchestrates the provider fallback chain: retries every provider in ElementProviderOptions.ProviderChain order until a match is found or the implicit wait expires.
public sealed class ElementResolver
{
    private readonly IReadOnlyList<IElementProvider> _providers;
    private readonly ElementProviderOptions _options;
    private readonly EngineLogger? _logger;

    // Orders providers by ElementProviderOptions.ProviderChain, dropping any chain entry with no matching provider.
    public ElementResolver(IEnumerable<IElementProvider> providers, ElementProviderOptions? options = null, EngineLogger? logger = null)
    {
        _options = options ?? ElementProviderOptions.Default;
        _logger = logger;

        var byName = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        _providers = _options.ProviderChain
            .Where(byName.ContainsKey)
            .Select(name => byName[name])
            .ToList();
    }

    // Polls the provider chain every PollIntervalMs until a match is found or ImplicitWaitMs elapses; throws ElementNotFoundError with a closest-match hint on timeout.
    public async Task<ElementHandle> FindAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var attempted = new List<string>();

        while (true)
        {
            foreach (var provider in _providers)
            {
                if (!attempted.Contains(provider.ProviderName))
                    attempted.Add(provider.ProviderName);

                var found = await provider.FindElementAsync(locator, session, ct).ConfigureAwait(false);
                if (found is not null)
                {
                    _logger?.Info("Element resolved", new { provider = provider.ProviderName, locator.Strategy, locator.Value, elapsedMs = stopwatch.ElapsedMilliseconds });
                    return found;
                }
            }

            if (stopwatch.ElapsedMilliseconds >= _options.ImplicitWaitMs)
                break;

            await Task.Delay(_options.PollIntervalMs, ct).ConfigureAwait(false);
        }

        var tree = await TrySnapshotAsync(session, ct).ConfigureAwait(false);
        var closestMatch = tree is null ? null : ClosestMatchFinder.Find(locator, tree);
        throw new ElementNotFoundError(locator, attempted.ToArray(), (int)stopwatch.ElapsedMilliseconds, closestMatch);
    }

    // Single pass across the provider chain (no retry) — returns the first provider's non-empty match list.
    public async Task<IReadOnlyList<ElementHandle>> FindAllAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        foreach (var provider in _providers)
        {
            var matches = await provider.FindElementsAsync(locator, session, ct).ConfigureAwait(false);
            if (matches.Count > 0)
                return matches;
        }

        return Array.Empty<ElementHandle>();
    }

    // Snapshots the tree from the first provider that returns a non-empty result; null if every provider returns empty.
    public async Task<IReadOnlyList<ElementSnapshot>?> TrySnapshotAsync(AppSession session, CancellationToken ct = default)
    {
        foreach (var provider in _providers)
        {
            var snapshot = await provider.SnapshotTreeAsync(session, ct).ConfigureAwait(false);
            if (snapshot.Count > 0)
                return snapshot;
        }

        return null;
    }
}
