// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Testing;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Deliberately-triggered assertion failures, one per failure shape, wrapped in Assert.Throws/ThrowsAsync so this stays a real
// (passing) regression test on Expect's diagnostic message shape instead of a permanently-red demonstration.
[Collection("ConsumerNotepad")]
public sealed class FailureShapeDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    // Golden path — passes without needing Assert.Throws, for contrast against the failures below.
    [Fact]
    public async Task GoldenPath_FileMenuHasExpectedName()
    {
        var fileMenu = await App.FindAsync(Locator.ByName("File"));
        Expect(fileMenu).ToHaveName("File");
    }

    // ElementExpect on a real, already-resolved handle: one-shot, no polling, no screenshot — the thrown message names both the expected and actual value.
    [Fact]
    public async Task ElementExpect_NameMismatch_ThrowsWithBothNames()
    {
        var fileMenu = await App.FindAsync(Locator.ByName("File"));

        var ex = Assert.Throws<ExpectFailedError>(() => Expect(fileMenu).ToHaveName("Filee"));

        Assert.Contains("Filee", ex.Message);
        Assert.Contains("File", ex.Message);
    }

    // LocatorExpect timeout where the element IS found, but never satisfies the condition — the thrown message names the condition and elapsed time, plus a screenshot path since TestSetup enabled CaptureScreenshotsOnFailure.
    [Fact]
    public async Task LocatorExpect_FoundButConditionNeverTrue_ThrowsWithElapsedTimeAndScreenshot()
    {
        var ex = await Assert.ThrowsAsync<ExpectFailedError>(
            () => Expect(Locator.ByControlType("TitleBar")).ToHaveTextAsync("this text will never appear"));

        Assert.Contains("this text will never appear", ex.Message);
        Assert.Contains("elapsed", ex.Message);
        Assert.Contains("Screenshot:", ex.Message);
    }

    // LocatorExpect timeout where the element is never found at all — the thrown message says so explicitly rather than describing a mismatched value.
    [Fact]
    public async Task LocatorExpect_NeverFound_ThrowsWithNoMatchFoundMessage()
    {
        var ex = await Assert.ThrowsAsync<ExpectFailedError>(
            () => Expect(Locator.ByAutomationId("ThisElementDoesNotExist")).ToBeVisibleAsync());

        Assert.Contains("no matching element was ever found", ex.Message);
        Assert.Contains("Screenshot:", ex.Message);
    }
}
