// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Errors;

public sealed class ElementNotInteractableError : Exception
{
    public ElementHandle Element { get; }

    // Thrown by action classes when an element is found but cannot accept the requested interaction.
    public ElementNotInteractableError(ElementHandle element, string message)
        : base(message) => Element = element;

    public ElementNotInteractableError(ElementHandle element, string message, Exception inner)
        : base(message, inner) => Element = element;
}
