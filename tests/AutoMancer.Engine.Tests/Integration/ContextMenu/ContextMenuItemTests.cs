// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Listing and clicking items inside an open context menu through UIA3, and every way that can fail: disabled, missing, closed.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class ContextMenuItemTests(ContextMenuHostFixture host, WpfMenuHostFixture wpf) : IClassFixture<ContextMenuHostFixture>, IClassFixture<WpfMenuHostFixture>, IAsyncLifetime
{
    // Clicks the host's status label before each test: it resets the status, and a click elsewhere keeps this test's first right-click on the native area from pairing with the last test's into a double-click, which opens no menu.
    public Task InitializeAsync() => host.ResetStatusAsync();

    // Nothing to tear down per test; the fixtures own the apps.
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly Locator MenuItems = Locator.ByControlType("MenuItem");

    // UIA3 reports the native menu's names without mnemonics or the tab-separated shortcut, keeps && as-is, and lists the owner-drawn item with an empty name; the separator isn't listed.
    [Fact]
    public async Task NativeMenu_ListsNamesAndEnabledState()
    {
        var menu = await host.App.OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);
        var items = await menu.FindAllAsync(MenuItems);
        await menu.CloseAsync();

        Assert.Equal([("Alpha", true), ("Bravo && Co", true), ("Charlie", false), ("", true), ("Delta", true)], items.Select(i => (i.Name, i.IsEnabled == true)));
    }

    // The WPF menu's items and their enabled state.
    [Fact]
    public async Task WpfMenu_ListsNamesAndEnabledState()
    {
        var menu = await wpf.App.OpenContextMenuAsync(WpfMenuHostFixture.MenuLabel);
        var items = await menu.FindAllAsync(MenuItems);
        await menu.CloseAsync();

        Assert.Equal([("Alpha", true), ("Bravo", true), ("Charlie", false), ("Delta", true)], items.Select(i => (i.Name, i.IsEnabled == true)));
    }

    // The one-call shortcut delivers the command on each menu kind; "Bravo && Co" is UIA3's name for the item labelled "Bravo & Co".
    [Fact]
    public async Task ClickContextMenuItemAsync_DeliversCommandOnEachMenuKind()
    {
        await host.App.ClickContextMenuItemAsync(ContextMenuHostFixture.NativeArea, "Bravo && Co");
        await Expect(host.App, TestAppFixture.Status).ToHaveNameAsync("picked 102");

        await host.App.ClickContextMenuItemAsync(ContextMenuHostFixture.StripArea, "Strip Two");
        await Expect(host.App, TestAppFixture.Status).ToHaveNameAsync("strip Two");

        await wpf.ResetStatusAsync();
        await wpf.App.ClickContextMenuItemAsync(WpfMenuHostFixture.MenuLabel, "Bravo");
        await Expect(wpf.App, TestAppFixture.Status).ToHaveNameAsync("picked Bravo");
    }

    // A click through the popup itself delivers the command, and the menu closes.
    [Fact]
    public async Task PopupClickAsync_DeliversCommandAndMenuCloses()
    {
        var menu = await host.App.OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);

        await menu.ClickAsync(Locator.ByName("Alpha"));

        await menu.WaitUntilClosedAsync();
        await Expect(host.App, TestAppFixture.Status).ToHaveNameAsync("picked 101");
    }

    // A disabled item is refused once ImplicitWaitMs runs out, and the menu stays open.
    [Fact]
    public async Task DisabledItem_IsRefusedAndMenuStaysOpen()
    {
        var menu = await host.App.WithOptions(new AppOptions { ImplicitWaitMs = 1_000 }).OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);

        await Assert.ThrowsAsync<ElementNotInteractableError>(() => menu.ClickAsync(Locator.ByName("Charlie")));

        Assert.True(await menu.IsOpenAsync());
        await menu.CloseAsync();
    }

    // app.Force skips the enabled check and clicks the grayed item anyway; Windows ignores the click, so no command is delivered and the menu stays open.
    [Fact]
    public async Task DisabledItem_ThroughForce_IsClickedButIgnoredByWindows()
    {
        var menu = await host.App.OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);

        await menu.Force.ClickAsync(Locator.ByName("Charlie"));

        Assert.True(await menu.IsOpenAsync());
        await menu.CloseAsync();
        await Expect(host.App, TestAppFixture.Status).ToHaveNameAsync("ready");
    }

    // A missing item through the popup raises ElementNotFoundError and leaves the menu open, so the test can still act on it.
    [Fact]
    public async Task MissingItem_ThroughPopup_ThrowsAndMenuStaysOpen()
    {
        var menu = await host.App.WithOptions(new AppOptions { ImplicitWaitMs = 1_000 }).OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);

        await Assert.ThrowsAsync<ElementNotFoundError>(() => menu.ClickAsync(Locator.ByName("Zulu")));

        Assert.True(await menu.IsOpenAsync());
        await menu.CloseAsync();
    }

    // A missing item through the shortcut raises ElementNotFoundError naming the menu, and closes the menu so it can't swallow the next input.
    [Fact]
    public async Task MissingItem_ThroughShortcut_ThrowsAndMenuIsClosed()
    {
        var quick = host.App.WithOptions(new AppOptions { ImplicitWaitMs = 1_000 });

        var ex = await Assert.ThrowsAsync<ElementNotFoundError>(() => quick.ClickContextMenuItemAsync(ContextMenuHostFixture.NativeArea, "Zulu"));

        Assert.Contains("uia3 context menu", ex.AttemptedProviders);
        Assert.DoesNotContain(NativeMethods.GetVisibleTopLevelWindows(new HashSet<int> { host.App.ProcessId }), NativeMethods.IsPopupMenuWindow);
    }

    // A submenu is a popup opened from inside the menu popup: hovering the native menu's Delta opens a second #32768. Hover, because UIA Invoke on a submenu parent reports success without opening anything. Clicking an item closes the whole menu.
    [Fact]
    public async Task NativeSubmenu_OpenedByHoverFromTheMenu_ItemIsClicked()
    {
        var menu = await host.App.OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);
        var submenu = await menu.OpenPopupAsync(Locator.ByName("Delta"), PopupTrigger.Hover, expect: Locator.ByName("Delta One"));

        await submenu.ClickAsync(Locator.ByName("Delta One"));

        await menu.WaitUntilClosedAsync();
        await Expect(host.App, TestAppFixture.Status).ToHaveNameAsync("picked 105");
    }

    // Escape closes one level, so closing the menu while its submenu is open closes only the submenu; the timeout says so, and closing innermost first works.
    [Fact]
    public async Task CloseAsync_WithSubmenuOpen_TimesOutWithHint_InnermostFirstWorks()
    {
        var menu = await host.App.WithOptions(new AppOptions { ImplicitWaitMs = 1_000 }).OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);
        var submenu = await menu.OpenPopupAsync(Locator.ByName("Delta"), PopupTrigger.Hover, expect: Locator.ByName("Delta One"));

        var error = await Assert.ThrowsAsync<ElementStillPresentError>(() => menu.CloseAsync());
        Assert.Contains("close it first", error.Message);
        Assert.False(await submenu.IsOpenAsync());

        await menu.CloseAsync();
        Assert.False(await menu.IsOpenAsync());
    }

    // A WPF submenu opens in its own Popup window when its parent item is clicked.
    [Fact]
    public async Task WpfSubmenu_OpenedByClickFromTheMenu_ItemIsClicked()
    {
        await wpf.ResetStatusAsync();
        var menu = await wpf.App.OpenContextMenuAsync(WpfMenuHostFixture.MenuLabel);
        var submenu = await menu.OpenPopupAsync(Locator.ByName("Delta"), PopupTrigger.LeftClick, expect: Locator.ByName("Delta One"));

        await submenu.ClickAsync(Locator.ByName("Delta One"));

        await menu.WaitUntilClosedAsync();
        await Expect(wpf.App, TestAppFixture.Status).ToHaveNameAsync("picked Delta One");
    }

    // Once closed, a popup can't be searched: it raises StaleElementError rather than searching the app.
    [Fact]
    public async Task ClosedMenu_FindThrowsStale()
    {
        var menu = await host.App.OpenContextMenuAsync(ContextMenuHostFixture.NativeArea);
        await menu.CloseAsync();

        await Assert.ThrowsAsync<StaleElementError>(() => menu.FindAllAsync(MenuItems));
    }

    // CloseAsync closes an open menu, and a second call is a no-op rather than an Escape sent to the app.
    [Fact]
    public async Task CloseAsync_ClosesThenNoOps()
    {
        var menu = await wpf.App.OpenContextMenuAsync(WpfMenuHostFixture.MenuLabel);

        await menu.CloseAsync();
        Assert.False(await menu.IsOpenAsync());
        await menu.CloseAsync();
    }
}
