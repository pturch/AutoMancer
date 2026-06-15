// Copyright (c) AutoMancer Contributors. Licensed under the MIT License.
using System.Diagnostics;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Core;

public sealed class AppSessionTests
{
    [Fact]
    public async Task AttachByPidAsync_InvalidPid_ThrowsAppLaunchError()
    {
        await Assert.ThrowsAsync<AppLaunchError>(() => AppSession.AttachByPidAsync(99_999_999));
    }

    [Fact]
    public async Task AttachByPidAsync_ProcessWithNoWindow_ThrowsAppLaunchError()
    {
        // The test host is a console process — MainWindowHandle is IntPtr.Zero.
        var pid = Process.GetCurrentProcess().Id;
        await Assert.ThrowsAsync<AppLaunchError>(() => AppSession.AttachByPidAsync(pid));
    }

    [Fact]
    public async Task AttachByTitleAsync_NoMatchingTitle_ThrowsAppLaunchError()
    {
        var uniqueTitle = $"__AutoMancer_NoWindow_{Guid.NewGuid()}";
        await Assert.ThrowsAsync<AppLaunchError>(() => AppSession.AttachByTitleAsync(uniqueTitle));
    }

    [Fact]
    public async Task LaunchAsync_NonExistentExecutable_ThrowsAppLaunchError()
    {
        await Assert.ThrowsAsync<AppLaunchError>(() =>
            AppSession.LaunchAsync("__automancer_does_not_exist__.exe"));
    }
}
