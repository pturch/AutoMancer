// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Integration;

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

    // Kills the Notepad instance launched for the test that just ran.
    public async Task DisposeAsync()
    {
        _session.KillApp();
        await _session.DisposeAsync();
    }

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
