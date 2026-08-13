// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Errors;

public sealed class WindowMinimizedErrorTests
{
    [Fact]
    public void Constructor_SetsWindowHandleAndMessage()
    {
        var handle = (IntPtr)12345;

        var ex = new WindowMinimizedError(handle);

        Assert.Equal(handle, ex.WindowHandle);
        Assert.Contains("minimized", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("12345", ex.Message);
    }
}
