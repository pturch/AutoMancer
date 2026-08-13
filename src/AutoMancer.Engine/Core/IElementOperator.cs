// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// Optional contract for providers that can interact via native patterns (InvokePattern, ValuePattern) — Win32/Visual providers skip it and fall through to SendInput.
public interface IElementOperator
{
    // Attempts a native click via InvokePattern; returns false when the element does not expose the pattern.
    Task<bool> TryClickAsync(ElementHandle element, CancellationToken ct = default);

    // Attempts to set the element's value via ValuePattern; returns false when the element does not expose the pattern or is read-only.
    Task<bool> TrySetValueAsync(ElementHandle element, string value, CancellationToken ct = default);

    // Reads the element's current value via ValuePattern, falling back to TextPattern's full document text; returns null when neither pattern is supported.
    Task<string?> TryGetValueAsync(ElementHandle element, CancellationToken ct = default);
}
