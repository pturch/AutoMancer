// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using Moq;

namespace AutoMancer.Engine.Tests.Resolver;

public sealed class ElementResolverTests
{
    private static readonly AppSession Session = AppSession.CreateForTesting(Process.GetCurrentProcess(), (IntPtr)1);
    private static readonly Locator TestLocator = Locator.ByName("Submit");

    private static ElementHandle Handle(string id) => new(id, "test", new object());

    private static Mock<IElementProvider> MockProvider(string name, ElementHandle? findResult)
    {
        var mock = new Mock<IElementProvider>();
        mock.SetupGet(p => p.ProviderName).Returns(name);
        mock.Setup(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>())).ReturnsAsync(findResult);
        mock.Setup(p => p.SnapshotTreeAsync(Session, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ElementSnapshot>());
        return mock;
    }

    [Fact]
    public async Task FindAsync_FirstProviderSucceeds_ReturnsImmediately()
    {
        var handle = Handle("1");
        var provider = MockProvider("uia3", handle);
        var resolver = new ElementResolver([provider.Object], new ElementProviderOptions { ProviderChain = ["uia3"] });

        var result = await resolver.FindAsync(TestLocator, Session);

        Assert.Same(handle, result);
        provider.Verify(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FindAsync_AllProvidersFail_ThrowsElementNotFoundError()
    {
        var provider = MockProvider("uia3", null);
        var options = new ElementProviderOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 50, PollIntervalMs = 10 };
        var resolver = new ElementResolver([provider.Object], options);

        var ex = await Assert.ThrowsAsync<ElementNotFoundError>(() => resolver.FindAsync(TestLocator, Session));

        Assert.Equal(TestLocator, ex.Locator);
        Assert.Contains("uia3", ex.AttemptedProviders);
    }

    [Fact]
    public async Task FindAsync_FirstProviderFails_FallsThroughToSecondProvider()
    {
        var handle = Handle("2");
        var failingProvider = MockProvider("uia3", null);
        var succeedingProvider = MockProvider("uia2", handle);
        var options = new ElementProviderOptions { ProviderChain = ["uia3", "uia2"] };
        var resolver = new ElementResolver([failingProvider.Object, succeedingProvider.Object], options);

        var result = await resolver.FindAsync(TestLocator, Session);

        Assert.Same(handle, result);
        failingProvider.Verify(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.Once);
        succeedingProvider.Verify(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WaitUntilGoneAsync_AllProvidersReturnNull_ResolvesImmediately()
    {
        var provider = MockProvider("uia3", null);
        var resolver = new ElementResolver([provider.Object], new ElementProviderOptions { ProviderChain = ["uia3"] });

        await resolver.WaitUntilGoneAsync(TestLocator, Session);

        provider.Verify(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WaitUntilGoneAsync_ElementDisappearsOnSecondPoll_ResolvesAfterRetry()
    {
        var provider = new Mock<IElementProvider>();
        provider.SetupGet(p => p.ProviderName).Returns("uia3");
        provider.SetupSequence(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Handle("1"))
            .ReturnsAsync((ElementHandle?)null);
        var options = new ElementProviderOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 5000, PollIntervalMs = 10 };
        var resolver = new ElementResolver([provider.Object], options);

        await resolver.WaitUntilGoneAsync(TestLocator, Session);

        provider.Verify(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task WaitUntilGoneAsync_ElementStillFoundAfterTimeout_ThrowsElementStillPresentError()
    {
        var provider = MockProvider("uia3", Handle("1"));
        var options = new ElementProviderOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 50, PollIntervalMs = 10 };
        var resolver = new ElementResolver([provider.Object], options);

        var ex = await Assert.ThrowsAsync<ElementStillPresentError>(() => resolver.WaitUntilGoneAsync(TestLocator, Session));

        Assert.Equal(TestLocator, ex.Locator);
    }

    [Fact]
    public async Task WaitUntilGoneAsync_OneProviderStillFindsElement_KeepsWaiting()
    {
        var goneProvider = MockProvider("uia3", null);
        var stillPresentProvider = MockProvider("uia2", Handle("1"));
        var options = new ElementProviderOptions { ProviderChain = ["uia3", "uia2"], ImplicitWaitMs = 30, PollIntervalMs = 10 };
        var resolver = new ElementResolver([goneProvider.Object, stillPresentProvider.Object], options);

        await Assert.ThrowsAsync<ElementStillPresentError>(() => resolver.WaitUntilGoneAsync(TestLocator, Session));

        stillPresentProvider.Verify(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task WaitForAsync_ConditionAlreadyTrue_ReturnsImmediately()
    {
        var handle = Handle("1");
        var provider = MockProvider("uia3", handle);
        var resolver = new ElementResolver([provider.Object], new ElementProviderOptions { ProviderChain = ["uia3"] });

        var result = await resolver.WaitForAsync(TestLocator, e => e.Id == "1", Session);

        Assert.Same(handle, result);
        provider.Verify(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WaitForAsync_ConditionBecomesTrueOnSecondPoll_ResolvesAfterRetry()
    {
        var stale = Handle("stale");
        var fresh = Handle("fresh");
        var provider = new Mock<IElementProvider>();
        provider.SetupGet(p => p.ProviderName).Returns("uia3");
        provider.SetupSequence(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale)
            .ReturnsAsync(fresh);
        var options = new ElementProviderOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 5000, PollIntervalMs = 10 };
        var resolver = new ElementResolver([provider.Object], options);

        var result = await resolver.WaitForAsync(TestLocator, e => e.Id == "fresh", Session);

        Assert.Same(fresh, result);
        provider.Verify(p => p.FindElementAsync(TestLocator, Session, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task WaitForAsync_ConditionNeverTrue_ThrowsElementConditionTimeoutError()
    {
        var provider = MockProvider("uia3", Handle("1"));
        var options = new ElementProviderOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 50, PollIntervalMs = 10 };
        var resolver = new ElementResolver([provider.Object], options);

        var ex = await Assert.ThrowsAsync<ElementConditionTimeoutError>(() => resolver.WaitForAsync(TestLocator, e => e.Id == "never", Session));

        Assert.Equal(TestLocator, ex.Locator);
    }

    [Fact]
    public async Task WaitForAsync_ElementNeverFound_ThrowsElementConditionTimeoutError()
    {
        var provider = MockProvider("uia3", null);
        var options = new ElementProviderOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 50, PollIntervalMs = 10 };
        var resolver = new ElementResolver([provider.Object], options);

        await Assert.ThrowsAsync<ElementConditionTimeoutError>(() => resolver.WaitForAsync(TestLocator, _ => true, Session));
    }
}
