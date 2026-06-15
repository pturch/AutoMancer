// Copyright (c) AutoMancer Contributors. Licensed under the MIT License.
namespace AutoMancer.Engine.Core;

public readonly record struct Rect(double X, double Y, double Width, double Height);

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
