// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Core;

// A logical-pixel bounding box, used for element rects and window geometry.
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    // The midpoint of this rect, in the same coordinate space as X/Y.
    public (double X, double Y) Center => (X + Width / 2, Y + Height / 2);
}
