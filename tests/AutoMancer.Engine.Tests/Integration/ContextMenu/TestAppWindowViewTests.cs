// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Window views on the test app's other windows: its secondary window and a message box, each scoped to its own controls and usable for everything an App does.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class TestAppWindowViewTests(ContextMenuHostFixture host) : IClassFixture<ContextMenuHostFixture>, IAsyncLifetime
{
    // Resets the status before each test, so a test's status assertion can't pass on an earlier test's pick.
    public Task InitializeAsync() => host.ResetStatusAsync();

    // Nothing to tear down per test; the fixture owns the app.
    public Task DisposeAsync() => Task.CompletedTask;

    // "MenuHost" matches both windows' titles, so RequireNewWindow is what picks the secondary one; the view sees only that window's controls, and a menu opened through it reports to the main window.
    [Fact]
    public async Task SecondaryWindow_DrivenThroughView()
    {
        await host.App.ClickAsync(ContextMenuHostFixture.OpenSecondary);
        var secondary = await host.App.WaitForWindowAsync(new WindowMatchOptions { TitleContains = "MenuHost", RequireNewWindow = true });
        try
        {
            Assert.Empty(await secondary.FindAllAsync(ContextMenuHostFixture.NativeArea));

            await secondary.ClickContextMenuItemAsync(ContextMenuHostFixture.SecondaryArea, "Secondary Item");

            await Expect(host.App, TestAppFixture.Status).ToHaveNameAsync("picked 201");
        }
        finally
        {
            await secondary.CloseWindowAsync();
            await secondary.WaitUntilGoneAsync(ContextMenuHostFixture.SecondaryArea);
        }
    }

    // A message box is a #32770 dialog window: a view reads its text and clicks its button, and the app gets the result.
    [Fact]
    public async Task MessageBox_DrivenThroughView()
    {
        await host.App.ClickAsync(ContextMenuHostFixture.ShowMessage);
        var box = await host.App.WaitForWindowAsync(new WindowMatchOptions { TitleContains = "MenuHost message" });

        Assert.Equal("#32770", (await box.SnapshotAsync())[0].ClassName);
        await Expect(box, Locator.ByName("Hello from MenuHost")).ToBeVisibleAsync();
        await box.ClickAsync(Locator.ByName("OK"));

        await box.WaitUntilGoneAsync(Locator.ByName("OK"));
        await Expect(host.App, TestAppFixture.Status).ToHaveNameAsync("message OK");
    }
}
