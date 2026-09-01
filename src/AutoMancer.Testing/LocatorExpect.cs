// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Testing;

// Retry-asserting checks against a Locator — polls via App.WaitForAsync instead of racing a single FindAsync.
public sealed class LocatorExpect
{
    private readonly App _app;
    private readonly Locator _locator;
    private readonly ExpectOptions _options;

    // Constructed only via Expect(App, Locator, ExpectOptions).
    internal LocatorExpect(App app, Locator locator, ExpectOptions options)
    {
        _app = app;
        _locator = locator;
        _options = options;
    }

    // Polls until the located element's Name equals expected exactly; throws ExpectFailedError on timeout.
    public Task ToHaveNameAsync(string expected, CancellationToken ct = default)
        => WaitOrFailAsync(e => e.Name == expected, $"to have name \"{expected}\"", ct);

    // Polls until the located element has a non-zero bounding rectangle; throws ExpectFailedError on timeout.
    public Task ToBeVisibleAsync(CancellationToken ct = default)
        => WaitOrFailAsync(e => e.BoundingRect.Width > 0 && e.BoundingRect.Height > 0, "to be visible", ct);

    // Polls until the located element's Name contains expected as a substring; throws ExpectFailedError on timeout.
    public Task ToHaveTextAsync(string expected, CancellationToken ct = default)
        => WaitOrFailAsync(e => e.Name?.Contains(expected, StringComparison.Ordinal) == true, $"to have text containing \"{expected}\"", ct);

    // Polls until the element's value equals expected, via Poll rather than WaitOrFailAsync since reading a value is async; throws ExpectFailedError on timeout.
    public async Task ToHaveValueAsync(string expected, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
    {
        var effectiveTimeoutMs = timeoutMs ?? _app.ImplicitWaitMs;
        var effectivePollIntervalMs = pollIntervalMs ?? _app.PollIntervalMs;
        var stopwatch = Stopwatch.StartNew();
        bool success;
        string? actual;
        long elapsedMs;
        try
        {
            (success, actual, elapsedMs) = await Poll.UntilAsync(() => _app.GetValueAsync(_locator, ct), v => v == expected, effectiveTimeoutMs, effectivePollIntervalMs, ct);
        }
        catch (ElementNotFoundError)
        {
            (success, actual, elapsedMs) = (false, null, stopwatch.ElapsedMilliseconds);
        }

        if (success)
            return;

        var describedActual = actual is null ? "no matching element was ever found" : $"found value \"{actual}\"";
        var screenshot = await ExpectDiagnostics.DescribeScreenshotAsync(_app, _options, ct);
        throw new ExpectFailedError($"Expected {_locator.Strategy}={_locator.Value} to have value \"{expected}\", but {describedActual} (elapsed {elapsedMs}ms){screenshot}");
    }

    // Delegates to App.WaitForAsync and rewraps a timeout as ExpectFailedError carrying what was actually found, self-contained with no logger required.
    private async Task WaitOrFailAsync(Func<ElementHandle, bool> condition, string description, CancellationToken ct)
    {
        try
        {
            await _app.WaitForAsync(_locator, condition, ct);
        }
        catch (ElementConditionTimeoutError ex)
        {
            var actual = await DescribeActualAsync(ct);
            var screenshot = await ExpectDiagnostics.DescribeScreenshotAsync(_app, _options, ct);
            throw new ExpectFailedError($"Expected {_locator.Strategy}={_locator.Value} {description}, but {actual} (elapsed {ex.ElapsedMs}ms){screenshot}");
        }
    }

    // Single-pass (no retry) lookup of the locator's current match, for a failure message that shows what was actually there.
    private async Task<string> DescribeActualAsync(CancellationToken ct)
    {
        var matches = await _app.FindAllAsync(_locator, ct);
        if (matches.Count == 0)
            return "no matching element was ever found";
        return $"found Name=\"{matches[0].Name}\", BoundingRect={matches[0].BoundingRect}";
    }
}
