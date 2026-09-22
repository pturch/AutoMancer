// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Integration;

// Covers FindByScrollingAsync end to end via Notepad's Open dialog file list (ClassName "UIItemsView"), a genuinely virtualized live control.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class VirtualizedListFindIntegrationTests
{
    // ~7 rows are visible before any scroll and each wheel notch reveals ~3 more (this machine's WheelScrollLines); Target/Last are sized off that so a single notch (maxScrolls: 1, below) provably can't reach either one.
    private const int FileCount = 15;
    // Extension-less: Windows hides known file extensions by default, so a real "file-0010.txt" row's UIA Name is "file-0010".
    private const string TargetFileName = "file-0010";
    private const string LastFileName = "file-0014";
    private const string FirstFileName = "file-0000";

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

        // The "File name:" combo and its nested Edit both carry AutomationId "1148", and ByControlType("Edit") separately matches the file list's per-row rename-in-place cells first — either ambiguity misses the real target. ClassName "Edit" (the native Win32 class) is unique to the combo's actual text box.
        await dialog!.TypeAsync(Locator.ByClassName("Edit"), folder);
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
            var itemsView = await dialog.FindAsync(Locator.ByClassName("UIItemsView"));

            var found = await dialog.FindByScrollingAsync(itemsView, Locator.ByName(TargetFileName), maxScrolls: 5);

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
            var itemsView = await dialog.FindAsync(Locator.ByClassName("UIItemsView"));

            await Assert.ThrowsAsync<ElementNotFoundError>(() =>
                dialog.FindByScrollingAsync(itemsView, Locator.ByName("this-file-does-not-exist.txt"), maxScrolls: 5));
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
            var itemsView = await dialog.FindAsync(Locator.ByClassName("UIItemsView"));

            var found = await dialog.FindByScrollingAsync(itemsView, Locator.ByName(FirstFileName), maxScrolls: 5);

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
            var itemsView = await dialog.FindAsync(Locator.ByClassName("UIItemsView"));

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
            var itemsView = await dialog.FindAsync(Locator.ByClassName("UIItemsView"));
            var found = await dialog.FindByScrollingAsync(itemsView, Locator.ByName(TargetFileName), maxScrolls: 5);

            await DoubleClickAction.ExecuteAsync(found);
            await Task.Delay(500);

            var content = await app.GetValueAsync(Locator.ByControlType("Document"));
            Assert.Equal("contents of file-0010", content);
        }
        finally
        {
            await app.KillAsync();
            try { Directory.Delete(folder, true); } catch { /* best-effort cleanup */ }
        }
    }
}
