// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// What a provider must answer for locator suggestion: which node of its own snapshot a handle is, and whether two of its handles are one element — Id strings can't say, since one element's RuntimeId can differ by how it was reached.
internal interface IElementIdentity
{
    // Snapshots the tree and returns it with the snapshot Id of the node that is target; null if target didn't come from this provider or isn't in the tree.
    Task<(IReadOnlyList<ElementSnapshot> Tree, string TargetId)?> SnapshotLocatingAsync(ElementHandle target, AppSession session, CancellationToken ct = default);

    // True when both handles came from this provider and refer to the same element.
    bool IsSameElement(ElementHandle a, ElementHandle b);
}
