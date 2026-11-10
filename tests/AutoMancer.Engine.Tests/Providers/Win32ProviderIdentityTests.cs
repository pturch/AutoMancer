// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Providers;

// Win32Provider's element identity: two handles are the same element exactly when they hold the same window handle.
public sealed class Win32ProviderIdentityTests
{
    private static readonly IElementIdentity Identity = new Win32Provider();

    // Builds a Win32 handle around an hwnd value.
    private static ElementHandle Handle(long hwnd) => new($"hwnd:{hwnd}", "win32", (IntPtr)hwnd);

    // The same hwnd reached through two separate handles is one element.
    [Fact]
    public void IsSameElement_SameHwnd_True() => Assert.True(Identity.IsSameElement(Handle(42), Handle(42)));

    // Different hwnds are different elements.
    [Fact]
    public void IsSameElement_DifferentHwnd_False() => Assert.False(Identity.IsSameElement(Handle(42), Handle(43)));

    // A handle that isn't backed by an hwnd (another provider's, or a native menu item) is never the same as one that is.
    [Fact]
    public void IsSameElement_NonHwndHandle_False() =>
        Assert.False(Identity.IsSameElement(Handle(42), new ElementHandle("x", "uia3", new object())));
}
