// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Providers;

// A scope whose NativeHandle came from a different provider's handle type can't be honored by any one provider. Proves each provider treats that as not-found rather than throwing (a bad cast) or silently searching the whole session (which would break the scoping guarantee) — no live window needed, since the mismatched cast is caught before any real COM/Win32 call happens.
public sealed class ScopeResolutionTests
{
    private static readonly AppSession Session = AppSession.CreateForTesting(ExitedProcess(), (IntPtr)42);
    private static readonly Locator AnyLocator = Locator.ByName("whatever");

    private static Process ExitedProcess()
    {
        var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        process.WaitForExit(2000);
        return process;
    }

    // Stands in for "this scope came from a different provider" — its NativeHandle is neither a COM element nor an hwnd.
    private static ElementHandle MismatchedScope() => new("scope", "other", new object());

    [Fact]
    public async Task Uia3Provider_ScopeFromAnotherProvider_ReturnsNullInsteadOfThrowing() =>
        Assert.Null(await new Uia3Provider().FindScopedElementAsync(AnyLocator, Session, MismatchedScope()));

    [Fact]
    public async Task Uia3Provider_ScopeFromAnotherProvider_FindElementsReturnsEmptyInsteadOfThrowing() =>
        Assert.Empty(await new Uia3Provider().FindScopedElementsAsync(AnyLocator, Session, MismatchedScope()));

    [Fact]
    public async Task Uia2Provider_ScopeFromAnotherProvider_ReturnsNullInsteadOfThrowing() =>
        Assert.Null(await new Uia2Provider().FindScopedElementAsync(AnyLocator, Session, MismatchedScope()));

    [Fact]
    public async Task Uia2Provider_ScopeFromAnotherProvider_FindElementsReturnsEmptyInsteadOfThrowing() =>
        Assert.Empty(await new Uia2Provider().FindScopedElementsAsync(AnyLocator, Session, MismatchedScope()));

    [Fact]
    public async Task Win32Provider_ScopeFromAnotherProvider_ReturnsNullInsteadOfThrowing() =>
        Assert.Null(await new Win32Provider().FindScopedElementAsync(AnyLocator, Session, MismatchedScope()));

    [Fact]
    public async Task Win32Provider_ScopeFromAnotherProvider_FindElementsReturnsEmptyInsteadOfThrowing() =>
        Assert.Empty(await new Win32Provider().FindScopedElementsAsync(AnyLocator, Session, MismatchedScope()));

    // The desktop window is a real, always-valid HWND, but it belongs to a system process rather than Session's own — stands in for a destroyed HWND that the OS has recycled for an unrelated window, which throws nothing (unlike a stale COM element) and so can't be caught by exception handling.
    [Fact]
    public async Task Win32Provider_ScopeHwndBelongsToDifferentProcess_ThrowsStaleElementErrorInsteadOfSearchingWrongWindow()
    {
        var foreignWindowScope = new ElementHandle("hwnd:foreign", "win32", NativeMethods.GetDesktopWindow());

        var ex = await Assert.ThrowsAsync<StaleElementError>(() => new Win32Provider().FindScopedElementAsync(AnyLocator, Session, foreignWindowScope));

        Assert.Same(foreignWindowScope, ex.Element);
    }

    // Same recycled-HWND case for FindAll — a gone scope is reported, never read as "nothing matches".
    [Fact]
    public async Task Win32Provider_ScopeHwndBelongsToDifferentProcess_FindElementsThrowsStaleElementError()
    {
        var foreignWindowScope = new ElementHandle("hwnd:foreign", "win32", NativeMethods.GetDesktopWindow());

        var ex = await Assert.ThrowsAsync<StaleElementError>(() => new Win32Provider().FindScopedElementsAsync(AnyLocator, Session, foreignWindowScope));

        Assert.Same(foreignWindowScope, ex.Element);
    }

    // A window found in another process (e.g. a window view rooted at a helper process's window) records that owner, so it's accepted even though it isn't the session's process.
    [Fact]
    public async Task Win32Provider_ScopeHwndStillOwnedByRecordedProcess_IsAcceptedAcrossProcesses()
    {
        var desktop = NativeMethods.GetDesktopWindow();
        NativeMethods.GetWindowThreadProcessId(desktop, out var owner);
        var helperWindowScope = new ElementHandle("hwnd:helper", "win32", desktop) { ProcessId = (int)owner };

        Assert.Null(await new Win32Provider().FindScopedElementAsync(Locator.ByClassName("No.Such.Class.AutoMancer"), Session, helperWindowScope));
    }

    // A recorded owner that no longer matches means the HWND was recycled for someone else's window — stale, whatever the session's process is.
    [Fact]
    public async Task Win32Provider_ScopeHwndNoLongerOwnedByRecordedProcess_ThrowsStaleElementError()
    {
        var recycledScope = new ElementHandle("hwnd:recycled", "win32", NativeMethods.GetDesktopWindow()) { ProcessId = Environment.ProcessId };

        var ex = await Assert.ThrowsAsync<StaleElementError>(() => new Win32Provider().FindScopedElementAsync(AnyLocator, Session, recycledScope));

        Assert.Same(recycledScope, ex.Element);
    }
}
