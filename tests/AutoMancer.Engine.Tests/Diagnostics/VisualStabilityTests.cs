// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Tests.Diagnostics;

// Verifies the settledness loop behind WaitForVisualStableAsync against scripted frame sources.
public class VisualStabilityTests
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(5);

    // Builds a capture that returns each of grays' frames in turn, repeating the last, and counts calls.
    private static (Func<CancellationToken, Task<byte[]>> Capture, Func<int> Calls) Frames(params byte[] grays)
    {
        var calls = 0;
        return (_ => Task.FromResult(TestPng.Gray(grays[Math.Min(calls++, grays.Length - 1)])), () => calls);
    }

    // Frames that keep changing and then hold still end the wait on the first repeated frame.
    [Fact]
    public async Task WaitAsync_FramesSettle_ReturnsAfterFirstRepeat()
    {
        var (capture, calls) = Frames(0x10, 0x80, 0xF0, 0xF0);

        await VisualStability.WaitAsync(capture, Poll, TimeSpan.FromSeconds(5), 0.001, default);

        Assert.Equal(4, calls());
    }

    // Frames that never repeat end in a TimeoutException.
    [Fact]
    public async Task WaitAsync_NeverSettles_ThrowsTimeout()
    {
        var calls = 0;
        Task<byte[]> Flicker(CancellationToken _) => Task.FromResult(TestPng.Gray((byte)(calls++ % 2 == 0 ? 0x10 : 0xF0)));

        await Assert.ThrowsAsync<TimeoutException>(() => VisualStability.WaitAsync(Flicker, Poll, TimeSpan.FromMilliseconds(100), 0.001, default));
    }

    // A difference within the tolerance counts as settled even though the frames aren't identical.
    [Fact]
    public async Task WaitAsync_DifferenceWithinTolerance_Returns()
    {
        var (capture, calls) = Frames(0x10, 0xF0);

        await VisualStability.WaitAsync(capture, Poll, TimeSpan.FromSeconds(5), 1.0, default);

        Assert.Equal(2, calls());
    }
}
