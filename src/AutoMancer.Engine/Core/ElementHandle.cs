// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Core;

// A logical-pixel bounding box, used for element rects and window geometry.
public readonly record struct Rect(double X, double Y, double Width, double Height);

// An opaque, resolved reference to a found UI element; only Id and NativeHandle are public per the daemon's storage contract.
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

    // The provider that resolved this element — retained for re-finding and diagnostics.
    internal IElementProvider? Provider { get; init; }

    // The operator for this element's native handle type — null for Win32 and Visual elements, which fall back to SendInput.
    internal IElementOperator? Operator { get; init; }

    // The logger this element was resolved with, if any — stamped by ElementResolver so actions can log without taking a separate logger parameter.
    internal IEngineLogger? Logger { get; set; }

    // Constructed by providers only — callers receive handles exclusively from the resolver.
    internal ElementHandle(string id, string resolvedVia, object nativeHandle)
    {
        Id = id;
        ResolvedVia = resolvedVia;
        NativeHandle = nativeHandle;
    }
}
