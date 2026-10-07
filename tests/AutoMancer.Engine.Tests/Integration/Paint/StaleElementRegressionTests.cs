// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Automation;
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies Uia2Provider and Uia3Provider return empty/null instead of throwing when their target element or window goes stale mid-search (including ElementNotAvailableException alongside InvalidOperationException), except a gone scope, which raises StaleElementError.
// Targets Paint since, unlike Notepad/Task Manager, it isn't single-instance — a fresh launch is always an independent process safe to kill mid-test.
[Trait("Category", "Integration")]
public sealed class StaleElementRegressionTests
{
    // Right after its process exits, a raw UIA2 read throws COMException E_UNEXPECTED for ~30ms before settling on ElementNotAvailableException — never a plain InvalidOperationException, and either way the shared check must call it stale.
    [Fact]
    public async Task RawUia2PropertyAccess_AfterProcessExit_ThrowsAFailureClassifiedAsStale()
    {
        await using var app = await App.LaunchAsync("mspaint.exe", new AppOptions { Logger = null, ProviderChain = ["uia2"], ImplicitWaitMs = 10_000 });
        var element = await app.FindAsync(Locator.ByAutomationId("PencilTool"));
        Assert.Equal("uia2", element.ResolvedVia);

        var uia2Element = (AutomationElement)element.NativeHandle;
        await KillWindowOwnerAsync(app);

        var ex = Assert.ThrowsAny<Exception>(() => _ = uia2Element.Current.Name);
        Assert.True(ex is ElementNotAvailableException or System.Runtime.InteropServices.COMException, $"unexpected {ex.GetType().Name}");
        Assert.True(ElementInputHelpers.IsStaleFailure(ex, element), $"{ex.GetType().Name} 0x{ex.HResult:X8} not classified as stale");
    }

    // A closed XAML dialog's elements never throw — they stay readable, detached, with blank values — so this pins the detached check: actions, value reads, and scoped finds against Paint's closed Resize dialog must all raise StaleElementError instead of clicking where the dialog used to be.
    [Theory]
    [InlineData("uia2")]
    [InlineData("uia3")]
    public async Task ClosedXamlDialog_HeldElementAndScope_ThrowStaleElementError(string provider)
    {
        await using var app = await App.LaunchAsync("mspaint.exe", new AppOptions { Logger = null, ProviderChain = [provider], ImplicitWaitMs = 10_000 });
        await app.FindAsync(Locator.ByAutomationId("PencilTool"));
        await app.HotkeyAsync(new KeyModifiers(Control: true), [Key.W]);
        var cancel = await app.FindAsync(Locator.ByName("Cancel"));
        var horizontal = await app.FindAsync(Locator.ByAutomationId("HorizontalResizeTextBox"));
        var session = AppSession.CreateForTesting(System.Diagnostics.Process.GetProcessById(app.ProcessId), app.RootWindowHandle);

        await app.PressKeyAsync(Key.Escape);
        // Search stops matching the button mid close-animation, a moment before it detaches; its raw Name going blank is the detach itself, independent of the check under test.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (RawName(cancel) != "" && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        Assert.Same(cancel, Assert.Throws<StaleElementError>(() => ElementInputHelpers.EnsureForeground(cancel)).Element);
        Assert.Same(cancel, (await Assert.ThrowsAsync<StaleElementError>(() => cancel.Operator!.TryGetValueAsync(cancel))).Element);
        Assert.Same(cancel, (await Assert.ThrowsAsync<StaleElementError>(() => cancel.Provider!.FindScopedElementsAsync(Locator.ByControlType("Text"), session, cancel))).Element);
        // The native Invoke/SetValue path runs before EnsureForeground and has no detached check of its own — it's safe only because a detached element stops exposing its patterns, so both decline and the action falls through to EnsureForeground.
        Assert.False(await cancel.Operator!.TryClickAsync(cancel));
        Assert.False(await horizontal.Operator!.TrySetValueAsync(horizontal, "77"));
    }

    // The live reads every action and value read makes: a dead element must raise StaleElementError for both providers — never fall through to a click at its cached BoundingRect, never read back as a null value. Calls EnsureForeground/TryGetValueAsync directly, so a regression can't send real input to wherever Paint used to be.
    [Theory]
    [InlineData("uia2")]
    [InlineData("uia3")]
    public async Task EnsureForeground_ElementOfKilledProcess_ThrowsStaleElementError(string provider)
    {
        await using var app = await App.LaunchAsync("mspaint.exe", new AppOptions { Logger = null, ProviderChain = [provider], ImplicitWaitMs = 10_000 });
        var element = await app.FindAsync(Locator.ByAutomationId("PencilTool"));
        Assert.Equal(provider, element.ResolvedVia);

        await KillWindowOwnerAsync(app);

        Assert.Throws<StaleElementError>(() => ElementInputHelpers.EnsureForeground(element));
        await Assert.ThrowsAsync<StaleElementError>(() => element.Operator!.TryGetValueAsync(element));
    }

    // Raw FindFirst/FindAll on a gone scope don't throw — uia2 returns null/an empty collection, uia3 null/a null array — so this pins the providers' liveness probe that turns it into StaleElementError instead of "nothing matches". The live-scope control proves a genuine miss still returns null/empty.
    [Theory]
    [InlineData("uia2")]
    [InlineData("uia3")]
    public async Task ScopedFind_ScopeOfKilledProcess_ThrowsStaleElementError(string provider)
    {
        await using var app = await App.LaunchAsync("mspaint.exe", new AppOptions { Logger = null, ProviderChain = [provider], ImplicitWaitMs = 10_000 });
        var scope = await app.FindAsync(Locator.ByAutomationId("PencilTool"));
        Assert.Equal(provider, scope.ResolvedVia);
        var session = AppSession.CreateForTesting(System.Diagnostics.Process.GetProcessById(app.ProcessId), app.RootWindowHandle);
        var noMatch = Locator.ByAutomationId("NoSuchElement");

        Assert.Null(await scope.Provider!.FindScopedElementAsync(noMatch, session, scope));
        Assert.Empty(await scope.Provider!.FindScopedElementsAsync(noMatch, session, scope));

        await KillWindowOwnerAsync(app);

        Assert.Same(scope, (await Assert.ThrowsAsync<StaleElementError>(() => scope.Provider!.FindScopedElementAsync(noMatch, session, scope))).Element);
        Assert.Same(scope, (await Assert.ThrowsAsync<StaleElementError>(() => scope.Provider!.FindScopedElementsAsync(noMatch, session, scope))).Element);
    }

    // FindAllAsync returns gracefully when the resolved uia2 element's process dies before Wrap reads its properties.
    [Fact]
    public async Task FindAllAsync_DoesNotThrow_WhenUia2ElementGoesStaleDuringWrap()
    {
        await using var app = await App.LaunchAsync("mspaint.exe", new AppOptions { Logger = null, ProviderChain = ["uia2"], ImplicitWaitMs = 10_000 });
        var element = await app.FindAsync(Locator.ByAutomationId("PencilTool"));
        Assert.Equal("uia2", element.ResolvedVia);

        System.Diagnostics.Process.GetProcessById(app.ProcessId).Kill();
        await Task.Delay(200);

        var results = await app.FindAllAsync(Locator.ByAutomationId("PencilTool"));
        Assert.NotNull(results);
    }

    // FindAllAsync returns gracefully when the target window is destroyed before uia3 resolves its root element.
    [Fact]
    public async Task FindAllAsync_DoesNotThrow_WhenUia3RootHandleGoesStaleDuringSearch()
    {
        await using var app = await App.LaunchAsync("mspaint.exe", new AppOptions { Logger = null, ProviderChain = ["uia3"], ImplicitWaitMs = 10_000 });
        var element = await app.FindAsync(Locator.ByAutomationId("PencilTool"));
        Assert.Equal("uia3", element.ResolvedVia);

        System.Diagnostics.Process.GetProcessById(app.ProcessId).Kill();
        await Task.Delay(200);

        var results = await app.FindAllAsync(Locator.ByAutomationId("PencilTool"));
        Assert.NotNull(results);
    }

    // Reads the element's current Name straight from UIA, bypassing AutoMancer's stale checks.
    private static string RawName(ElementHandle element) => element.NativeHandle is AutomationElement uia2
        ? uia2.Current.Name
        : ((Interop.UIAutomationClient.IUIAutomationElement)element.NativeHandle).CurrentName;

    // Kills the process that owns app's window and waits for it to exit, so its UIA elements are really gone before the test probes them; a fixed delay occasionally probed a still-live window.
    private static async Task KillWindowOwnerAsync(App app)
    {
        NativeMethods.GetWindowThreadProcessId(app.RootWindowHandle, out var ownerPid);
        using var owner = System.Diagnostics.Process.GetProcessById((int)ownerPid);
        owner.Kill();
        await owner.WaitForExitAsync(new CancellationTokenSource(10_000).Token);
    }
}
