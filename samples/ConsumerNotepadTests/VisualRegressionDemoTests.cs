// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Testing;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Shows WaitForVisualStableAsync (wait for the window to stop changing instead of a fixed delay) and the pixel-baseline assertion.
[Collection("ConsumerNotepad")]
public sealed class VisualRegressionDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    // Opens the File menu and waits for its open animation to finish; afterwards the window is pixel-stable, which the test proves with two further captures.
    [Fact]
    public async Task WaitForVisualStableAsync_AfterOpeningMenu_WindowIsStable()
    {
        await App.ClickAsync(Locator.ByXPath("//MenuItem[@Name='File']"));

        await App.WaitForVisualStableAsync();

        var first = await App.TakeScreenshotAsync();
        var second = await App.TakeScreenshotAsync();
        Assert.True(PixelDiff.DifferenceRatio(first, second) <= 0.001);
        await App.PressKeyAsync(Key.Escape);
    }

    // Scoped to one element: waits only on the Document's pixels, so activity elsewhere in the window can't keep it waiting; afterwards the Document is stable by its own captures.
    [Fact]
    public async Task WaitForVisualStableAsync_OnElement_WaitsOnlyOnThatRegion()
    {
        var document = Locator.ByControlType("Document");
        await App.TypeAsync(document, "Settled text");

        await App.WaitForVisualStableAsync(document);

        var first = await App.TakeScreenshotAsync(document);
        var second = await App.TakeScreenshotAsync(document);
        Assert.True(PixelDiff.DifferenceRatio(first, second) <= 0.001);
        await App.ClearAsync(document);
    }

    // A window that never settles (an impossible tolerance stands in for endless animation) ends in a TimeoutException rather than hanging.
    [Fact]
    public async Task WaitForVisualStableAsync_NeverSettles_TimesOut()
    {
        await Assert.ThrowsAsync<TimeoutException>(() =>
            App.WaitForVisualStableAsync(timeout: TimeSpan.FromMilliseconds(500), maxDifferenceRatio: -1));
    }

    // The baseline workflow: the first run records the window and fails so you review it, a rerun matches, and typing into the document drifts the window past a tight threshold.
    [Fact]
    public async Task ToMatchScreenshotAsync_RecordsMatchesThenCatchesDrift()
    {
        var document = Locator.ByControlType("Document");
        await App.ClearAsync(document);
        await App.WaitForVisualStableAsync();
        var baseline = Path.Combine(Path.GetTempPath(), $"automancer-demo-baseline-{Guid.NewGuid():N}.png");
        try
        {
            await Assert.ThrowsAsync<ExpectFailedError>(() => Expect().ToMatchScreenshotAsync(baseline));
            Assert.True(File.Exists(baseline));

            await Expect().ToMatchScreenshotAsync(baseline);

            await App.TypeAsync(document, "This line was not in the baseline, so the window has drifted from it.");
            var drift = await Assert.ThrowsAsync<ExpectFailedError>(() =>
                Expect().ToMatchScreenshotAsync(baseline, maxDifferenceRatio: 0.0001, timeoutMs: 1_000));
            Assert.Contains("of pixels differed", drift.Message);
        }
        finally
        {
            File.Delete(baseline);
            await App.ClearAsync(document);
        }
    }

    // The same workflow scoped to one element: only the Document's pixels are compared, so a change elsewhere in the window (the tab title gaining an asterisk) can't fail it, while a change inside the Document does.
    [Fact]
    public async Task ToMatchScreenshotAsync_OnElement_IgnoresTheRestOfTheWindow()
    {
        var document = Locator.ByControlType("Document");
        await App.ClearAsync(document);
        await App.WaitForVisualStableAsync();
        var baseline = Path.Combine(Path.GetTempPath(), $"automancer-demo-element-baseline-{Guid.NewGuid():N}.png");
        try
        {
            await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(document).ToMatchScreenshotAsync(baseline));

            await Expect(document).ToMatchScreenshotAsync(baseline);

            await App.TypeAsync(document, "This line was not in the baseline, so the Document has drifted from it.");
            var drift = await Assert.ThrowsAsync<ExpectFailedError>(() =>
                Expect(document).ToMatchScreenshotAsync(baseline, maxDifferenceRatio: 0.0001, timeoutMs: 1_000));
            Assert.Contains("of pixels differed", drift.Message);
        }
        finally
        {
            File.Delete(baseline);
            await App.ClearAsync(document);
        }
    }
}
