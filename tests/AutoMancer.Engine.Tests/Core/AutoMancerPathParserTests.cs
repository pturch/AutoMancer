// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Core;

public sealed class AutoMancerPathParserTests
{
    [Fact]
    public void SimpleType_ParsesControlType()
    {
        var segments = AutoMancerPathParser.Parse("Window");

        Assert.Single(segments);
        Assert.Equal("Window", segments[0].ControlType);
        Assert.Null(segments[0].Name);
        Assert.Null(segments[0].Index);
    }

    [Fact]
    public void TypeWithName_ParsesName()
    {
        var segments = AutoMancerPathParser.Parse("Button[\"OK\"]");

        Assert.Single(segments);
        Assert.Equal("Button", segments[0].ControlType);
        Assert.Equal("OK", segments[0].Name);
        Assert.Null(segments[0].Index);
    }

    [Fact]
    public void TypeWithIndex_ParsesIndex()
    {
        var segments = AutoMancerPathParser.Parse("Pane[2]");

        Assert.Single(segments);
        Assert.Equal("Pane", segments[0].ControlType);
        Assert.Null(segments[0].Name);
        Assert.Equal(2, segments[0].Index);
    }

    [Fact]
    public void MultiSegment_ParsesAll()
    {
        var segments = AutoMancerPathParser.Parse("Window > Pane[2] > Button[\"Submit\"]");

        Assert.Equal(3, segments.Count);
        Assert.Equal("Window", segments[0].ControlType);
        Assert.Null(segments[0].Name);
        Assert.Null(segments[0].Index);
        Assert.Equal("Pane", segments[1].ControlType);
        Assert.Equal(2, segments[1].Index);
        Assert.Null(segments[1].Name);
        Assert.Equal("Button", segments[2].ControlType);
        Assert.Equal("Submit", segments[2].Name);
        Assert.Null(segments[2].Index);
    }

    [Fact]
    public void Wildcard_ParsesAsWildcardType()
    {
        var segments = AutoMancerPathParser.Parse("Window > *");

        Assert.Equal(2, segments.Count);
        Assert.Equal("*", segments[1].ControlType);
        Assert.Null(segments[1].Name);
        Assert.Null(segments[1].Index);
    }
}
