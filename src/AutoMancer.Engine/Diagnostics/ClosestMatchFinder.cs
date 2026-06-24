// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Diagnostics;

// Produced by ClosestMatchFinder and attached to ElementNotFoundError when a near-match exists.
public sealed record ClosestMatch(string ElementName, double Confidence);

// Finds the closest-named element in a snapshot tree when an exact locator match fails, powering "Did you mean?" hints.
public static class ClosestMatchFinder
{
    private const double MinConfidence = 0.70;

    // Searches the element tree for the name closest to locator.Value; returns null when no match meets the threshold.
    public static ClosestMatch? Find(Locator locator, IReadOnlyList<ElementSnapshot> tree)
    {
        ClosestMatch? best = null;

        foreach (var snapshot in Flatten(tree))
        {
            if (string.IsNullOrEmpty(snapshot.Name)) continue;

            double confidence = Similarity(locator.Value, snapshot.Name);

            if (confidence >= MinConfidence && (best is null || confidence > best.Confidence))
                best = new ClosestMatch(snapshot.Name, confidence);
        }

        return best;
    }

    // Flattens a hierarchical snapshot tree into a depth-first sequence.
    private static IEnumerable<ElementSnapshot> Flatten(IReadOnlyList<ElementSnapshot> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }

    // Returns a 0–1 similarity score between two strings using case-insensitive Levenshtein distance.
    private static double Similarity(string a, string b)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();
        int dist = LevenshteinDistance(a, b);
        int maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 1.0 : 1.0 - (double)dist / maxLen;
    }

    // Computes the minimum edit distance between two strings using the standard DP algorithm.
    private static int LevenshteinDistance(string a, string b)
    {
        int m = a.Length, n = b.Length;
        int[] prev = new int[n + 1];
        int[] curr = new int[n + 1];

        for (int j = 0; j <= n; j++) prev[j] = j;

        for (int i = 1; i <= m; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= n; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[n];
    }
}
