// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
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

    // Constructed by providers only — callers receive handles exclusively from the resolver.
    internal ElementHandle(string id, string resolvedVia, object nativeHandle)
    {
        Id = id;
        ResolvedVia = resolvedVia;
        NativeHandle = nativeHandle;
    }
}
