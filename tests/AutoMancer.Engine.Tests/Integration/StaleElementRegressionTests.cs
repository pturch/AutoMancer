// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Automation;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies Uia2Provider and Uia3Provider return empty/null instead of throwing when their target element or window goes stale mid-search, including ElementNotAvailableException alongside InvalidOperationException.
// Targets Paint since, unlike Notepad/Task Manager, it isn't single-instance — a fresh launch is always an independent process safe to kill mid-test.
[Trait("Category", "Integration")]
public sealed class StaleElementRegressionTests
{
    // A stale UIA2 element throws ElementNotAvailableException, not InvalidOperationException.
    [Fact]
    public async Task RawUia2PropertyAccess_ThrowsElementNotAvailableException_NotInvalidOperationException()
    {
        await using var app = await App.LaunchAsync("mspaint.exe", new AppOptions { Logger = null, ProviderChain = ["uia2"], ImplicitWaitMs = 10_000 });
        var element = await app.FindAsync(Locator.ByAutomationId("PencilTool"));
        Assert.Equal("uia2", element.ResolvedVia);

        var uia2Element = (AutomationElement)element.NativeHandle;
        System.Diagnostics.Process.GetProcessById(app.ProcessId).Kill();
        await Task.Delay(500);

        var ex = Assert.ThrowsAny<Exception>(() => _ = uia2Element.Current.Name);
        Assert.IsType<ElementNotAvailableException>(ex);
        Assert.IsNotType<InvalidOperationException>(ex);
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
}
