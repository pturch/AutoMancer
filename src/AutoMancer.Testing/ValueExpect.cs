// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;

namespace AutoMancer.Testing;

// Retry-asserting checks against an arbitrary async value producer — the generic form of LocatorExpect, for state that spans multiple elements or isn't exposed as a single ElementHandle property (e.g. a composite reading like an elapsed-time label). Polls through the same primitive as LocatorExpect.ToHaveValueAsync.
public sealed class ValueExpect<T>
{
    private readonly App _app;
    private readonly Func<Task<T>> _produce;
    private readonly ExpectOptions _options;

    // Constructed only via Expect(App, Func<Task<T>>, ExpectOptions).
    internal ValueExpect(App app, Func<Task<T>> produce, ExpectOptions options)
    {
        _app = app;     
        _produce = produce;
        _options = options;
    }

    // Polls until produce() satisfies predicate; throws ExpectFailedError with the last-seen value on timeout. description is folded into the failure message (e.g. "to change from lorem").
    public async Task<T> ToSatisfyAsync(Func<T, bool> predicate, string description, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
    {
        var (success, last, elapsedMs) = await Poll.UntilAsync(_produce, predicate, timeoutMs ?? _app.ImplicitWaitMs, pollIntervalMs ?? _app.PollIntervalMs, ct);
        if (success)
            return last;

        var screenshot = await ExpectDiagnostics.DescribeScreenshotAsync(_app, _options, ct);
        throw new ExpectFailedError($"Expected value {description}, but last saw \"{last}\" (elapsed {elapsedMs}ms){screenshot}");
    }

    // Polls until produce() no longer equals from; the common case of ToSatisfyAsync.
    public Task<T> ToChangeFromAsync(T from, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => ToSatisfyAsync(v => !EqualityComparer<T>.Default.Equals(v, from), $"to change from \"{from}\"", timeoutMs, pollIntervalMs, ct);

    // Polls for the full window and fails fast the moment produce() differs from the first read — proves the value stayed constant throughout, not just at two sampled instants.
    public async Task ToStayEqualAsync(int windowMs, int? pollIntervalMs = null, CancellationToken ct = default)
    {
        var effectivePollIntervalMs = pollIntervalMs ?? _app.PollIntervalMs;
        var baseline = await _produce().ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < windowMs)
        {
            var remaining = windowMs - (int)stopwatch.ElapsedMilliseconds;
            await Task.Delay(Math.Min(effectivePollIntervalMs, remaining), ct).ConfigureAwait(false);

            var current = await _produce().ConfigureAwait(false);
            if (!EqualityComparer<T>.Default.Equals(current, baseline))
            {
                var screenshot = await ExpectDiagnostics.DescribeScreenshotAsync(_app, _options, ct);
                throw new ExpectFailedError($"Expected value to stay \"{baseline}\", but it changed to \"{current}\" after {stopwatch.ElapsedMilliseconds}ms{screenshot}");
            }
        }
    }
}
