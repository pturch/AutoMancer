// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Testing;

// Entry points for the retry-asserting API; import via 'using static AutoMancer.Testing.Assertions' for bare Expect(...) syntax.
public static class Assertions
{
    // Wraps an already-resolved element for one-shot assertions against its snapshotted properties.
    public static ElementExpect Expect(ElementHandle element) => new(element);

    // Wraps a locator for retry-asserting checks that poll through app until the condition holds or times out; options controls failure-diagnostics behavior.
    public static LocatorExpect Expect(App app, Locator locator, ExpectOptions? options = null) => new(app, locator, options ?? ExpectOptions.Default);

    // Wraps an arbitrary async value producer for retry-asserting checks — the generic form of Expect(App, Locator), for state that isn't a single ElementHandle property.
    public static ValueExpect<T> Expect<T>(App app, Func<Task<T>> produce, ExpectOptions? options = null) => new(app, produce, options ?? ExpectOptions.Default);
}
