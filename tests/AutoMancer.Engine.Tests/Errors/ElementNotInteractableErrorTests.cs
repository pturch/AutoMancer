// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Errors;

public sealed class ElementNotInteractableErrorTests
{
    private static ElementHandle Handle() => new("1", "test", new object()) { Name = "Save" };

    [Fact]
    public void Constructor_MessageOnly_SetsElementAndMessage()
    {
        var element = Handle();

        var ex = new ElementNotInteractableError(element, "disabled");

        Assert.Same(element, ex.Element);
        Assert.Equal("disabled", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsBoth()
    {
        var element = Handle();
        var inner = new InvalidOperationException("boom");

        var ex = new ElementNotInteractableError(element, "disabled", inner);

        Assert.Same(element, ex.Element);
        Assert.Same(inner, ex.InnerException);
    }
}
