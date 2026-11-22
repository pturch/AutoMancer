// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Testing;

// The poll-then-fail flow shared by the Expect classes that assert on a live async read.
internal static class ExpectPolling
{
    // Polls read until accept holds; on timeout throws ExpectFailedError with describeLast's account of the last read, or "no matching element was ever found" if the locator never resolved; describeScreenshot yields the failure-message screenshot suffix.
    public static async Task PollOrFailAsync<T>(string subject, Func<Task<T>> read, Func<T, bool> accept, string description, Func<T, string> describeLast, int timeoutMs, int pollIntervalMs, Func<Task<string>> describeScreenshot, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        string describedActual;
        long elapsedMs;
        try
        {
            var (success, last, pollElapsedMs) = await Poll.UntilAsync(read, accept, timeoutMs, pollIntervalMs, ct);
            if (success)
                return;
            (describedActual, elapsedMs) = (describeLast(last), pollElapsedMs);
        }
        catch (ElementNotFoundError)
        {
            (describedActual, elapsedMs) = ("no matching element was ever found", stopwatch.ElapsedMilliseconds);
        }

        var screenshot = await describeScreenshot();
        throw new ExpectFailedError($"Expected {subject} {description}, but {describedActual} (elapsed {elapsedMs}ms){screenshot}");
    }
}
