// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Window views on Notepad's Open dialog (#32770, an owned top-level window): finds and actions scoped to the dialog, a handle held from it acted on directly, and a view's dispose leaving Notepad alone.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class WindowViewIntegrationTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    private static readonly Locator Document = Locator.ByControlType("Document");
    private static readonly Locator FileNameBox = Locator.ByClassName("Edit");
    private static readonly Locator Cancel = Locator.ByName("Cancel");

    // The view is rooted at the dialog: its snapshot starts there, Notepad's Document is out of its reach, and typing into the file-name box round-trips; Escape through the view closes the dialog.
    [Fact]
    public async Task OpenDialog_DrivenThroughView_IsScopedToTheDialog()
    {
        var dialog = await OpenDialogAsync();

        var snapshot = await dialog.SnapshotAsync();
        Assert.Equal("#32770", snapshot[0].ClassName);
        Assert.Empty(await dialog.FindAllAsync(Document));

        await dialog.TypeAsync(FileNameBox, "view check");
        Assert.Equal("view check", await dialog.GetValueAsync(FileNameBox));

        await dialog.PressKeyAsync(Key.Escape);
        await dialog.WaitUntilGoneAsync(Cancel);
    }

    // A handle found through the view stays usable on its own: the static ClickAction clicks the held Cancel button, and the main App carries on in Notepad's window.
    [Fact]
    public async Task HeldDialogButton_ClickedDirectly_ClosesDialog()
    {
        var dialog = await OpenDialogAsync();
        var cancel = await dialog.FindAsync(Cancel);

        await ClickAction.ExecuteAsync(cancel);

        await dialog.WaitUntilGoneAsync(Cancel);
        await fixture.App.ClickAsync(Document);
    }

    // Disposing a view is a no-op: Notepad keeps running and the main App keeps working after the dialog is closed and its view disposed.
    [Fact]
    public async Task DisposingView_LeavesNotepadRunning()
    {
        var dialog = await OpenDialogAsync();
        await dialog.CloseWindowAsync();
        await dialog.WaitUntilGoneAsync(Cancel);

        await dialog.DisposeAsync();

        Assert.False(Process.GetProcessById(fixture.App.ProcessId).HasExited);
        await fixture.App.ClickAsync(Document);
    }

    // Opens Notepad's Open dialog with Ctrl+O and returns a view rooted at it.
    private async Task<App> OpenDialogAsync()
    {
        await fixture.App.HotkeyAsync(new KeyModifiers(Control: true), [Key.O]);
        return await fixture.App.WaitForWindowAsync(new WindowMatchOptions { TitleContains = "Open" });
    }
}
