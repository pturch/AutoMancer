// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// A read-only, point-in-time copy of an element's properties and its full subtree, used for tree dumps and closest-match search.
public sealed record ElementSnapshot(
    string Id,
    string? Name,
    string? AutomationId,
    string? ClassName,
    string? ControlType,
    Rect BoundingRect,
    IReadOnlyList<ElementSnapshot> Children
);

// The contract every automation backend (UIA3, UIA2, Win32, Visual, ...) implements to find elements and snapshot the tree.
public interface IElementProvider
{
    string ProviderName { get; }

    // Returns the first element matching the locator anywhere in the session, or null if not found — never throws on not-found.
    Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default);

    // Returns all elements matching the locator anywhere in the session — empty list if none found.
    Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default);

    // Returns the first element matching the locator within scope's subtree, or null if not found — including when scope's NativeHandle came from a different provider and can't be honored.
    Task<ElementHandle?> FindScopedElementAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default);

    // Returns all elements matching the locator within scope's subtree — empty list if none found or scope came from a different provider.
    Task<IReadOnlyList<ElementHandle>> FindScopedElementsAsync(Locator locator, AppSession session, ElementHandle scope, CancellationToken ct = default);

    // Returns a read-only snapshot of the element tree rooted at the session's window.
    Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default);
}

