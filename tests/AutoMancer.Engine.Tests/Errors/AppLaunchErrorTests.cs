// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Errors;

public sealed class AppLaunchErrorTests
{
    [Fact]
    public void Constructor_MessageOnly_SetsMessage()
    {
        var ex = new AppLaunchError("calc.exe exited before a window appeared.");

        Assert.Equal("calc.exe exited before a window appeared.", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsBoth()
    {
        var inner = new InvalidOperationException("boom");

        var ex = new AppLaunchError("Launch failed.", inner);

        Assert.Equal("Launch failed.", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
