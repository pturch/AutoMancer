// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Testing.XUnit;

// Base class for xUnit tests driving a shared AppFixture; exposes App and Expect. Expect's failures are self-contained and need no logger.
public abstract class AutoMancerTest(AppFixture fixture)
{
    // The App instance shared by this test's xUnit collection.
    protected App App { get; } = fixture.App;

    // The logger the fixture's App was configured with, if any — set via AppOptions.Logger in CreateAppAsync.
    protected IEngineLogger? Logger => App.Logger;

    // Defaults to the process-wide AutoMancerTestOptions policy; override only for a test class that needs to diverge from it.
    protected virtual ExpectOptions ExpectOptions => new()
    {
        CaptureScreenshotsOnFailure = AutoMancerTestOptions.CaptureScreenshotsOnFailure,
        ScreenshotDirectory = AutoMancerTestOptions.ScreenshotDirectory
    };

    // One-shot assertions against an already-resolved element's snapshotted properties.
    protected static ElementExpect Expect(ElementHandle element) => Assertions.Expect(element);

    // Retry-asserting checks against a locator, polling through this test's App until the condition holds or times out.
    protected LocatorExpect Expect(Locator locator) => Assertions.Expect(App, locator, ExpectOptions);

    // Retry-asserting checks against an arbitrary async value producer, polling through this test's App until the condition holds or times out — for state that isn't a single ElementHandle property.
    protected ValueExpect<T> Expect<T>(Func<Task<T>> produce) => Assertions.Expect(App, produce, ExpectOptions);
}
