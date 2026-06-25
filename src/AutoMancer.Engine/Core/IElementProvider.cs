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

// The contract every automation backend (UIA3, UIA2, Win32, ...) implements to find elements, snapshot the tree, and perform native interactions.
public interface IElementProvider
{
    string ProviderName { get; }

    // Returns the first element matching the locator, or null if not found — never throws on not-found.
    Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default);

    // Returns all elements matching the locator — empty list if none found.
    Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default);

    // Returns a read-only snapshot of the element tree rooted at the session's window.
    Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default);

    // Attempts a native click (e.g. InvokePattern) without synthesized mouse input; returns false when the provider cannot perform the click.
    Task<bool> TryClickAsync(ElementHandle element, CancellationToken ct = default);

    // Attempts to set the element's value via a native mechanism (e.g. ValuePattern); returns false when the provider cannot set the value.
    Task<bool> TrySetValueAsync(ElementHandle element, string value, CancellationToken ct = default);
}
