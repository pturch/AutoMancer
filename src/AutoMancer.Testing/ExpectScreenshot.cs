// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Testing;

// The save-or-compare flow behind both AppExpect's and LocatorExpect's ToMatchScreenshotAsync.
internal static class ExpectScreenshot
{
    // Saves the current capture via record and fails if baselinePath is missing, else polls compare until it passes; subject names what's compared in messages and describeScreenshot yields the failure-message screenshot suffix.
    public static async Task MatchAsync(Func<Task> record, Func<Task<VisualComparison>> compare, Func<Task<string>> describeScreenshot, string subject, string baselinePath, double maxDifferenceRatio, int timeoutMs, int pollIntervalMs, CancellationToken ct)
    {
        if (!File.Exists(baselinePath))
        {
            try
            {
                await record();
            }
            catch (ElementNotFoundError)
            {
                throw new ExpectFailedError($"Expected {subject} to be recorded as a baseline at {baselinePath}, but no matching element was ever found");
            }
            throw new ExpectFailedError($"No baseline existed at {baselinePath}; recorded the current {subject} as the baseline. Review it and rerun.");
        }

        try
        {
            await ExpectPolling.PollOrFailAsync(subject, compare,
                c => c.Passed, $"to match baseline {baselinePath} within {maxDifferenceRatio:P2}",
                c => $"{c.DifferenceRatio:P2} of pixels differed", timeoutMs, pollIntervalMs, describeScreenshot, ct);
        }
        catch (BaselineDpiMismatchError ex)
        {
            throw new ExpectFailedError($"Expected {subject} to match baseline {baselinePath}, but it can't be compared: {ex.Message}");
        }
    }
}
