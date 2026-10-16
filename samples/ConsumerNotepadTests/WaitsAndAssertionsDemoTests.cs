// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Demonstrates WaitConditions' canned predicates for WaitForAsync, and the Expect assertions built on the same rules.
[Collection("ConsumerNotepad")]
public sealed class WaitsAndAssertionsDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    private static readonly Locator FileMenu = Locator.ByName("File");
    private static readonly Locator Tabs = Locator.ByControlType("TabItem");

    // WaitConditions replaces hand-written lambdas; IsVisible/IsEnabled are the exact rules actions wait on before acting, so a wait that passes means an action won't block.
    [Fact]
    public async Task WaitForAsync_WithWaitConditions_ReturnsTheMatchedElement()
    {
        var file = await App.WaitForAsync(FileMenu, WaitConditions.IsVisible());
        await App.WaitForAsync(FileMenu, WaitConditions.IsEnabled());
        await App.WaitForAsync(FileMenu, WaitConditions.ControlTypeEquals("MenuItem"));

        Assert.Equal("File", file.Name);
    }

    // Not() flips any condition — the shape for waiting on a spinner or overlay to clear rather than appear.
    [Fact]
    public async Task WaitForAsync_WithNot_PassesWhenConditionIsFalse()
        => await App.WaitForAsync(FileMenu, WaitConditions.Not(WaitConditions.NameEquals("Edit")));

    // ToBeEnabledAsync and ToBeVisibleAsync retry until the element is enabled/visible by the same rules actions use.
    [Fact]
    public async Task Expect_ToBeEnabledAsync_And_ToBeVisibleAsync()
    {
        await Expect(FileMenu).ToBeEnabledAsync();
        await Expect(FileMenu).ToBeVisibleAsync();
    }

    // ToContainTextAsync retries until Name contains a substring — here the title bar, which Notepad updates from the typed text a moment after typing.
    [Fact]
    public async Task Expect_ToContainTextAsync_WaitsForTitleBarToUpdate()
    {
        var document = Locator.ByControlType("Document");
        await App.ClearAsync(document);
        await App.TypeAsync(document, "Waits demo");
        try
        {
            await Expect(Locator.ByControlType("TitleBar")).ToContainTextAsync("Waits demo");
        }
        finally
        {
            await App.ClearAsync(document);
        }
    }

    // ToHaveCountAsync waits for a collection to reach a size — the wait single-pass FindAllAsync deliberately leaves out, so a new tab still being added doesn't race the assertion.
    [Fact]
    public async Task Expect_ToHaveCountAsync_WaitsForNewTab()
    {
        var before = (await App.FindAllAsync(Tabs)).Count;
        await App.PressKeyAsync(Key.N, new KeyModifiers(Control: true));
        try
        {
            await Expect(Tabs).ToHaveCountAsync(before + 1);
        }
        finally
        {
            // An empty tab closes without a save prompt.
            await App.PressKeyAsync(Key.W, new KeyModifiers(Control: true));
        }
        await Expect(Tabs).ToHaveCountAsync(before);
    }
}
