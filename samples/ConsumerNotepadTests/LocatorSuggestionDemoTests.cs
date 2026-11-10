// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;
using Xunit.Abstractions;

namespace ConsumerNotepadTests;

// Shows SuggestLocatorAsync: given an element you already hold, the strongest locator that matches only that element.
[Collection("ConsumerNotepad")]
public sealed class LocatorSuggestionDemoTests(NotepadFixture fixture, ITestOutputHelper output) : AutoMancerTest(fixture)
{
    // The suggestion for the File menu item is a locator you can paste into a test; IsStable says it rests on an identity (AutomationId or Name), not on where the element sits in the tree.
    [Fact]
    public async Task SuggestLocatorAsync_NamedElement_GivesStableLocatorThatFindsIt()
    {
        var file = await App.FindAsync(Locator.ByXPath("//MenuItem[@Name='File']"));

        var suggestion = await App.SuggestLocatorAsync(file);

        Assert.NotNull(suggestion);
        output.WriteLine($"File menu item -> {suggestion.Locator.Strategy} = {suggestion.Locator.Value} (stable: {suggestion.IsStable})");
        Assert.True(suggestion.IsStable);
        var found = await App.FindAsync(suggestion.Locator);
        Assert.Equal(file.Name, found.Name);
        Assert.Equal(file.ControlType, found.ControlType);
    }

    // The consumer workflow: reach an element however is easiest while writing the test (here its control type), ask for a locator once, and use that locator from then on — in a real suite you'd paste it into a page object as a constant instead of keeping the lookup.
    [Fact]
    public async Task SuggestLocatorAsync_FoundByControlType_ReplacedByTheSuggestedLocator()
    {
        var document = await App.FindAsync(Locator.ByControlType("Document"));
        var suggestion = await App.SuggestLocatorAsync(document);
        Assert.NotNull(suggestion);
        output.WriteLine($"Paste into your page object: Locator.By{suggestion.Locator.Strategy}(\"{suggestion.Locator.Value}\")");

        await App.ClickAsync(suggestion.Locator);
        await App.TypeAsync(suggestion.Locator, "Typed through the suggested locator");
        await Expect(Locator.ByControlType("TitleBar")).ToContainTextAsync("Typed through");

        await App.ClearAsync(suggestion.Locator);
    }

    // An element with nothing unique to name it still gets a locator, but IsStable is false: it depends on the rest of the tree staying as it is. Treat those as a prompt to ask the app's developers for an AutomationId.
    [Fact]
    public async Task SuggestLocatorAsync_ReportsWhichSuggestionsAreFragile()
    {
        var items = await App.FindAllAsync(Locator.ByControlType("MenuItem"));

        foreach (var item in items)
        {
            var suggestion = await App.SuggestLocatorAsync(item);
            output.WriteLine($"{item.ControlType} '{item.Name}' -> {(suggestion is null ? "none" : $"{suggestion.Locator.Strategy} = {suggestion.Locator.Value} (stable: {suggestion.IsStable})")}");
            if (suggestion is not null)
                Assert.Equal(item.Name, (await App.FindAsync(suggestion.Locator)).Name);
        }

        Assert.NotEmpty(items);
    }
}
