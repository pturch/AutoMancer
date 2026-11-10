// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies that locators suggested for live Notepad elements resolve back to those same elements through the engine's own find path.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class LocatorSuggestionIntegrationTests
{
    // Notepad shows a "Busy" LoadingRing for a moment after launch, so the tree is read until that is gone and two consecutive snapshots agree.
    private static async Task<IReadOnlyList<ElementSnapshot>> SettledTreeAsync(App app)
    {
        static string Key(IReadOnlyList<ElementSnapshot> t) =>
            string.Join("\n", t.DescendantsAndSelf().Select(n => $"{n.Id}|{n.ControlType}|{n.AutomationId}|{n.Name}"));

        var deadline = DateTime.UtcNow.AddSeconds(15);
        var previous = await app.SnapshotAsync() ?? [];
        while (DateTime.UtcNow < deadline)
        {
            var current = await app.SnapshotAsync() ?? [];
            if (Key(current) == Key(previous) && !Key(current).Contains("LoadingRing"))
                return current;
            previous = current;
        }
        throw new TimeoutException("Notepad's element tree never settled.");
    }

    // Every on-screen node in a settled Notepad window gets a suggestion that FindAsync resolves back to that same element.
    [Fact]
    public async Task SuggestLocatorAsync_EveryVisibleElement_RoundTripsToSameElement()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var tree = await SettledTreeAsync(app);
            var failures = new List<string>();

            foreach (var node in tree.DescendantsAndSelf().Where(n => n.BoundingRect.Width > 0 && n.BoundingRect.Height > 0))
            {
                // The positional XPath is the handle source because it doesn't depend on RuntimeId strings, which differ for the same non-client element by how it was reached.
                var positional = LocatorSuggester.Candidates(node.Id, tree).Last().Locator;
                var handle = (await app.FindAllAsync(positional)).FirstOrDefault();
                if (handle is null) { failures.Add($"{node.ControlType} '{node.Name}': positional XPath {positional.Value} found nothing"); continue; }

                var suggestion = await app.SuggestLocatorAsync(handle);
                if (suggestion is null) { failures.Add($"{node.ControlType} '{node.Name}': no verified suggestion"); continue; }

                var shouldBeStable = suggestion.Locator.Strategy is LocatorStrategy.AutomationId or LocatorStrategy.Name;
                if (suggestion.IsStable != shouldBeStable)
                    failures.Add($"{node.ControlType} '{node.Name}': {suggestion.Locator.Strategy} suggestion reports IsStable={suggestion.IsStable}");

                var found = await app.FindAsync(suggestion.Locator);
                if (!((IElementIdentity)new Uia3Provider()).IsSameElement(found, handle))
                    failures.Add($"{node.ControlType} '{node.Name}': {suggestion.Locator.Strategy}={suggestion.Locator.Value} resolved to a different element");
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }
        finally
        {
            await app.KillAsync();
            await app.DisposeAsync();
        }
    }

    // A multi-match XPath returns every match in document order, the same nodes XPathEvaluator picks from the snapshot, from a single walk.
    [Fact]
    public async Task FindAllAsync_XPathWithManyMatches_ReturnsThemInDocumentOrder()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            var tree = await SettledTreeAsync(app);
            var all = tree.DescendantsAndSelf().ToList();
            var expected = XPathEvaluator.Evaluate("//MenuItem", tree).Order().Select(i => all[i].Name).ToList();
            Assert.True(expected.Count > 1, "Notepad should show several menu items.");

            var found = await app.FindAllAsync(Locator.ByXPath("//MenuItem"));

            Assert.Equal(expected, found.Select(h => h.Name).ToList());
        }
        finally
        {
            await app.KillAsync();
            await app.DisposeAsync();
        }
    }
}
