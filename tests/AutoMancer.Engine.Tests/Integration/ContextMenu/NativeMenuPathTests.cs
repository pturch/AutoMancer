// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// The native HMENU path, driven directly: no menu tried is invisible to UIA3, so detection never falls back to Win32Provider by itself; these tests build a Popup around the real #32768 through Win32Provider, so every find and click goes through the HMENU alone.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class NativeMenuPathTests(ContextMenuHostFixture host) : IClassFixture<ContextMenuHostFixture>, IAsyncLifetime
{
    // Clicks the host's status label before each test: it resets the status, and a click elsewhere keeps this test's first right-click on the native area from pairing with the last test's into a double-click, which opens no menu.
    public Task InitializeAsync() => host.ResetStatusAsync();

    // Nothing to tear down per test; the fixtures own the apps.
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly Locator MenuItems = Locator.ByControlType("MenuItem");

    // The native path lists what UIA3 lists, with the same names and states, minus the owner-drawn item, whose HMENU entry has no text; the separator is skipped on both.
    [Fact]
    public async Task Items_MatchUia3PathExceptOwnerDrawn()
    {
        var uia3Menu = await host.App.OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);
        var native = WrapOpenNativeMenu(host.App);

        var uia3Items = await uia3Menu.FindAllAsync(MenuItems);
        var nativeItems = await native.FindAllAsync(MenuItems);
        await native.CloseAsync();

        Assert.Equal(uia3Items.Where(i => i.Name != "").Select(i => (i.Name, i.IsEnabled)), nativeItems.Select(i => (i.Name, i.IsEnabled)));
        Assert.Equal(["Alpha", "Bravo && Co", "Charlie", "Delta"], nativeItems.Select(i => i.Name));
        Assert.All(nativeItems, item => Assert.Equal("win32", item.ResolvedVia));
    }

    // A submenu's parent is listed and clickable (the click opens the submenu), but its children aren't reachable: they live in another #32768 the popup isn't rooted at. A documented limit.
    [Fact]
    public async Task SubmenuParent_IsClickable_ButChildrenAreNotReachable()
    {
        var native = await OpenNativePathAsync(host.App);

        Assert.Empty(await native.FindAllAsync(Locator.ByName("Delta One")));
        await native.ClickAsync(Locator.ByName("Delta"));
        await Expect(host.App, () => Task.FromResult(VisibleMenuWindows().Count)).ToSatisfyAsync(count => count == 2, "to show the submenu beside the menu");

        // One Escape closes the submenu; CloseAsync's closes the menu.
        await KeyboardAction.PressKeyCoreAsync(Key.Escape, KeyModifiers.None, null, CancellationToken.None);
        await native.CloseAsync();
    }

    // A native menu item has no window handle for Win32Provider to compare, so it gets no suggestion rather than an error or a guess.
    [Fact]
    public async Task SuggestLocatorAsync_NativeMenuItem_ReturnsNull()
    {
        var native = await OpenNativePathAsync(host.App);
        var item = await native.FindAsync(Locator.ByName("Alpha"));

        Assert.Null(await native.SuggestLocatorAsync(item));

        await native.CloseAsync();
    }

    // A click through the HMENU path delivers the command.
    [Fact]
    public async Task Click_DeliversCommand()
    {
        var native = await OpenNativePathAsync(host.App);

        await native.ClickAsync(Locator.ByName("Alpha"));

        await native.WaitUntilClosedAsync();
        await Expect(host.App, TestAppFixture.Status).ToHaveNameAsync("picked 101");
    }

    // A grayed item reads as disabled from the HMENU, so the click is refused once ImplicitWaitMs runs out.
    [Fact]
    public async Task DisabledItem_IsRefused()
    {
        var native = await OpenNativePathAsync(host.App.WithOptions(new AppOptions { ImplicitWaitMs = 1_000 }));

        await Assert.ThrowsAsync<ElementNotInteractableError>(() => native.ClickAsync(Locator.ByName("Charlie")));

        await native.CloseAsync();
    }

    // Force clicks the grayed item anyway; Windows ignores it, so no command is delivered and the menu stays open.
    [Fact]
    public async Task DisabledItem_ThroughForce_IsIgnoredByWindows()
    {
        var native = await OpenNativePathAsync(host.App);

        await native.Force.ClickAsync(Locator.ByName("Charlie"));

        Assert.True(await native.IsOpenAsync());
        await native.CloseAsync();
        await Expect(host.App, TestAppFixture.Status).ToHaveNameAsync("ready");
    }

    // An item held across closing and reopening the menu is stale: the reopened menu is a new HMENU, even if Windows reuses the #32768 window.
    [Fact]
    public async Task ItemHeldAcrossReopen_IsStale()
    {
        var native = await OpenNativePathAsync(host.App);
        var alpha = await native.FindAsync(Locator.ByName("Alpha"));
        await native.CloseAsync();
        // A click elsewhere first, so the reopening right-click doesn't pair with the first into a double-click.
        await host.ResetStatusAsync();
        var reopened = await OpenNativePathAsync(host.App);

        await Assert.ThrowsAsync<StaleElementError>(() => ClickAction.ExecuteAsync(alpha));

        await reopened.CloseAsync();
    }

    // CloseAsync closes the menu, and a second call is a no-op, as on the UIA3 path.
    [Fact]
    public async Task CloseAsync_ClosesThenNoOps()
    {
        var native = await OpenNativePathAsync(host.App);

        await native.CloseAsync();
        Assert.False(await native.IsOpenAsync());
        await native.CloseAsync();
    }

    // Opens the native menu, then builds a native-path Popup around it with opener's settings.
    private async Task<Popup> OpenNativePathAsync(App opener)
    {
        await opener.OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);
        return WrapOpenNativeMenu(opener);
    }

    // Builds a Popup around the open #32768 menu through Win32Provider, with opener's settings.
    private Popup WrapOpenNativeMenu(App opener) =>
        new(opener, App.Win32.WrapWindowHandle(VisibleMenuWindows().Single()), "context menu");

    // The host's visible #32768 windows: the menu, plus a submenu if one is open.
    private List<IntPtr> VisibleMenuWindows() =>
        NativeMethods.GetVisibleTopLevelWindows(new HashSet<int> { host.App.ProcessId }).Where(NativeMethods.IsPopupMenuWindow).ToList();
}
