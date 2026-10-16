// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Errors;

// Thrown when a resolved element has left the UIA tree since it was found; always raised, regardless of AppOptions.ReresolveOnStale.
public sealed class StaleElementError : Exception
{
    public ElementHandle Element { get; }

    // Records the stale element, chaining the provider failure that revealed it (if any) as InnerException.
    public StaleElementError(ElementHandle element, string message, Exception? inner = null)
        // inner keeps the UIA exception's HResult/stack; null when no exception revealed it.
        : base(message, inner) => Element = element;
}
