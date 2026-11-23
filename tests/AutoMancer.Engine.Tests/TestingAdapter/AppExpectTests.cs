// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Text.RegularExpressions;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Testing;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.TestingAdapter;

// Covers AppExpect's pass, retry and timeout-failure logic without a live window: the session wraps the test host's own process and a root handle that isn't a window, so title reads blank and state reads false.
public sealed class AppExpectTests
{
    private static readonly AppOptions FastOptions = new() { ProviderChain = ["uia3"], ImplicitWaitMs = 100, PollIntervalMs = 10, Logger = null };

    // Builds an App over process with a root handle that isn't a real window.
    private static App FakeApp(Process? process = null) =>
        App.CreateForTesting(AppSession.CreateForTesting(process ?? Process.GetCurrentProcess(), (IntPtr)1), [], FastOptions, ownsProcess: false);

    [Fact]
    public async Task ToBeRunningAsync_RunningProcess_Passes()
        => await Expect(FakeApp()).ToBeRunningAsync();

    [Fact]
    public async Task ToHaveExitedAsync_RunningProcess_FailsWithStillRunning()
    {
        var error = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(FakeApp()).ToHaveExitedAsync());

        Assert.Contains("to have exited", error.Message);
        Assert.Contains("the process was still running", error.Message);
    }

    // The one condition that flips on its own: a child process is killed mid-poll, so the assertion has to retry to see it.
    [Fact]
    public async Task ToHaveExitedAsync_ProcessExitsDuringPolling_RetriesUntilItPasses()
    {
        using var child = Process.Start(new ProcessStartInfo("ping", "-n 30 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true })!;
        var app = FakeApp(child);
        _ = Task.Delay(150).ContinueWith(_ => child.Kill());

        await Expect(app).ToHaveExitedAsync(timeoutMs: 5_000);
        await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(app).ToBeRunningAsync());
    }

    [Fact]
    public async Task ToBeRespondingAsync_RunningProcess_Passes()
        => await Expect(FakeApp()).ToBeRespondingAsync();

    [Fact]
    public async Task ToHaveTitleAsync_BlankTitle_MatchesStringAndRegex()
    {
        await Expect(FakeApp()).ToHaveTitleAsync("");
        await Expect(FakeApp()).ToHaveTitleAsync(new Regex("^$"));
    }

    [Fact]
    public async Task ToHaveTitleAsync_WrongTitle_FailsWithActualTitle()
    {
        var error = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(FakeApp()).ToHaveTitleAsync("Notepad"));

        Assert.Contains("to have title \"Notepad\"", error.Message);
        Assert.Contains("the title was \"\"", error.Message);
    }

    [Fact]
    public async Task ToHaveTitleAsync_RegexNoMatch_Fails()
    {
        var error = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(FakeApp()).ToHaveTitleAsync(new Regex("Note.*")));

        Assert.Contains("to have a title matching /Note.*/", error.Message);
    }

    [Fact]
    public async Task ToBeMinimizedAsync_And_ToBeMaximizedAsync_NotInThatState_Fail()
    {
        await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(FakeApp()).ToBeMinimizedAsync());
        await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(FakeApp()).ToBeMaximizedAsync());
    }

    [Fact]
    public async Task ToHaveWindowCountAsync_MatchingCount_PassesAndWrongCountFails()
    {
        // The test host is a console process with no visible top-level window.
        await Expect(FakeApp()).ToHaveWindowCountAsync(0);

        var error = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(FakeApp()).ToHaveWindowCountAsync(3));
        Assert.Contains("to have 3 visible window(s)", error.Message);
        Assert.Contains("there were 0", error.Message);
    }

    [Fact]
    public async Task ToHaveNoDialogAsync_NoOtherWindows_Passes()
        => await Expect(FakeApp()).ToHaveNoDialogAsync();

    [Fact]
    public async Task ToHaveDialogAsync_NoMatchingWindow_Fails()
    {
        var error = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(FakeApp()).ToHaveDialogAsync(new WindowMatchOptions { TitleContains = "Open" }));

        Assert.Contains("to have a matching dialog open", error.Message);
    }

    [Fact]
    public async Task ToHaveMemoryBelowAsync_GenerousBoundPassesAndTinyBoundFails()
    {
        await Expect(FakeApp()).ToHaveMemoryBelowAsync(long.MaxValue);

        var error = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(FakeApp()).ToHaveMemoryBelowAsync(1));
        Assert.Contains("to use less than 1 bytes of private memory", error.Message);
        Assert.Contains("it used", error.Message);
    }

    [Fact]
    public async Task ToHaveHandleCountBelowAsync_GenerousBoundPassesAndTinyBoundFails()
    {
        await Expect(FakeApp()).ToHaveHandleCountBelowAsync(int.MaxValue);

        var error = await Assert.ThrowsAsync<ExpectFailedError>(() => Expect(FakeApp()).ToHaveHandleCountBelowAsync(1));
        Assert.Contains("to have fewer than 1 handles", error.Message);
    }

    // Window bounds, foreground and visual stability read a real window, so only their live tests cover them.
}
