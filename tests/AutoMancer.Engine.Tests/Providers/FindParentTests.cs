// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Providers;

// FindParentAsync follows the providers' never-throw-on-not-found rule: a handle another provider made, or the top of a tree, gives null rather than an exception; no live window needed.
public sealed class FindParentTests
{
    private static readonly AppSession Session = AppSession.CreateForTesting(Process.GetCurrentProcess(), (IntPtr)42);

    // Stands in for "this element came from a different provider": its NativeHandle is neither a COM element nor an hwnd.
    private static ElementHandle ForeignElement() => new("foreign", "other", new object());

    [Fact]
    public async Task Uia3Provider_ElementFromAnotherProvider_ReturnsNull() =>
        Assert.Null(await new Uia3Provider().FindParentAsync(ForeignElement(), Session));

    [Fact]
    public async Task Uia2Provider_ElementFromAnotherProvider_ReturnsNull() =>
        Assert.Null(await new Uia2Provider().FindParentAsync(ForeignElement(), Session));

    [Fact]
    public async Task Win32Provider_ElementFromAnotherProvider_ReturnsNull() =>
        Assert.Null(await new Win32Provider().FindParentAsync(ForeignElement(), Session));

    // The desktop window has no parent window of its own, so it's the top of Win32Provider's tree.
    [Fact]
    public async Task Win32Provider_TopLevelWindow_ReturnsNull() =>
        Assert.Null(await new Win32Provider().FindParentAsync(new ElementHandle("hwnd:desktop", "win32", NativeMethods.GetDesktopWindow()), Session));

    // A native menu item isn't a window, so its parent comes from the popup it records rather than from GetAncestor.
    [Fact]
    public async Task Win32Provider_NativeMenuItem_ReturnsItsPopupWindow()
    {
        var popup = NativeMethods.GetDesktopWindow();
        var item = new ElementHandle("hmenu:1:0", "win32", new NativeMenuItem(popup, (IntPtr)1, 0));

        var parent = await new Win32Provider().FindParentAsync(item, Session);

        Assert.Equal(popup, Assert.IsType<IntPtr>(parent!.NativeHandle));
    }
}
