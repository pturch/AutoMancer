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

    // Polls until the located element's value (ValuePattern/TextPattern, not Name) equals expected exactly; throws ExpectFailedError on timeout. Runs its own loop rather than WaitOrFailAsync/App.WaitForAsync, since reading a value is an async operator call, not a synchronous ElementHandle check. Defaults mirror the App's own ImplicitWaitMs/PollIntervalMs (from AppOptions) unless overridden.
    public async Task ToHaveValueAsync(string expected, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
    {
        var effectiveTimeoutMs = timeoutMs ?? _app.ImplicitWaitMs;
        var effectivePollIntervalMs = pollIntervalMs ?? _app.PollIntervalMs;
        var stopwatch = Stopwatch.StartNew();
        string? actual = null;
        try
        {
            while (true)
            {
                actual = await _app.GetValueAsync(_locator, ct);
                if (actual == expected)
                    return;
                if (stopwatch.ElapsedMilliseconds >= effectiveTimeoutMs)
                    break;
                await Task.Delay(effectivePollIntervalMs, ct);
            }
        }
        catch (ElementNotFoundError)
        {
            actual = null;
        }

        var describedActual = actual is null ? "no matching element was ever found" : $"found value \"{actual}\"";
        var screenshot = _options.CaptureScreenshotsOnFailure ? await DescribeScreenshotAsync(ct) : "";
        throw new ExpectFailedError($"Expected {_locator.Strategy}={_locator.Value} to have value \"{expected}\", but {describedActual} (elapsed {stopwatch.ElapsedMilliseconds}ms){screenshot}");
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
            var screenshot = _options.CaptureScreenshotsOnFailure ? await DescribeScreenshotAsync(ct) : "";
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

    // Saves a screenshot to a uniquely-named PNG in _options.ScreenshotDirectory; a capture failure is folded into the message rather than masking the original assertion failure.
    private async Task<string> DescribeScreenshotAsync(CancellationToken ct)
    {
        try
        {
            var bytes = await _app.ScreenshotAsync(ct);
            var path = Path.Combine(_options.ScreenshotDirectory, $"automancer-expect-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png");
            await File.WriteAllBytesAsync(path, bytes, ct);
            return $" Screenshot: {path}";
        }
        catch (Exception ex)
        {
            return $" (screenshot capture failed: {ex.Message})";
        }
    }
}
