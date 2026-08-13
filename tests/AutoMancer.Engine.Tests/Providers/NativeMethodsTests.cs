// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Providers;

public sealed class NativeMethodsTests
{
    [Fact]
    public void ThrowIfFailed_Success_DoesNotThrow()
        => true.ThrowIfFailed("SomeOperation");

    [Fact]
    public void ThrowIfFailed_Failure_ThrowsWin32CallErrorNamingOperation()
    {
        var ex = Assert.Throws<Win32CallError>(() => false.ThrowIfFailed("SomeOperation"));

        Assert.Equal("SomeOperation", ex.Operation);
    }
}
