// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Integration;

// Covers FindByScrollingAsync end to end: locating a far-down row in a long, genuinely virtualized live list.
// Uses Notepad's native Open dialog (a classic Win32 common item dialog) pointed at a folder with hundreds of files —
// its file list is the "Items View" (AutomationId "1000"), a well-known, widely-relied-on control across Windows UI
// automation tooling for the common Open/Save dialog's virtualized file list.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class VirtualizedListFindIntegrationTests
{
    private const int FileCount = 300;
    private const string TargetFileName = "file-0250.txt";
    private const string LastFileName = "file-0299.txt";
    private const string FirstFileName = "file-0000.txt";

    // Each file's content marks its own name, so a test can open a file found via scrolling and confirm — via Document's actual text — that it opened the file it thinks it did, not just a same-named decoy.
    private static string CreateFolderWithManyFiles()
    {
        var folder = Path.Combine(Path.GetTempPath(), "amc-virtualized-list-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        for (var i = 0; i < FileCount; i++)
            File.WriteAllText(Path.Combine(folder, $"file-{i:D4}.txt"), $"contents of file-{i:D4}");
        return folder;
    }

    private static async Task<App> OpenFileDialogOnFolderAsync(App app, string folder)
    {
        await app.FindAsync(Locator.ByControlType("Document"));
        await app.HotkeyAsync(new KeyModifiers(Control: true), [Key.O]);

        var dialog = await App.FindDialogAsync(app.ProcessId, "Open");
        Assert.NotNull(dialog);

        await dialog!.TypeAsync(Locator.ByControlType("Edit"), folder);
        await dialog.PressKeyAsync(Key.Enter);
        await Task.Delay(500);

        return dialog;
    }

    [Fact]
    public async Task FindByScrollingAsync_FarDownItemInLongList_ScrollsUntilFound()
    {
        var folder = CreateFolderWithManyFiles();
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var dialog = await OpenFileDialogOnFolderAsync(app, folder);
            var itemsView = await dialog.FindAsync(Locator.ByAutomationId("1000"));

            var found = await dialog.FindByScrollingAsync(itemsView, Locator.ByName(TargetFileName), maxScrolls: 30);

            Assert.Equal(TargetFileName, found.Name);
        }
        finally
        {
            await app.KillAsync();
            try { Directory.Delete(folder, true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public async Task FindByScrollingAsync_ItemThatDoesNotExist_ThrowsElementNotFoundErrorInsteadOfHanging()
    {
        var folder = CreateFolderWithManyFiles();
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var dialog = await OpenFileDialogOnFolderAsync(app, folder);
            var itemsView = await dialog.FindAsync(Locator.ByAutomationId("1000"));

            await Assert.ThrowsAsync<ElementNotFoundError>(() =>
                dialog.FindByScrollingAsync(itemsView, Locator.ByName("this-file-does-not-exist.txt"), maxScrolls: 30));
        }
        finally
        {
            await app.KillAsync();
            try { Directory.Delete(folder, true); } catch { /* best-effort cleanup */ }
        }
    }

    // The common case where FindScopedAsync's very first (pre-scroll) attempt already succeeds — proves the happy path doesn't require scrolling, or ScrollWheelAction, to work at all.
    [Fact]
    public async Task FindByScrollingAsync_ItemAlreadyVisibleNearTop_FindsItWithoutScrolling()
    {
        var folder = CreateFolderWithManyFiles();
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var dialog = await OpenFileDialogOnFolderAsync(app, folder);
            var itemsView = await dialog.FindAsync(Locator.ByAutomationId("1000"));

            var found = await dialog.FindByScrollingAsync(itemsView, Locator.ByName(FirstFileName), maxScrolls: 30);

            Assert.Equal(FirstFileName, found.Name);
        }
        finally
        {
            await app.KillAsync();
            try { Directory.Delete(folder, true); } catch { /* best-effort cleanup */ }
        }
    }

    // A real item that exists but sits past what maxScrolls allows reaching — a caller-tuning mistake, distinct from the item genuinely not existing, but observably the same ElementNotFoundError (no hang either way).
    [Fact]
    public async Task FindByScrollingAsync_MaxScrollsTooSmallToReachRealItem_ThrowsElementNotFoundError()
    {
        var folder = CreateFolderWithManyFiles();
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var dialog = await OpenFileDialogOnFolderAsync(app, folder);
            var itemsView = await dialog.FindAsync(Locator.ByAutomationId("1000"));

            await Assert.ThrowsAsync<ElementNotFoundError>(() =>
                dialog.FindByScrollingAsync(itemsView, Locator.ByName(LastFileName), maxScrolls: 1));
        }
        finally
        {
            await app.KillAsync();
            try { Directory.Delete(folder, true); } catch { /* best-effort cleanup */ }
        }
    }

    // Proves the ElementHandle FindByScrollingAsync hands back is a genuinely live, interactable element (found within a scrolled-to row), not just a match against a stale snapshot — double-clicking it opens the actual file, confirmed by its content landing in Document.
    [Fact]
    public async Task FindByScrollingAsync_ThenDoubleClickTheResult_OpensTheCorrectFile()
    {
        var folder = CreateFolderWithManyFiles();
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var dialog = await OpenFileDialogOnFolderAsync(app, folder);
            var itemsView = await dialog.FindAsync(Locator.ByAutomationId("1000"));
            var found = await dialog.FindByScrollingAsync(itemsView, Locator.ByName(TargetFileName), maxScrolls: 30);

            await DoubleClickAction.ExecuteAsync(found);
            await Task.Delay(500);

            var content = await app.GetValueAsync(Locator.ByControlType("Document"));
            Assert.Equal("contents of file-0250", content);
        }
        finally
        {
            await app.KillAsync();
            try { Directory.Delete(folder, true); } catch { /* best-effort cleanup */ }
        }
    }
}
