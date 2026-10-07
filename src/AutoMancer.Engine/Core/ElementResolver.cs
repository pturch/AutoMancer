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
    private readonly IEngineLogger? _logger;
    private readonly int _foregroundActivationTimeoutMs;

    // ---- Construction ----

    // Orders providers by ElementProviderOptions.ProviderChain, dropping any chain entry with no matching provider.
    public ElementResolver(IEnumerable<IElementProvider> providers, ElementProviderOptions? options = null, IEngineLogger? logger = null, int foregroundActivationTimeoutMs = 3_000)
    {
        _options = options ?? ElementProviderOptions.Default;
        _logger = logger;
        _foregroundActivationTimeoutMs = foregroundActivationTimeoutMs;

        var byName = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        _providers = _options.ProviderChain
            .Where(byName.ContainsKey)
            .Select(name => byName[name])
            .ToList();
    }

    // ---- Finding a single element (public FindAsync/FindScopedAsync surface) ----

    // Polls the provider chain every PollIntervalMs until find (given a provider) returns a match or ImplicitWaitMs (or the implicitWaitMs override) elapses; throws ElementNotFoundError with a closest-match hint on timeout.
    // Shared by the plain and scoped FindAsync paths — they only differ in which provider method find calls.
    private async Task<ElementHandle> PollAsync(Locator locator, AppSession session, Func<IElementProvider, Task<ElementHandle?>> find, CancellationToken ct, int? implicitWaitMs = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var attempted = new List<string>();

        while (true)
        {
            foreach (var provider in _providers)
            {
                if (!attempted.Contains(provider.ProviderName))
                    attempted.Add(provider.ProviderName);

                // Delegates the actual lookup to the caller's closure — whole-session or scoped, per which FindAsync overload called in.
                var found = await find(provider).ConfigureAwait(false);
                if (found is not null)
                {
                    found.Logger = _logger;
                    found.ForegroundActivationTimeoutMs = _foregroundActivationTimeoutMs;
                    _logger?.Info("Element resolved", new { provider = provider.ProviderName, locator.Strategy, locator.Value, elapsedMs = stopwatch.ElapsedMilliseconds });
                    return found;
                }
            }

            if (stopwatch.ElapsedMilliseconds >= (implicitWaitMs ?? _options.ImplicitWaitMs))
                break;

            await Task.Delay(_options.PollIntervalMs, ct).ConfigureAwait(false);
        }

        var tree = await TrySnapshotAsync(session, ct).ConfigureAwait(false);
        var closestMatch = tree is null ? null : ClosestMatchFinder.Find(locator, tree);
        throw new ElementNotFoundError(locator, attempted.ToArray(), (int)stopwatch.ElapsedMilliseconds, closestMatch);
    }

    // Polls the provider chain until a match is found anywhere in the session, or ImplicitWaitMs elapses; throws ElementNotFoundError with a closest-match hint on timeout.
    public async Task<ElementHandle> FindAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        if (locator.Strategy == LocatorStrategy.Spatial)
            return await FindSpatialAsync(locator, session, ct).ConfigureAwait(false);

        // Per-poll lookup: ask this provider for locator anywhere in session.
        return await PollAsync(locator, session, provider => provider.FindElementAsync(locator, session, ct), ct).ConfigureAwait(false);
    }

    // Polls the provider chain within a scope handle held fixed for the whole call, throwing StaleElementError if that scope is gone; implicitWaitMs overrides ImplicitWaitMs (0 = a single pass) for callers running their own retry loop.
    internal async Task<ElementHandle> FindScopedAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default, int? implicitWaitMs = null)
    {
        // locator (what we're searching FOR) is Spatial — unsupported combined with any scope, since anchor-relative matching needs a whole-session snapshot; scope is dropped.
        if (locator.Strategy == LocatorStrategy.Spatial)
        {
            _logger?.Warn("Scope is not honored for Spatial locators — searching the whole session instead", new { scope = scope.Id, locator.Direction, locator.MaxDistancePx });
            return await FindSpatialAsync(locator, session, ct).ConfigureAwait(false);
        }

        // Per-poll lookup: ask this provider for locator within scope's subtree.
        return await PollAsync(locator, session, provider => provider.FindScopedElementAsync(locator, session, scope, ct), ct, implicitWaitMs).ConfigureAwait(false);
    }

    // Resolves scope fresh via provider, then finds locator within it — picks up a scope element replaced mid-wait (e.g. a dialog that closes and reopens) instead of getting stuck against a dead handle.
    private async Task<ElementHandle?> FindViaFreshScopeAsync(IElementProvider provider, Locator locator, Locator scope, AppSession session, CancellationToken ct)
    {
        var scopeMatch = await provider.FindElementAsync(scope, session, ct).ConfigureAwait(false);
        if (scopeMatch is null)
            return null;

        try
        {
            return await provider.FindScopedElementAsync(locator, session, scopeMatch, ct).ConfigureAwait(false);
        }
        // Scope died between being resolved and searched — a miss for this attempt; the next poll re-resolves it.
        catch (StaleElementError) when (_options.ReresolveOnStale)
        {
            return null;
        }
    }

    // Resolves scope fresh from the same provider on every poll attempt, instead of once up front.
    public async Task<ElementHandle> FindScopedAsync(Locator locator, AppSession session, Locator scope, CancellationToken ct = default)
    {
        // locator (what we're searching FOR) is Spatial — unsupported combined with any scope, since anchor-relative matching needs a whole-session snapshot; scope is dropped.
        if (locator.Strategy == LocatorStrategy.Spatial)
        {
            _logger?.Warn("Scope is not honored for Spatial locators — searching the whole session instead", new { scope.Strategy, scope.Value, locator.Direction, locator.MaxDistancePx });
            return await FindSpatialAsync(locator, session, ct).ConfigureAwait(false);
        }

        // scope (the container we're searching WITHIN) is Spatial — a different, fully-supported case: only how the container is identified, not what we're searching for. No provider can redo this per poll, so resolve it up front and hold it, re-resolving only if it goes stale.
        if (scope.Strategy == LocatorStrategy.Spatial)
        {
            return await RetryOnStaleScopeAsync(scope, async () =>
            {
                var scopeHandle = await FindAsync(scope, session, ct).ConfigureAwait(false);
                return await FindScopedAsync(locator, session, scopeHandle, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        return await PollAsync(locator, session, provider => FindViaFreshScopeAsync(provider, locator, scope, session, ct), ct).ConfigureAwait(false);
    }

    // Resolves LocatorStrategy.Spatial: the element nearest the anchor in the given direction, via SpatialMatcher.
    private async Task<ElementHandle> FindSpatialAsync(Locator locator, AppSession session, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var anchor = await FindAsync(locator.Anchor!, session, ct).ConfigureAwait(false);
        var tree = await TrySnapshotAsync(session, ct).ConfigureAwait(false);

        // Snapshot-backed stand-ins, only good for rect matching — they have no live native handle.
        var candidates = (tree ?? Array.Empty<ElementSnapshot>())
            .DescendantsAndSelf()
            .Where(s => s.Id != anchor.Id)
            .Select(s => new ElementHandle(s.Id, "snapshot", new object())
            {
                Name = s.Name,
                AutomationId = s.AutomationId,
                ClassName = s.ClassName,
                ControlType = s.ControlType,
                BoundingRect = s.BoundingRect,
            })
            .ToList();

        var nearest = SpatialMatcher.FindNearest(anchor.BoundingRect, candidates, locator.Direction!.Value, locator.MaxDistancePx!.Value);
        if (nearest is null)
            throw new ElementNotFoundError(locator, new[] { "spatial" }, (int)stopwatch.ElapsedMilliseconds);

        // Re-resolve the winner by RuntimeId so the caller gets a fully interactable handle, not the stand-in.
        return await FindAsync(Locator.ByRuntimeId(nearest.Id), session, ct).ConfigureAwait(false);
    }

    // ---- Waiting on element state (public) ----

    // Polls the provider chain every PollIntervalMs until every provider returns null for the locator, or ImplicitWaitMs elapses; throws ElementStillPresentError on timeout.
    public async Task WaitUntilGoneAsync(Locator locator, AppSession session, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var stillPresent = false;
            foreach (var provider in _providers)
            {
                if (await provider.FindElementAsync(locator, session, ct).ConfigureAwait(false) is not null)
                {
                    stillPresent = true;
                    break;
                }
            }

            if (!stillPresent)
            {
                _logger?.Info("Element gone", new { locator.Strategy, locator.Value, elapsedMs = stopwatch.ElapsedMilliseconds });
                return;
            }

            if (stopwatch.ElapsedMilliseconds >= _options.ImplicitWaitMs)
                throw new ElementStillPresentError(locator, (int)stopwatch.ElapsedMilliseconds);

            await Task.Delay(_options.PollIntervalMs, ct).ConfigureAwait(false);
        }
    }

    // Polls the provider chain every PollIntervalMs until the located element satisfies condition, or ImplicitWaitMs elapses; throws ElementConditionTimeoutError on timeout. The primitive Stage 9's Expect() assertions poll on.
    public async Task<ElementHandle> WaitForAsync(Locator locator, Func<ElementHandle, bool> condition, AppSession session, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            foreach (var provider in _providers)
            {
                var found = await provider.FindElementAsync(locator, session, ct).ConfigureAwait(false);
                if (found is not null && condition(found))
                {
                    found.Logger = _logger;
                    found.ForegroundActivationTimeoutMs = _foregroundActivationTimeoutMs;
                    _logger?.Info("Wait condition met", new { provider = provider.ProviderName, locator.Strategy, locator.Value, elapsedMs = stopwatch.ElapsedMilliseconds });
                    return found;
                }
            }

            if (stopwatch.ElapsedMilliseconds >= _options.ImplicitWaitMs)
                throw new ElementConditionTimeoutError(locator, (int)stopwatch.ElapsedMilliseconds);

            await Task.Delay(_options.PollIntervalMs, ct).ConfigureAwait(false);
        }
    }

    // ---- Finding multiple elements (public FindAllAsync/FindAllScopedAsync surface) ----

    // Single pass across the provider chain (no retry) — calls find (given a provider) and returns the first non-empty match list. Shared by the plain and scoped FindAllAsync paths.
    private async Task<IReadOnlyList<ElementHandle>> FindAllViaAsync(Locator locator, Func<IElementProvider, Task<IReadOnlyList<ElementHandle>>> find, CancellationToken ct)
    {
        foreach (var provider in _providers)
        {
            var matches = await find(provider).ConfigureAwait(false);
            if (matches.Count > 0)
            {
                foreach (var match in matches)
                {
                    match.Logger = _logger;
                    match.ForegroundActivationTimeoutMs = _foregroundActivationTimeoutMs;
                }
                _logger?.Info("Elements found", new { provider = provider.ProviderName, locator.Strategy, locator.Value, count = matches.Count });
                return matches;
            }
        }

        return Array.Empty<ElementHandle>();
    }

    // Single pass across the provider chain (no retry) — returns the first provider's non-empty match list from anywhere in the session.
    public async Task<IReadOnlyList<ElementHandle>> FindAllAsync(Locator locator, AppSession session, CancellationToken ct = default) =>
        await FindAllViaAsync(locator, provider => provider.FindElementsAsync(locator, session, ct), ct).ConfigureAwait(false);

    // Single pass across the provider chain (no retry) — returns the first provider's non-empty match list within a scope handle held fixed for the call; StaleElementError if that scope is gone. Used directly for held-handle reuse and as the Spatial-scope fallback in the public overload below.
    internal async Task<IReadOnlyList<ElementHandle>> FindAllScopedAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default) =>
        await FindAllViaAsync(locator, provider => provider.FindScopedElementsAsync(locator, session, scope, ct), ct).ConfigureAwait(false);

    // Resolves scope fresh via provider, then finds all locator matches within it.
    private static async Task<IReadOnlyList<ElementHandle>> FindAllViaFreshScopeAsync(IElementProvider provider, Locator locator, Locator scope, AppSession session, CancellationToken ct)
    {
        var scopeMatch = await provider.FindElementAsync(scope, session, ct).ConfigureAwait(false);
        if (scopeMatch is null)
            return [];

        return await provider.FindScopedElementsAsync(locator, session, scopeMatch, ct).ConfigureAwait(false);
    }

    // Resolves scope fresh from the same provider as part of this call, instead of taking a held handle; a scope that dies between being resolved and searched is re-resolved and searched again (see RetryOnStaleScopeAsync).
    // This also lets the whole provider chain work end to end — a scope resolved by one provider's native handle can't be honored by another.
    public async Task<IReadOnlyList<ElementHandle>> FindAllScopedAsync(Locator locator, AppSession session, Locator scope, CancellationToken ct = default)
    {
        // If the scope is identified via a Spatial locator, no provider can refresh it on its own — the whole Spatial resolution reruns instead.
        if (scope.Strategy == LocatorStrategy.Spatial)
        {
            return await RetryOnStaleScopeAsync(scope, async () =>
            {
                var scopeHandle = await FindAsync(scope, session, ct).ConfigureAwait(false);
                return await FindAllScopedAsync(locator, session, scopeHandle, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // But for the others, we can refresh the scope element and provider before searching.
        return await RetryOnStaleScopeAsync(scope, () => FindAllViaAsync(locator, provider => FindAllViaFreshScopeAsync(provider, locator, scope, session, ct), ct)).ConfigureAwait(false);
    }

    // Reruns attempt immediately on StaleElementError, up to ImplicitWaitMs, so a Locator scope that PollAsync can't refresh (FindAll, Spatial) is re-resolved rather than reported as empty.
    private async Task<T> RetryOnStaleScopeAsync<T>(Locator scope, Func<Task<T>> attempt)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return await attempt().ConfigureAwait(false);
            }
            // ReresolveOnStale=false lets the error propagate instead, for callers driving their own recovery.
            catch (StaleElementError ex) when (_options.ReresolveOnStale && stopwatch.ElapsedMilliseconds < _options.ImplicitWaitMs)
            {
                _logger?.Warn("Scope went stale mid-search, re-resolving via locator", new { scope.Strategy, scope.Value, staleId = ex.Element.Id, elapsedMs = stopwatch.ElapsedMilliseconds });
            }
        }
    }

    // ---- Tree snapshot (public) ----

    // Snapshots the tree from the first provider whose result has descendants beneath the root.
    public async Task<IReadOnlyList<ElementSnapshot>?> TrySnapshotAsync(AppSession session, CancellationToken ct = default)
    {
        IReadOnlyList<ElementSnapshot>? rootOnlyFallback = null;
        string? rootOnlyProviderName = null;

        foreach (var provider in _providers)
        {
            var snapshot = await provider.SnapshotTreeAsync(session, ct).ConfigureAwait(false);
            if (snapshot.Count == 0)
                continue;

            if (snapshot.Any(s => s.Children.Count > 0))
            {
                _logger?.Info("Snapshot taken", new { provider = provider.ProviderName, count = snapshot.Count });
                return snapshot;
            }

            // A childless root (e.g. UIA3 seeing the window but none of its content) doesn't count as success —
            // keep trying later providers for one that actually sees content, but remember this in case none do.
            rootOnlyFallback ??= snapshot;
            rootOnlyProviderName ??= provider.ProviderName;
        }

        // No provider ever found descendants — settle for the first root-only result rather than null.
        if (rootOnlyFallback is not null)
            _logger?.Info("Snapshot taken (root only — no descendants found by any provider)", new { provider = rootOnlyProviderName, count = rootOnlyFallback.Count });

        return rootOnlyFallback;
    }
}
