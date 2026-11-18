// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Diagnostics;

// The settledness loop behind App.WaitForVisualStableAsync, separated from window capture so it runs against any frame source.
internal static class VisualStability
{
    // Polls capture every interval until two consecutive frames differ by at most maxDifferenceRatio of pixels; throws TimeoutException if that doesn't happen within timeout.
    public static async Task WaitAsync(Func<CancellationToken, Task<byte[]>> capture, TimeSpan interval, TimeSpan timeout, double maxDifferenceRatio, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        var previous = await capture(ct);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(interval, ct);
            var current = await capture(ct);
            if (PixelDiff.DifferenceRatio(previous, current) <= maxDifferenceRatio)
                return;
            previous = current;
        }
        throw new TimeoutException("Pixels did not become stable within the timeout.");
    }
}
