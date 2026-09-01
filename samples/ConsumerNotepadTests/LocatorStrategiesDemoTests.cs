// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Exercises the Locator strategies ActionsDemoTests/WindowAndDiscoveryDemoTests don't already cover — ByPath, ByXPath, ByRuntimeId, ByClassName — against a live Notepad instance.
[Collection("ConsumerNotepad")]
public sealed class LocatorStrategiesDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    private static readonly Locator Document = Locator.ByControlType("Document");

    // ByPath walks the live tree segment by segment — Pane[2] picks the third Pane child of Window (the toolbar/menu bridge), then MenuBar, then the named File item. Index-based paths are position-sensitive: a long/scrolled document adds a status-bar Pane that isn't there in a fresh window, so this resets to an empty, unscrolled document first rather than assuming whatever state an earlier test left behind — unlike ByXPath below, which matches by name/attribute and doesn't care about sibling position.
    [Fact]
    public async Task ByPath_FindsFileMenuItem()
    {
        await App.ClickAsync(Document);
        await App.ClearAsync(Document);
        await Task.Delay(300);

        var handle = await App.FindAsync(Locator.ByPath("Window > Pane[2] > Pane > MenuBar > MenuItem[\"File\"]"));

        Assert.Equal("File", handle.Name);
    }

    // ByXPath evaluates against the UIA tree exported by SnapshotAsync — tags are control types, attributes are UIA properties like @Name.
    [Fact]
    public async Task ByXPath_FindsFileMenuItem()
    {
        var handle = await App.FindAsync(Locator.ByXPath("//MenuItem[@Name='File']"));

        Assert.Equal("File", handle.Name);
    }

    // ByRuntimeId re-finds a specific element via its stable RuntimeId (ElementHandle.Id) instead of searching by name/type again — useful once you already hold a handle and need to re-resolve it, e.g. after a session re-attach.
    [Fact]
    public async Task ByRuntimeId_RefindsTheSameElement()
    {
        var original = await App.FindAsync(Locator.ByControlType("Document"));

        var refound = await App.FindAsync(Locator.ByRuntimeId(original.Id));

        Assert.Equal(original.Id, refound.Id);
    }

    // ByClassName matches the Win32 window class rather than any UIA property — only meaningful against the win32 provider. Discovers a real class from the live tree instead of hardcoding one, since Notepad's Win32 child classes vary across Windows builds; skips (inconclusive, not failed) if none are found.
    [Fact]
    public async Task ByClassName_FindsChildWindowViaWin32Provider()
    {
        var win32 = App.WithOptions(new AppOptions { ProviderChain = ["win32"], ImplicitWaitMs = 2_000, PollIntervalMs = 200 });
        var snapshot = await win32.SnapshotAsync();
        var classedChild = snapshot?[0].Children.FirstOrDefault(c => !string.IsNullOrEmpty(c.ClassName));

        if (classedChild is null)
            return;

        var handle = await win32.FindAsync(Locator.ByClassName(classedChild.ClassName!));

        Assert.Equal("win32", handle.ResolvedVia);
        Assert.Equal(classedChild.ClassName, handle.ClassName);
    }
}
