// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class MouseMoveActionTests
{
    // Relative moves must omit MouseEventAbsolute — its presence is what turns Dx/Dy from a delta into a normalized screen coordinate (see SendInputBuilders.MouseInputAt).
    [Theory]
    [InlineData(10, -5)]
    [InlineData(0, 0)]
    [InlineData(-1000, 1000)]
    public void RelativeMoveInput_SetsDeltaWithoutAbsoluteFlag(int dx, int dy)
    {
        var input = MouseMoveAction.RelativeMoveInput(dx, dy);

        Assert.Equal(NativeMethods.InputTypeMouse, input.Type);
        Assert.Equal(dx, input.Data.Mouse.Dx);
        Assert.Equal(dy, input.Data.Mouse.Dy);
        Assert.Equal(NativeMethods.MouseEventFlags.Move, input.Data.Mouse.Flags);
    }
}
