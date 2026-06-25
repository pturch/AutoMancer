// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Runtime.InteropServices;  // DllImport for KeybdEvent
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Tests.Integration;

[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class NotepadIntegrationTests : IAsyncLifetime
{
    private AppSession _session = null!;
    private ElementResolver _resolver = null!;

    // Launches a fresh Notepad instance before each test in this class.
    public async Task InitializeAsync()
    {
        _session = await AppSession.LaunchAsync("notepad.exe");
        _resolver = new ElementResolver([new Uia3Provider()], new ElementProviderOptions { ProviderChain = ["uia3"] });
    }

    // Kills the Notepad instance launched for the test that just ran; null-guards against a failed InitializeAsync (xUnit skips DisposeAsync when Init throws, but defensive here just in case).
    public async Task DisposeAsync()
    {
        if (_session is null) return;
        SendEscape();  // dismiss any open menu/popup so WinUI3 host processes don't linger after the kill
        _session.KillApp();
        // WinUI3 Notepad is single-instance: if the killed process hasn't fully exited before the next
        // test's LaunchAsync runs, the OS redirects the new launch into the dying window instead of starting fresh.
        await Task.Delay(800);
        await _session.DisposeAsync();
    }

    // Sends a bare Escape keystroke so any open menu popup closes before the process is killed.
    private static void SendEscape()
    {
        KeybdEvent(0x1B, 0, 0, IntPtr.Zero);  // VK_ESCAPE down
        KeybdEvent(0x1B, 0, 2, IntPtr.Zero);  // VK_ESCAPE up (KEYEVENTF_KEYUP = 0x0002)
    }

    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void KeybdEvent(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    // Windows 11's Notepad (WinUI3) exposes its text area as ControlType "Document", not the classic Win32 "Edit" control.
    [Fact]
    public async Task FindEditControl_ResolvesViaUia3()
    {
        var handle = await _resolver.FindAsync(Locator.ByControlType("Document"), _session);

        Assert.Equal("uia3", handle.ResolvedVia);
        Assert.Equal("Document", handle.ControlType);
    }

    [Fact]
    public async Task AttachByTitleAsync_FindsTheLaunchedNotepad()
    {
        var title = Process.GetProcessById(_session.ProcessId).MainWindowTitle;

        var attached = await AppSession.AttachByTitleAsync(title);

        Assert.Equal(_session.ProcessId, attached.ProcessId);
    }

    [Fact]
    public async Task FindAsync_TypoInName_ThrowsWithClosestMatchHint()
    {
        var provider = new Uia3Provider();
        var tree = await provider.SnapshotTreeAsync(_session);
        var realName = FindNameLongerThan(tree, minLength: 5)
            ?? throw new InvalidOperationException("Notepad's element tree has no name long enough for a reliable typo test.");
        var typo = realName[..^1];

        var resolver = new ElementResolver([provider], new ElementProviderOptions { ImplicitWaitMs = 300, PollIntervalMs = 100, ProviderChain = ["uia3"] });

        var ex = await Assert.ThrowsAsync<ElementNotFoundError>(() => resolver.FindAsync(Locator.ByName(typo), _session));

        Assert.NotNull(ex.ClosestMatch);
        Assert.Equal(realName, ex.ClosestMatch!.ElementName);
    }

    [Fact]
    public async Task TypeInEditor_TextAppears()
    {
        const string text = "Hello AutoMancer";
        var editor = await _resolver.FindAsync(Locator.ByControlType("Document"), _session);

        await ClickAction.ExecuteAsync(editor);
        await TypeAction.ExecuteAsync(editor, text);
        await Task.Delay(200);

        // Read typed text back via IUIAutomationTextPattern — the reliable way to verify editor content.
        var uiaElement = (IUIAutomationElement)editor.NativeHandle;
        var textPattern = (IUIAutomationTextPattern)uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_TextPatternId);
        var content = textPattern.DocumentRange.GetText(-1);

        Assert.Contains(text, content);
    }

    [Fact]
    public async Task ClickFileMenu_OpensMenu()
    {
        // Establish a known focus baseline: editor has focus.
        var editor = await _resolver.FindAsync(Locator.ByControlType("Document"), _session);
        await ClickAction.ExecuteAsync(editor);
        await Task.Delay(100);

        var fileMenu = await _resolver.FindAsync(Locator.ByName("File"), _session);
        await ClickAction.ExecuteAsync(fileMenu);
        await Task.Delay(500);

        // WinUI3 Notepad pre-loads all menu items into the UIA tree so popup-window counting won't change.
        // The reliable signal that clicking File worked is that focus moved off the Document control.
        var focusedControlType = await Task.Run(() =>
        {
            var automation = new CUIAutomation8Class();
            return automation.GetFocusedElement()?.CurrentControlType ?? -1;
        });
        Assert.NotEqual(UIA_ControlTypeIds.UIA_DocumentControlTypeId, focusedControlType);
    }

    // Depth-first search for the first element name at least minLength characters long.
    private static string? FindNameLongerThan(IReadOnlyList<ElementSnapshot> nodes, int minLength)
    {
        foreach (var node in nodes)
        {
            if (node.Name is { Length: var len } name && len >= minLength)
                return name;

            var fromChildren = FindNameLongerThan(node.Children, minLength);
            if (fromChildren is not null)
                return fromChildren;
        }

        return null;
    }
}
