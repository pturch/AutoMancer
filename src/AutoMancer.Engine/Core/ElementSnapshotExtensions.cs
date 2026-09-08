// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// Flattening helpers for the tree SnapshotAsync/TrySnapshotAsync return, for callers that want to search the whole subtree rather than walk Children by hand.
public static class ElementSnapshotExtensions
{
    // Depth-first walk of this snapshot and every descendant beneath it, itself first.
    public static IEnumerable<ElementSnapshot> DescendantsAndSelf(this ElementSnapshot snapshot)
    {
        yield return snapshot;
        foreach (var child in snapshot.Children)
            foreach (var descendant in child.DescendantsAndSelf())
                yield return descendant;
    }

    // Depth-first walk of every root in roots and all of their descendants.
    public static IEnumerable<ElementSnapshot> DescendantsAndSelf(this IEnumerable<ElementSnapshot> roots) =>
        roots.SelectMany(r => r.DescendantsAndSelf());
}
