// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Testing;

// Failure-diagnostics policy for a single Expect(App, Locator) call.
public sealed class ExpectOptions
{
    // When true, a failed assertion saves a screenshot to ScreenshotDirectory and folds the path into the failure message.
    public bool CaptureScreenshotsOnFailure { get; init; } = false;

    // Directory failure screenshots are written to when CaptureScreenshotsOnFailure is true.
    public string ScreenshotDirectory { get; init; } = Path.GetTempPath();

    // Sensible defaults for ad-hoc use outside AutoMancerTest.
    public static ExpectOptions Default { get; } = new();
}
