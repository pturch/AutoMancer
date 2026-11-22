// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;

namespace AutoMancer.Testing;

// Retry-asserting checks against the app window as a whole rather than one element.
public sealed class AppExpect
{
    private readonly App _app;
    private readonly ExpectOptions _options;

    // Constructed only via Expect(App, ExpectOptions).
    internal AppExpect(App app, ExpectOptions options)
    {
        _app = app;
        _options = options;
    }

    // Polls until the window's pixels match the baseline PNG within maxDifferenceRatio; a missing baseline is recorded from the current capture and fails so it gets reviewed. Throws ExpectFailedError on timeout.
    public Task ToMatchScreenshotAsync(string baselinePath, double maxDifferenceRatio = 0.01, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => ExpectScreenshot.MatchAsync(
            () => _app.TakeScreenshotAsync(baselinePath, ct),
            () => _app.CompareScreenshotAsync(baselinePath, maxDifferenceRatio, ct),
            () => ExpectDiagnostics.DescribeScreenshotAsync(_app, _options, ct),
            "window", baselinePath, maxDifferenceRatio, timeoutMs ?? _app.ImplicitWaitMs, pollIntervalMs ?? _app.PollIntervalMs, ct);
}
