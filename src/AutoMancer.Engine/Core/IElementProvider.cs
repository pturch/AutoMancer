// Copyright (c) AutoMancer Contributors. Licensed under the MIT License.
namespace AutoMancer.Engine.Core;

public sealed record ElementSnapshot(
    string Id,
    string? Name,
    string? AutomationId,
    string? ClassName,
    string? ControlType,
    Rect BoundingRect,
    IReadOnlyList<ElementSnapshot> Children
);

public interface IElementProvider
{
    string ProviderName { get; }

    // Returns the first element matching the locator, or null if not found — never throws on not-found.
    Task<ElementHandle?> FindElementAsync(Locator locator, AppSession session, CancellationToken ct = default);

    // Returns all elements matching the locator — empty list if none found.
    Task<IReadOnlyList<ElementHandle>> FindElementsAsync(Locator locator, AppSession session, CancellationToken ct = default);

    // Returns a read-only snapshot of the element tree rooted at the session's window.
    Task<IReadOnlyList<ElementSnapshot>> SnapshotTreeAsync(AppSession session, CancellationToken ct = default);
}
