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

    private static ElementHandle Handle(string? name = null, Rect rect = default, IElementOperator? op = null, bool? isEnabled = null, bool? isOffscreen = null) =>
        new ElementHandle("1", "test", new object()) { Name = name, BoundingRect = rect, Operator = op, IsEnabled = isEnabled, IsOffscreen = isOffscreen };

    // Builds an App over a single fake provider named "uia3" whose FindElementsAsync for TestLocator returns each of counts' sizes in turn, repeating the last.
    private static (App App, Mock<IElementProvider> Provider) FakeAppWithCounts(params int[] counts)
    {
        var provider = new Mock<IElementProvider>();
        provider.SetupGet(p => p.ProviderName).Returns("uia3");
        var call = 0;
        provider.Setup(p => p.FindElementsAsync(TestLocator, Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Enumerable.Range(0, counts[Math.Min(call++, counts.Length - 1)]).Select(i => Handle(name: $"Item {i}")).ToList());
        provider.Setup(p => p.SnapshotTreeAsync(Session, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ElementSnapshot>());
        var options = new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 50, PollIntervalMs = 10, Logger = null };
        return (App.CreateForTesting(Session, [provider.Object], options), provider);
    }

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
    public async Task ToBeVisibleAsync_NonZeroRectButOffscreen_ThrowsExpectFailedError()
    {
        var app = FakeApp(Handle(rect: new Rect(0, 0, 10, 10), isOffscreen: true));

        var ex = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToBeVisibleAsync());

        Assert.Contains("IsOffscreen=True", ex.Message);
    }

    [Fact]
    public async Task ToBeEnabledAsync_Enabled_DoesNotThrow()
    {
        var app = FakeApp(Handle(isEnabled: true));

        await Expect(app, TestLocator).ToBeEnabledAsync();
    }

    [Fact]
    public async Task ToBeEnabledAsync_EnabledStateUnknown_DoesNotThrow()
    {
        var app = FakeApp(Handle(isEnabled: null));

        await Expect(app, TestLocator).ToBeEnabledAsync();
    }

    [Fact]
    public async Task ToBeEnabledAsync_ConditionBecomesTrueOnSecondPoll_ResolvesAfterRetry()
    {
        var provider = new Mock<IElementProvider>();
        provider.SetupGet(p => p.ProviderName).Returns("uia3");
        provider.SetupSequence(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Handle(isEnabled: false))
            .ReturnsAsync(Handle(isEnabled: true));
        var options = new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 5_000, PollIntervalMs = 10, Logger = null };
        var app = App.CreateForTesting(Session, [provider.Object], options);

        await Expect(app, TestLocator).ToBeEnabledAsync();

        provider.Verify(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task ToBeEnabledAsync_NeverEnabled_ThrowsExpectFailedErrorDescribingActualElement()
    {
        var app = FakeApp(Handle(name: "Submit", isEnabled: false));

        var ex = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToBeEnabledAsync());

        Assert.Contains("to be enabled", ex.Message);
        Assert.Contains("IsEnabled=False", ex.Message);
    }

    [Fact]
    public async Task ToContainTextAsync_ContainsSubstring_DoesNotThrow()
    {
        var app = FakeApp(Handle(name: "Hello from AutoMancer."));

        await Expect(app, TestLocator).ToContainTextAsync("from Auto");
    }

    [Fact]
    public async Task ToContainTextAsync_ConditionBecomesTrueOnSecondPoll_ResolvesAfterRetry()
    {
        var provider = new Mock<IElementProvider>();
        provider.SetupGet(p => p.ProviderName).Returns("uia3");
        provider.SetupSequence(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Handle(name: "Loading..."))
            .ReturnsAsync(Handle(name: "3 results found"));
        var options = new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 5_000, PollIntervalMs = 10, Logger = null };
        var app = App.CreateForTesting(Session, [provider.Object], options);

        await Expect(app, TestLocator).ToContainTextAsync("results");

        provider.Verify(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task ToContainTextAsync_MissingSubstring_ThrowsExpectFailedErrorDescribingActualElement()
    {
        var app = FakeApp(Handle(name: "Goodbye"));

        var ex = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToContainTextAsync("Hello"));

        Assert.Contains("to contain text \"Hello\"", ex.Message);
        Assert.Contains("found Name=\"Goodbye\"", ex.Message);
    }

    [Fact]
    public async Task ToHaveCountAsync_MatchingCount_DoesNotThrow()
    {
        var (app, _) = FakeAppWithCounts(3);

        await Expect(app, TestLocator).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task ToHaveCountAsync_ZeroExpectedAndNoMatches_DoesNotThrow()
    {
        var (app, _) = FakeAppWithCounts(0);

        await Expect(app, TestLocator).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ToHaveCountAsync_CollectionStillPopulating_ResolvesAfterRetry()
    {
        var (app, provider) = FakeAppWithCounts(0, 2, 5);

        await Expect(app, TestLocator).ToHaveCountAsync(5, timeoutMs: 5_000, pollIntervalMs: 10);

        provider.Verify(p => p.FindElementsAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ToHaveCountAsync_CountNeverReached_ThrowsExpectFailedErrorReportingLastCount()
    {
        var (app, _) = FakeAppWithCounts(1, 2);

        var ex = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToHaveCountAsync(3, timeoutMs: 50, pollIntervalMs: 10));

        Assert.Contains("to have count 3", ex.Message);
        Assert.Contains("found 2", ex.Message);
    }

    // An element with no on-screen bounds has nothing to hit, which IsUnobscuredAsync reports as false without touching the live desktop.
    [Fact]
    public async Task ToBeObscuredAsync_NoOnScreenBounds_DoesNotThrow()
    {
        var app = FakeApp(Handle(name: "Submit", rect: default));

        await Expect(app, TestLocator).ToBeObscuredAsync();
    }

    [Fact]
    public async Task ToBeUnobscuredAsync_NoOnScreenBounds_ThrowsExpectFailedError()
    {
        var app = FakeApp(Handle(name: "Submit", rect: default));

        var ex = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToBeUnobscuredAsync());

        Assert.Contains("to be unobscured", ex.Message);
        Assert.Contains("still covered or had no on-screen bounds", ex.Message);
    }

    [Fact]
    public async Task ToBeUnobscuredAsync_ElementNeverFound_ThrowsExpectFailedErrorSayingNoMatchingElement()
    {
        var app = FakeApp(findResult: null);

        var ex = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app, TestLocator).ToBeUnobscuredAsync());

        Assert.Contains("no matching element was ever found", ex.Message);
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
