// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;

namespace AutoMancer.Testing;

// The polling loop shared by every retry-asserting Expect(...) overload — samples produce() until predicate holds or timeoutMs elapses.
internal static class Poll
{
    // Runs the sample-compare-wait loop; the returned Last value is whatever produce() last returned, whether or not predicate ever held.
    public static async Task<(bool Success, T Last, long ElapsedMs)> UntilAsync<T>(Func<Task<T>> produce, Func<T, bool> predicate, int timeoutMs, int pollIntervalMs, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var current = await produce().ConfigureAwait(false);
            if (predicate(current))
                return (true, current, stopwatch.ElapsedMilliseconds);
            if (stopwatch.ElapsedMilliseconds >= timeoutMs)
                return (false, current, stopwatch.ElapsedMilliseconds);
            await Task.Delay(pollIntervalMs, ct).ConfigureAwait(false);
        }
    }
}
