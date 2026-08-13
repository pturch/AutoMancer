// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Testing;
using Moq;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.TestingAdapter;

// Covers LocatorExpect's retry/timeout/failure-message logic against a fake provider — the coverage roadmap batch 1.9.6 called for, previously only exercised by one live-Notepad integration test.
public sealed class LocatorExpectTests
{
    private static readonly AppSession Session = AppSession.CreateForTesting(Process.GetCurrentProcess(), (IntPtr)1);
    private static readonly Locator TestLocator = Locator.ByName("Submit");

    private static ElementHandle Handle(string? name = null, Rect rect = default, IElementOperator? op = null) =>
        new ElementHandle("1", "test", new object()) { Name = name, BoundingRect = rect, Operator = op };

    // Builds an App over a single fake provider named "uia3" that always returns findResult for TestLocator.
    private static App FakeApp(ElementHandle? findResult, int implicitWaitMs = 50, int pollIntervalMs = 10)
    {
        var provider = new Mock<IElementProvider>();
        provider.SetupGet(p => p.ProviderName).Returns("uia3");
        provider.Setup(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>())).ReturnsAsync(findResult);
        provider.Setup(p => p.FindElementsAsync(TestLocator, Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(findResult is null ? Array.Empty<ElementHandle>() : [findResult]);
        provider.Setup(p => p.SnapshotTreeAsync(Session, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ElementSnapshot>());
        var options = new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = implicitWaitMs, PollIntervalMs = pollIntervalMs, Logger = null };
        return App.CreateForTesting(Session, [provider.Object], options);
    }

    [Fact]
    public async Task ToHaveNameAsync_MatchingName_DoesNotThrow()
    {
        var app = FakeApp(Handle(name: "Submit"));

        await Expect(app, TestLocator).ToHaveNameAsync("Submit");
    }

    [Fact]
    public async Task ToHaveNameAsync_ConditionBecomesTrueOnSecondPoll_ResolvesAfterRetry()
    {
        var provider = new Mock<IElementProvider>();
        provider.SetupGet(p => p.ProviderName).Returns("uia3");
        provider.SetupSequence(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Handle(name: "Loading..."))
            .ReturnsAsync(Handle(name: "Submit"));
        var options = new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 5_000, PollIntervalMs = 10, Logger = null };
        var app = App.CreateForTesting(Session, [provider.Object], options);

        await Expect(app, TestLocator).ToHaveNameAsync("Submit");

        provider.Verify(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task ToHaveNameAsync_ElementFoundButNeverMatches_ThrowsExpectFailedErrorDescribingActualElement()
    {
        var app = FakeApp(Handle(name: "Cancel", rect: new Rect(0, 0, 10, 10)));

        var ex = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToHaveNameAsync("Submit"));

        Assert.Contains("to have name \"Submit\"", ex.Message);
        Assert.Contains("found Name=\"Cancel\"", ex.Message);
    }

    [Fact]
    public async Task ToHaveNameAsync_ElementNeverFound_ThrowsExpectFailedErrorSayingNoMatchingElement()
    {
        var app = FakeApp(findResult: null);

        var ex = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToHaveNameAsync("Submit"));

        Assert.Contains("no matching element was ever found", ex.Message);
    }

    [Fact]
    public async Task ToBeVisibleAsync_NonZeroRect_DoesNotThrow()
    {
        var app = FakeApp(Handle(rect: new Rect(0, 0, 10, 10)));

        await Expect(app, TestLocator).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ToBeVisibleAsync_ZeroRect_ThrowsExpectFailedError()
    {
        var app = FakeApp(Handle(rect: default));

        await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToBeVisibleAsync());
    }

    [Fact]
    public async Task ToHaveTextAsync_ContainsSubstring_DoesNotThrow()
    {
        var app = FakeApp(Handle(name: "Hello from AutoMancer."));

        await Expect(app, TestLocator).ToHaveTextAsync("Hello from AutoMancer");
    }

    [Fact]
    public async Task ToHaveTextAsync_MissingSubstring_ThrowsExpectFailedError()
    {
        var app = FakeApp(Handle(name: "Goodbye"));

        await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToHaveTextAsync("Hello"));
    }

    [Fact]
    public async Task ToHaveValueAsync_MatchingValue_DoesNotThrow()
    {
        var op = new Mock<IElementOperator>();
        op.Setup(o => o.TryGetValueAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>())).ReturnsAsync("expected value");
        var app = FakeApp(Handle(op: op.Object));

        await Expect(app, TestLocator).ToHaveValueAsync("expected value");
    }

    [Fact]
    public async Task ToHaveValueAsync_MismatchedValue_ThrowsExpectFailedErrorDescribingActualValue()
    {
        var op = new Mock<IElementOperator>();
        op.Setup(o => o.TryGetValueAsync(It.IsAny<ElementHandle>(), It.IsAny<CancellationToken>())).ReturnsAsync("actual value");
        var app = FakeApp(Handle(op: op.Object));

        var ex = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToHaveValueAsync("expected value", timeoutMs: 50, pollIntervalMs: 10));

        Assert.Contains("to have value \"expected value\"", ex.Message);
        Assert.Contains("found value \"actual value\"", ex.Message);
    }

    [Fact]
    public async Task ToHaveValueAsync_ElementNeverFound_ThrowsExpectFailedErrorSwallowingElementNotFoundError()
    {
        var app = FakeApp(findResult: null, implicitWaitMs: 30, pollIntervalMs: 10);

        var ex = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToHaveValueAsync("expected value", timeoutMs: 30, pollIntervalMs: 10));

        Assert.Contains("no matching element was ever found", ex.Message);
    }

    [Fact]
    public async Task Expect_CaptureScreenshotsOnFailureDisabled_MessageOmitsScreenshotText()
    {
        var app = FakeApp(findResult: null);

        var ex = await Assert.ThrowsAsync<ExpectFailedError>(
            () => Expect(app, TestLocator, new ExpectOptions { CaptureScreenshotsOnFailure = false }).ToHaveNameAsync("Submit"));

        Assert.DoesNotContain("Screenshot:", ex.Message);
        Assert.DoesNotContain("screenshot capture failed", ex.Message);
    }
}
