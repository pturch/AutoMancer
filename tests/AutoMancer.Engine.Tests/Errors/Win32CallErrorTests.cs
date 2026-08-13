// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Errors;

public sealed class Win32CallErrorTests
{
    [Fact]
    public void Constructor_SetsPropertiesAndMessage()
    {
        var ex = new Win32CallError("GetWindowRect", 1400);

        Assert.Equal("GetWindowRect", ex.Operation);
        Assert.Equal(1400, ex.Win32Error);
        Assert.Contains("GetWindowRect", ex.Message);
        Assert.Contains("1400", ex.Message);
    }
}
