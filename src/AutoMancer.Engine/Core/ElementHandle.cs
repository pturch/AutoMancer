// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Core;

// An opaque, resolved reference to a found UI element; Id, NativeHandle, and read-only metadata are public, but Provider/Operator/Logger stay internal per the daemon's storage contract.
public sealed class ElementHandle
{
    public string Id { get; }
    public string ResolvedVia { get; }
    public object NativeHandle { get; }

    public string? Name { get; init; }
    public string? AutomationId { get; init; }
    public string? ClassName { get; init; }
    public string? ControlType { get; init; }
    public Rect BoundingRect { get; init; }

    // Whether the element currently accepts input; null when the resolving provider can't determine this (e.g. Win32 has no equivalent to UIA's IsOffscreen).
    public bool? IsEnabled { get; init; }
    public bool? IsOffscreen { get; init; }

    // The provider that resolved this element — retained for re-finding and diagnostics.
    internal IElementProvider? Provider { get; init; }

    // The operator for this element's native handle type — null for Win32 and Visual elements, which fall back to SendInput.
    internal IElementOperator? Operator { get; init; }

    // The logger this element was resolved with, if any — stamped by ElementResolver so actions can log without taking a separate logger parameter.
    internal IEngineLogger? Logger { get; set; }

    // How long actions should retry foregrounding this element's window before giving up — stamped by ElementResolver from AppOptions.ForegroundActivationTimeoutMs, so actions can read it without taking a separate parameter.
    internal int ForegroundActivationTimeoutMs { get; set; } = 3_000;

    // Constructed by providers only — callers receive handles exclusively from the resolver.
    internal ElementHandle(string id, string resolvedVia, object nativeHandle)
    {
        Id = id;
        ResolvedVia = resolvedVia;
        NativeHandle = nativeHandle;
    }
}
