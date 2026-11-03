// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Integration;

// Each menu kind is found by popup detection, which always goes through UIA3 whichever provider found the target; a target with no menu times out naming the detection step.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class ContextMenuDetectionTests(ContextMenuHostFixture host, WpfMenuHostFixture wpf) : IClassFixture<ContextMenuHostFixture>, IClassFixture<WpfMenuHostFixture>, IAsyncLifetime
{
    // Clicks the host's status label before each test: it resets the status, and a click elsewhere keeps this test's first right-click on the native area from pairing with the last test's into a double-click, which opens no menu.
    public Task InitializeAsync() => host.ResetStatusAsync();

    // Nothing to tear down per test; the fixtures own the apps.
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly Locator MenuItems = Locator.ByControlType("MenuItem");

    // A TrackPopupMenuEx menu is its own #32768 top-level window, which UIA3 reads through its MSAA proxy.
    [Fact]
    public async Task NativeMenu_IsDetectedAsItsOwn32768Window()
    {
        var menu = await host.App.OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);
        var snapshot = await menu.SnapshotAsync();
        var items = await menu.FindAllAsync(MenuItems);
        await menu.CloseAsync();

        Assert.Equal("#32768", snapshot[0].ClassName);
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal("uia3", item.ResolvedVia));
    }

    // A WinForms ContextMenuStrip is a toolkit popup with no HMENU behind it.
    [Fact]
    public async Task WinFormsStrip_IsDetected()
    {
        var menu = await host.App.OpenContextMenuAsync(ContextMenuHostFixture.StripArea);
        var items = await menu.FindAllAsync(MenuItems);
        await menu.CloseAsync();

        Assert.Equal(["Strip One", "Strip Two"], items.Select(i => i.Name));
    }

    // A WPF ContextMenu opens in its own Popup window.
    [Fact]
    public async Task WpfMenu_IsDetected()
    {
        var menu = await wpf.App.OpenContextMenuAsync(WpfMenuHostFixture.MenuLabel);
        var items = await menu.FindAllAsync(MenuItems);
        await menu.CloseAsync();

        Assert.Equal(["Alpha", "Bravo", "Charlie", "Delta"], items.Select(i => i.Name));
    }

    // A target in another window of the app (an owned secondary window, found from the main App since UIA nests owned windows) has its menu found too: detection scopes to the target's own window.
    [Fact]
    public async Task TargetInSecondaryWindow_MenuIsDetected()
    {
        await host.App.ClickAsync(ContextMenuHostFixture.OpenSecondary);
        var secondary = await host.App.WaitForWindowAsync(new WindowMatchOptions { TitleContains = "secondary" });
        try
        {
            var menu = await host.App.OpenContextMenuAsync(ContextMenuHostFixture.SecondaryArea);
            var items = await menu.FindAllAsync(MenuItems);
            await menu.CloseAsync();

            Assert.Equal(["Secondary Item"], items.Select(i => i.Name));
        }
        finally
        {
            await secondary.CloseWindowAsync();
        }
    }

    // A target found through UIA2 still has its menu detected and searched through UIA3, which sees every popup tried (UIA2 sees a #32768 only as an empty Pane).
    [Fact]
    public async Task Uia2FoundTarget_StillDetectsThroughUia3()
    {
        var menu = await host.App.WithOptions(new AppOptions { ProviderChain = ["uia2"] }).OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);
        var items = await menu.FindAllAsync(MenuItems);
        await menu.CloseAsync();

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal("uia3", item.ResolvedVia));
    }

    // Right-clicking something with no menu waits out ImplicitWaitMs and names the detection step it was waiting on.
    [Fact]
    public async Task NoMenu_ThrowsElementNotFoundNamingDetection()
    {
        var quick = host.App.WithOptions(new AppOptions { ImplicitWaitMs = 1_000 });

        var ex = await Assert.ThrowsAsync<ElementNotFoundError>(() => quick.OpenContextMenuAsync(TestAppFixture.Status));

        Assert.Contains("context menu detection", ex.AttemptedProviders);
    }
}
