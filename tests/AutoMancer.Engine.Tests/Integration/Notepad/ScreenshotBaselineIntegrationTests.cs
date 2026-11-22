// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Tests.Diagnostics;
using AutoMancer.Testing;
using AutoMancer.Testing.XUnit;

namespace AutoMancer.Engine.Tests.Integration;

// Covers the failure messages behind Expect().ToMatchScreenshotAsync that the record/match/drift demo tests don't reach, through the public API against live Notepad.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class ScreenshotBaselineIntegrationTests(NotepadAppFixture fixture)
    : AutoMancerTest(fixture), IClassFixture<NotepadAppFixture>, IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    // Removes the temp baseline directory.
    public void Dispose() => Directory.Delete(_directory, true);

    // Returns a baseline path in the temp directory.
    private string BaselinePath() => Path.Combine(_directory, "baseline.png");

    // Expect over a locator on this App with a short wait, so a locator that never resolves fails in under a second.
    private LocatorExpect ExpectQuickly(Locator locator) =>
        Assertions.Expect(App.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 300, PollIntervalMs = 100 }), locator, ExpectOptions);

    // A window screenshot saved to a not-yet-existing folder compares as matching right after, and as different once the document changes.
    [Fact]
    public async Task TakeScreenshotToPath_ThenCompare_MatchesUntilWindowChanges()
    {
        var path = Path.Combine(_directory, "nested", "window.png");
        var document = Locator.ByControlType("Document");
        await App.ClearAsync(document);
        await App.WaitForVisualStableAsync();

        await App.TakeScreenshotAsync(path);

        Assert.True((await App.CompareScreenshotAsync(path, maxDifferenceRatio: 0.05)).Passed);
        await App.TypeAsync(document, "Text that was not in the saved screenshot, so the window has changed.");
        var changed = await App.CompareScreenshotAsync(path, maxDifferenceRatio: 0.0);
        Assert.False(changed.Passed);
        Assert.True(changed.DifferenceRatio > 0);
        await App.ClearAsync(document);
    }

    // The element overloads save and compare just that element's pixels.
    [Fact]
    public async Task TakeScreenshotToPath_ElementOverloads_SaveAndCompare()
    {
        var path = Path.Combine(_directory, "document.png");
        var document = Locator.ByControlType("Document");
        await App.ClearAsync(document);
        await App.WaitForVisualStableAsync(document);

        await App.TakeScreenshotAsync(document, path);

        Assert.True((await App.CompareScreenshotAsync(document, path, maxDifferenceRatio: 0.05)).Passed);
    }

    // A baseline recorded at another DPI than the live capture fails with the DPI explanation, not a raw exception or a 100% diff.
    [Fact]
    public async Task WindowBaselineAtOtherDpi_FailsWithExplanation()
    {
        await File.WriteAllBytesAsync(BaselinePath(), TestPng.Gray(0x40, 77));

        var error = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect().ToMatchScreenshotAsync(BaselinePath(), timeoutMs: 1_000));

        Assert.Contains("can't be compared", error.Message);
        Assert.Contains("77 DPI", error.Message);
    }

    // An element baseline at another DPI fails the same way.
    [Fact]
    public async Task ElementBaselineAtOtherDpi_FailsWithExplanation()
    {
        await File.WriteAllBytesAsync(BaselinePath(), TestPng.Gray(0x40, 77));

        var error = await Assert.ThrowsAsync<ExpectFailedError>(() =>
            Expect(Locator.ByControlType("Document")).ToMatchScreenshotAsync(BaselinePath(), timeoutMs: 1_000));

        Assert.Contains("can't be compared", error.Message);
    }

    // With a baseline on disk, a locator that never resolves fails as an ExpectFailedError naming the missing element, not a raw ElementNotFoundError.
    [Fact]
    public async Task ElementNeverFound_WithBaseline_FailsWithExpectFailedError()
    {
        await File.WriteAllBytesAsync(BaselinePath(), TestPng.Gray(0x40));

        var error = await Assert.ThrowsAsync<ExpectFailedError>(() =>
            ExpectQuickly(Locator.ByName("NoSuchElementAnywhere")).ToMatchScreenshotAsync(BaselinePath(), timeoutMs: 500));

        Assert.Contains("no matching element", error.Message);
    }

    // With no baseline yet, a locator that never resolves fails as an ExpectFailedError and records nothing.
    [Fact]
    public async Task ElementNeverFound_NoBaseline_FailsWithoutRecording()
    {
        var error = await Assert.ThrowsAsync<ExpectFailedError>(() =>
            ExpectQuickly(Locator.ByName("NoSuchElementAnywhere")).ToMatchScreenshotAsync(BaselinePath(), timeoutMs: 500));

        Assert.Contains("no matching element", error.Message);
        Assert.False(File.Exists(BaselinePath()));
    }
}
