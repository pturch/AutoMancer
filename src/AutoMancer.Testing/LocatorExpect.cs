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

    // Polls until the located element is visible by the same rule actions wait on (WaitConditions.IsVisible); throws ExpectFailedError on timeout.
    public Task ToBeVisibleAsync(CancellationToken ct = default)
        => WaitOrFailAsync(WaitConditions.IsVisible(), "to be visible", ct);

    // Polls until the located element is enabled by the same rule actions wait on (WaitConditions.IsEnabled); throws ExpectFailedError on timeout.
    public Task ToBeEnabledAsync(CancellationToken ct = default)
        => WaitOrFailAsync(WaitConditions.IsEnabled(), "to be enabled", ct);

    // Polls until the located element's Name contains substring; throws ExpectFailedError on timeout.
    public Task ToContainTextAsync(string substring, CancellationToken ct = default)
        => WaitOrFailAsync(WaitConditions.NameContains(substring), $"to contain text \"{substring}\"", ct);

    // Polls until the located element's Name contains expected as a substring; throws ExpectFailedError on timeout.
    public Task ToHaveTextAsync(string expected, CancellationToken ct = default)
        => WaitOrFailAsync(e => e.Name?.Contains(expected, StringComparison.Ordinal) == true, $"to have text containing \"{expected}\"", ct);

    // Polls until the element's value equals expected, via Poll rather than WaitOrFailAsync since reading a value is async; throws ExpectFailedError on timeout.
    public Task ToHaveValueAsync(string expected, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollOrFailAsync(() => _app.GetValueAsync(_locator, ct), v => v == expected, $"to have value \"{expected}\"",
            v => v is null ? "no matching element was ever found" : $"found value \"{v}\"", timeoutMs, pollIntervalMs, ct);

    // Polls single-pass FindAllAsync until exactly expected elements match — the wait FindAllAsync itself leaves out; throws ExpectFailedError reporting the last count on timeout.
    public Task ToHaveCountAsync(int expected, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollOrFailAsync(() => _app.FindAllAsync(_locator, ct), m => m.Count == expected, $"to have count {expected}",
            m => $"found {m.Count}", timeoutMs, pollIntervalMs, ct);

    // Polls App.IsUnobscuredAsync until a click at the element's center would reach it — polled because overlays animate in and out, and Chromium's first hit-test after a change can be stale; throws ExpectFailedError on timeout.
    public Task ToBeUnobscuredAsync(int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollOrFailAsync(() => _app.IsUnobscuredAsync(_locator, ct), clear => clear, "to be unobscured",
            _ => "it was still covered or had no on-screen bounds", timeoutMs, pollIntervalMs, ct);

    // Polls App.IsUnobscuredAsync until something covers the element's center (or it has no on-screen bounds); throws ExpectFailedError on timeout.
    public Task ToBeObscuredAsync(int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)
        => PollOrFailAsync(() => _app.IsUnobscuredAsync(_locator, ct), clear => !clear, "to be obscured",
            _ => "it was still clear", timeoutMs, pollIntervalMs, ct);

    // Polls a live async read until accept holds, for assertions WaitOrFailAsync can't express; on timeout throws ExpectFailedError with describeLast's account of the last read, or "no matching element was ever found" if the locator never resolved.
    private async Task PollOrFailAsync<T>(Func<Task<T>> read, Func<T, bool> accept, string description, Func<T, string> describeLast, int? timeoutMs, int? pollIntervalMs, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        string describedActual;
        long elapsedMs;
        try
        {
            var (success, last, pollElapsedMs) = await Poll.UntilAsync(read, accept, timeoutMs ?? _app.ImplicitWaitMs, pollIntervalMs ?? _app.PollIntervalMs, ct);
            if (success)
                return;
            (describedActual, elapsedMs) = (describeLast(last), pollElapsedMs);
        }
        catch (ElementNotFoundError)
        {
            (describedActual, elapsedMs) = ("no matching element was ever found", stopwatch.ElapsedMilliseconds);
        }

        var screenshot = await ExpectDiagnostics.DescribeScreenshotAsync(_app, _options, ct);
        throw new ExpectFailedError($"Expected {_locator.Strategy}={_locator.Value} {description}, but {describedActual} (elapsed {elapsedMs}ms){screenshot}");
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
        var m = matches[0];
        return $"found Name=\"{m.Name}\", BoundingRect={m.BoundingRect}, IsEnabled={m.IsEnabled?.ToString() ?? "unknown"}, IsOffscreen={m.IsOffscreen?.ToString() ?? "unknown"}";
    }
}
