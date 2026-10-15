// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Testing.XUnit;

namespace ConsumerVsCodeTests;

// Shows how enabled state, hidden views, and occlusion read on an Electron app, against one VS Code shared by the class; each test leaves the workbench as it found it.
[Collection("VsCode")]
public sealed class WorkbenchStateDemoTests(VsCodeFixture fixture) : AutoMancerTest(fixture), IClassFixture<VsCodeFixture>
{
    private static readonly Locator EmptyEditorGroup = Locator.ByName("Editor Group 1 (empty)");

    // Chromium reports a disabled toolbar button as disabled, so actions wait on it and then throw; it enables once there's navigation history.
    [Fact]
    public async Task ClickAsync_DisabledGoBack_ThrowsUntilNavigationEnablesIt()
    {
        var goBack = Locator.ByName("Go Back (Alt+LeftArrow)");
        var shortWait = App.WithOptions(new AppOptions { ImplicitWaitMs = 1_000, PollIntervalMs = 200 });
        await Assert.ThrowsAsync<ElementNotInteractableError>(() => shortWait.ClickAsync(goBack));

        // Two new editors, so the second can navigate back to the first.
        await App.ClickAsync(EmptyEditorGroup);
        await App.PressKeyAsync(Key.N, new KeyModifiers(Control: true));
        await Task.Delay(300); // a keystroke right after a new editor opens can be swallowed while focus settles.
        await App.PressKeyAsync(Key.N, new KeyModifiers(Control: true));
        try
        {
            await Expect(goBack).ToBeEnabledAsync();
        }
        finally
        {
            // Ctrl+K Ctrl+W closes every editor in the group; untouched untitled editors close without a save prompt.
            await App.HotkeyAsync(new KeyModifiers(Control: true), [Key.K]);
            await App.HotkeyAsync(new KeyModifiers(Control: true), [Key.W]);
            await App.FindAsync(EmptyEditorGroup);
        }
    }

    // Chromium drops a hidden view from the UI Automation tree rather than marking it offscreen, so a handle held across Ctrl+B goes stale instead of failing as not interactable.
    [Fact]
    public async Task HeldSideBarElement_AfterSideBarHides_ThrowsStaleElementError()
    {
        var workspaceSection = Locator.ByName("Explorer Section: workspace");
        await App.PressKeyAsync(Key.B, new KeyModifiers(Control: true));
        var held = await App.FindAsync(workspaceSection);

        await App.PressKeyAsync(Key.B, new KeyModifiers(Control: true));
        await App.WaitUntilGoneAsync(workspaceSection);

        var error = await Assert.ThrowsAsync<StaleElementError>(() => ClickAction.ExecuteAsync(held));
        Assert.Same(held, error.Element);
    }

    // The Command Palette covers the editor area's center while it's open; Chromium can answer the first hit-test after a change from stale state, so poll with Expect rather than asserting a single call.
    [Fact]
    public async Task IsUnobscuredAsync_EditorAreaUnderCommandPalette_FalseUntilDismissed()
    {
        // Short enough that the palette's list reaches past the editor area's center; also keeps the status bar clear of the taskbar.
        await App.MoveWindowAsync(100, 60);
        await App.ResizeWindowAsync(1400, 450);
        await Expect(() => App.IsUnobscuredAsync(EmptyEditorGroup)).ToSatisfyAsync(clear => clear, "to be clear");

        var palette = Locator.ByName("Type the name of a command to run.");
        await App.HotkeyAsync(new KeyModifiers(Control: true, Shift: true), [Key.P]);
        try
        {
            await Expect(palette).ToBeVisibleAsync();
            await Expect(() => App.IsUnobscuredAsync(EmptyEditorGroup)).ToSatisfyAsync(clear => !clear, "to be covered by the Command Palette");
            Assert.True(await App.IsUnobscuredAsync(palette));
        }
        finally
        {
            await App.PressKeyAsync(Key.Escape);
            await App.WaitUntilGoneAsync(palette);
        }
        await Expect(() => App.IsUnobscuredAsync(EmptyEditorGroup)).ToSatisfyAsync(clear => clear, "to be clear again");
    }
}
