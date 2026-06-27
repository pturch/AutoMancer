// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Core;

public sealed class XPathEvaluatorTests
{
    // Flat index layout (depth-first):
    //   0: Window "My Window"
    //   1:   Pane ""
    //   2:     Button "OK"
    //   3:     Button "Cancel"
    private static readonly IReadOnlyList<ElementSnapshot> Tree =
    [
        new("w", "My Window", null, null, "Window", default,
        [
            new("p", "", null, null, "Pane", default,
            [
                new("b1", "OK",     null, null, "Button", default, []),
                new("b2", "Cancel", null, null, "Button", default, []),
            ]),
        ]),
    ];

    [Fact]
    public void AllButtons_ReturnsAllMatchingIndices()
    {
        var result = XPathEvaluator.Evaluate("//Button", Tree);

        Assert.Equal([2, 3], result);
    }

    [Fact]
    public void ZeroBasedIndex_ReturnsFirstSiblingMatch()
    {
        // [0] is converted to [1] — XPath's first-among-siblings predicate.
        var result = XPathEvaluator.Evaluate("//Button[0]", Tree);

        Assert.Equal([2], result);
    }

    [Fact]
    public void NamePredicate_ReturnsNamedButton()
    {
        var result = XPathEvaluator.Evaluate("//Pane/Button[@Name='OK']", Tree);

        Assert.Equal([2], result);
    }

    [Fact]
    public void NoMatch_ReturnsEmpty()
    {
        var result = XPathEvaluator.Evaluate("//CheckBox", Tree);

        Assert.Empty(result);
    }

    [Fact]
    public void SecondZeroBasedIndex_ReturnsSecondSibling()
    {
        // [1] → [2] — second sibling Button.
        var result = XPathEvaluator.Evaluate("//Button[1]", Tree);

        Assert.Equal([3], result);
    }
}
