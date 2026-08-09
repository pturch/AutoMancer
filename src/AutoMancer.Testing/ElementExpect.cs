// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Testing;

// One-shot assertions against an already-resolved ElementHandle's snapshotted properties; no retry.
public sealed class ElementExpect
{
    private readonly ElementHandle _element;

    // Constructed only via Expect(ElementHandle).
    internal ElementExpect(ElementHandle element) => _element = element;

    // Asserts the element's Name equals expected exactly.
    public void ToHaveName(string expected)
    {
        if (_element.Name != expected)
            throw new ExpectFailedError($"Expected name \"{expected}\" but was \"{_element.Name}\"");
    }

    // Asserts the element has a non-zero bounding rectangle.
    public void ToBeVisible()
    {
        if (_element.BoundingRect.Width <= 0 || _element.BoundingRect.Height <= 0)
            throw new ExpectFailedError($"Expected element to be visible but its bounding rect was {_element.BoundingRect}");
    }

    // Asserts the element's Name contains expected as a substring.
    public void ToHaveText(string expected)
    {
        if (_element.Name is null || !_element.Name.Contains(expected, StringComparison.Ordinal))
            throw new ExpectFailedError($"Expected text containing \"{expected}\" but Name was \"{_element.Name}\"");
    }
}
