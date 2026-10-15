// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using Interop.UIAutomationClient;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class NotepadIntegrationTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    // UIA3-only App — mirrors the old ElementResolver([new Uia3Provider()]) used before the App facade.
    private readonly App _app = fixture.App.WithOptions(new AppOptions { ProviderChain = ["uia3"] });

    // Windows 11's Notepad (WinUI3) exposes its text area as ControlType "Document", not the classic Win32 "Edit" control.
    [Fact]
    public async Task FindEditControl_ResolvesViaUia3()
    {
        var handle = await _app.FindAsync(Locator.ByControlType("Document"));

        Assert.Equal("uia3", handle.ResolvedVia);
        Assert.Equal("Document", handle.ControlType);
    }

    [Fact]
    public async Task AttachByTitleAsync_FindsTheLaunchedNotepad()
    {
        var title = Process.GetProcessById(fixture.App.ProcessId).MainWindowTitle;

        var attached = await App.AttachByTitleAsync(title);

        Assert.Equal(fixture.App.ProcessId, attached.ProcessId);
    }

    [Fact]
    public async Task AttachByPidAsync_WindowMatchMatchesRealWindow_Succeeds()
    {
        var title = Process.GetProcessById(fixture.App.ProcessId).MainWindowTitle;

        var attached = await AppSession.AttachByPidAsync(fixture.App.ProcessId, new WindowMatchOptions { TitleContains = title });

        Assert.Equal(fixture.App.ProcessId, attached.ProcessId);
    }

    [Fact]
    public async Task AttachByPidAsync_WindowMatchDoesNotMatch_ThrowsAppLaunchError()
    {
        var impossibleTitle = $"__AutoMancer_NoSuchWindow_{Guid.NewGuid()}";

        await Assert.ThrowsAsync<AppLaunchError>(() =>
            AppSession.AttachByPidAsync(fixture.App.ProcessId, new WindowMatchOptions { TitleContains = impossibleTitle }));
    }

    // Notepad is single-instance, so a plain LaunchAsync would hand off to fixture's already-running window; RequireNewWindow must reject that hand-off instead of returning it.
    [Fact]
    public async Task LaunchAsync_RequireNewWindow_RejectsHandoffToExistingWindow_ThrowsAppLaunchError()
    {
        await Assert.ThrowsAsync<AppLaunchError>(() =>
            AppSession.LaunchAsync("notepad.exe", timeoutMs: 2_000, windowMatch: new WindowMatchOptions { RequireNewWindow = true }));
    }

    [Fact]
    public async Task FindAsync_TypoInName_ThrowsWithClosestMatchHint()
    {
        var snapshot = await _app.SnapshotAsync();
        var realName = FindNameLongerThan(snapshot!, minLength: 5)
            ?? throw new InvalidOperationException("Notepad's element tree has no name long enough for a reliable typo test.");
        var typo = realName[..^1];

        var quick = fixture.App.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 300, PollIntervalMs = 100 });
        var ex = await Assert.ThrowsAsync<ElementNotFoundError>(() => quick.FindAsync(Locator.ByName(typo)));

        Assert.NotNull(ex.ClosestMatch);
        Assert.Equal(realName, ex.ClosestMatch!.ElementName);
    }

    [Fact]
    public async Task TypeInEditor_TextAppears()
    {
        const string text = "Hello AutoMancer";
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await _app.TypeAsync(Locator.ByControlType("Document"), text);

        await Expect(_app, Locator.ByControlType("Document")).ToHaveValueAsync(text);
    }

    [Fact]
    public async Task ClearEditor_RemovesTypedText()
    {
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await _app.TypeAsync(Locator.ByControlType("Document"), "text to clear");
        await Expect(_app, Locator.ByControlType("Document")).ToHaveValueAsync("text to clear");

        await _app.ClearAsync(Locator.ByControlType("Document"));

        await Expect(_app, Locator.ByControlType("Document")).ToHaveValueAsync("");
    }

    [Fact]
    public async Task ClickFileMenu_OpensMenu()
    {
        await _app.ClickAsync(Locator.ByControlType("Document"));

        await _app.ClickAsync(Locator.ByName("File"));

        // The menu's items only exist in the tree while it's open, so one appearing is the signal that clicking File worked.
        await _app.WaitForAsync(Locator.ByName("New tab"), WaitConditions.IsVisible());

        // Close the menu and wait for its items to leave the tree, so subsequent tests in this shared session aren't affected; focus stays on the File item, so it can't signal this.
        NotepadFixture.SendEscape();
        await _app.WaitUntilGoneAsync(Locator.ByName("New tab"));
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
