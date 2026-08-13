// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Testing;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.TestingAdapter;

public sealed class ElementExpectTests
{
    private static ElementHandle Handle(string? name = null, Rect rect = default) =>
        new ElementHandle("1", "test", new object()) { Name = name, BoundingRect = rect };

    [Fact]
    public void ToHaveName_MatchingName_DoesNotThrow()
    {
        Expect(Handle(name: "File")).ToHaveName("File");
    }

    [Fact]
    public void ToHaveName_MismatchedName_ThrowsExpectFailedError()
    {
        var ex = Assert.Throws<ExpectFailedError>(() => Expect(Handle(name: "Vile")).ToHaveName("File"));

        Assert.Contains("File", ex.Message);
        Assert.Contains("Vile", ex.Message);
    }

    [Fact]
    public void ToBeVisible_NonZeroRect_DoesNotThrow()
    {
        Expect(Handle(rect: new Rect(0, 0, 10, 10))).ToBeVisible();
    }

    [Fact]
    public void ToBeVisible_ZeroRect_ThrowsExpectFailedError()
    {
        Assert.Throws<ExpectFailedError>(() => Expect(Handle(rect: default)).ToBeVisible());
    }

    [Fact]
    public void ToHaveText_ContainsSubstring_DoesNotThrow()
    {
        Expect(Handle(name: "Hello from AutoMancer.")).ToHaveText("Hello from AutoMancer");
    }

    [Fact]
    public void ToHaveText_MissingSubstring_ThrowsExpectFailedError()
    {
        Assert.Throws<ExpectFailedError>(() => Expect(Handle(name: "Goodbye")).ToHaveText("Hello"));
    }
}
