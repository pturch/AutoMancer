// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;

namespace AutoMancer.Testing;

// Failure-diagnostics helper shared by every Expect(...) overload that has an App to screenshot through.
internal static class ExpectDiagnostics
{
    // Saves a screenshot to a uniquely-named PNG under options.ScreenshotDirectory when CaptureScreenshotsOnFailure is set, returning the suffix to fold into a failure message
    // A capture failure is folded in too rather than masking the original assertion failure.
    public static async Task<string> DescribeScreenshotAsync(App app, ExpectOptions options, CancellationToken ct)
    {
        if (!options.CaptureScreenshotsOnFailure)
            return "";

        try
        {
            var bytes = await app.ScreenshotAsync(ct);
            var path = Path.Combine(options.ScreenshotDirectory, $"automancer-expect-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png");
            await File.WriteAllBytesAsync(path, bytes, ct);
            return $" Screenshot: {path}";
        }
        catch (Exception ex)
        {
            return $" (screenshot capture failed: {ex.Message})";
        }
    }
}
