// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing;
using AutoMancer.Testing.XUnit;

namespace ConsumerVsCodeTests;

// VS Code's popups are drawn inside its own window (no popup window of their own), so OpenPopupAsync finds them through what's newly showing in the app's window. One VS Code shared by the class; each test leaves the workbench as it found it.
[Collection("VsCode")]
public sealed class PopupDemoTests(VsCodeFixture fixture) : AutoMancerTest(fixture), IClassFixture<VsCodeFixture>
{
    private static readonly Locator EmptyEditorGroup = Locator.ByName("Editor Group 1 (empty)");

    // The editor area's context menu: right-click, then click an item inside it. Ctrl+W in the new, empty group closes it again.
    [Fact]
    public async Task EditorContextMenu_SplitRight_OpensASecondGroup()
    {
        var menu = await App.OpenContextMenuAsync(EmptyEditorGroup);

        // Deliberate pause: VS Code drops an activation sent in a menu's first moments (about 1 run in 3 without it), and nothing in UI Automation changes when it starts accepting them.
        await Task.Delay(300);
        await menu.ClickAsync(Locator.ByName("Split Right"));

        await menu.WaitUntilClosedAsync();
        var secondGroup = Locator.ByName("Editor Group 2 (empty)");
        await Expect(secondGroup).ToBeVisibleAsync();
        await App.ClickAsync(secondGroup);
        await App.PressKeyAsync(Key.W, new KeyModifiers(Control: true));
        await App.WaitUntilGoneAsync(secondGroup);
    }

    // The title-bar File menu. Chromium reports every item in these menus after the first as offscreen, though they're plainly on screen, so the actionability gate refuses them: app.Force is the way through for a known misreport like this one.
    [Fact]
    public async Task FileMenu_ItemsPastTheFirst_NeedForce()
    {
        var fileMenu = await App.OpenPopupAsync(Locator.ByName("File"), PopupTrigger.LeftClick, expect: Locator.ByControlType("MenuItem"));
        var newFile = await fileMenu.FindAsync(Locator.ByName("New File... Ctrl+Alt+Windows+N"));
        Assert.True(newFile.IsOffscreen);

        // Deliberate pause, as in the context-menu test above.
        await Task.Delay(300);
        await fileMenu.Force.ClickAsync(Locator.ByName("New File... Ctrl+Alt+Windows+N"));

        await fileMenu.WaitUntilClosedAsync();
        var newFilePicker = Locator.ByXPath("//Edit[starts-with(@Name,'Select File Type')]");
        await Expect(newFilePicker).ToBeVisibleAsync();
        await App.PressKeyAsync(Key.Escape);
        await App.WaitUntilGoneAsync(newFilePicker);
    }

    // The command center opens a quick pick: a list popup whose items act like any other element.
    [Fact]
    public async Task QuickAccess_ShowAndRunCommands_OpensTheCommandPalette()
    {
        var quickPick = await App.OpenPopupAsync(Locator.ByName("Open Quick Access"), PopupTrigger.LeftClick, expect: Locator.ByControlType("ListItem"));

        await quickPick.ClickAsync(Locator.ByXPath("//ListItem[starts-with(@Name,'Show and Run Commands')]"));

        var palette = Locator.ByXPath("//Edit[@Name='Type the name of a command to run.']");
        await Expect(palette).ToBeVisibleAsync();
        await App.PressKeyAsync(Key.Escape);
        await App.WaitUntilGoneAsync(palette);
    }

    // An activity-bar button's hover is a tooltip drawn in the window: its text is the popup's own name, and it goes away when the pointer leaves.
    [Fact]
    public async Task ActivityBarHover_TextIsReadThenClosesWhenPointerLeaves()
    {
        var hover = await App.OpenPopupAsync(Locator.ByName("Explorer (Ctrl+Shift+E)"), PopupTrigger.Hover, expect: Locator.ByControlType("ToolTip"));

        Assert.Equal("Explorer (Ctrl+Shift+E)", (await hover.SnapshotAsync())[0].Name);
        await App.HoverAsync(EmptyEditorGroup);
        await hover.WaitUntilClosedAsync();
    }
}
