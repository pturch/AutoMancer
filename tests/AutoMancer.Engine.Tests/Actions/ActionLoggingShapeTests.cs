// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Reflection;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Tests.Actions;

// Guards against a new Actions method silently losing trace visibility — logs either directly (IEngineLogger? param) or via a carrier object (ElementHandle/AppSession).
public sealed class ActionLoggingShapeTests
{
    // Methods with no log path at all (pure reads: ScreenshotAction, WindowAction.GetSizeAsync), plus every public method whose parameters are all raw coordinates/keys/handles — those log only through an internal *CoreAsync sibling, since none of their parameter types can carry a logger the way ElementHandle can.
    private static readonly HashSet<string> Exempt =
    [
        "ScreenshotAction.CaptureAsync",
        "WindowAction.GetSizeAsync",
        "WindowAction.MoveAsync",
        "WindowAction.ResizeAsync",
        "WindowAction.SetVisualStateAsync",
        "WindowAction.CloseAsync",
        "KeyboardAction.PressKeyAsync",
        "KeyboardAction.HotkeyAsync",
        "KeyboardAction.KeyDownAsync",
        "KeyboardAction.KeyUpAsync",
        "MouseMoveAction.MoveRelativeAsync",
        "DragAction.DragThroughAsync",
        "DragAction.DragAsync",
    ];

    [Fact]
    public void EveryActionMethod_HasALogPathUnlessExempt()
    {
        var offenders = typeof(ClickAction).Assembly.GetTypes()
            .Where(t => t.IsClass && t.IsPublic && t.Namespace == "AutoMancer.Engine.Actions")
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => !Exempt.Contains($"{m.DeclaringType!.Name}.{m.Name}"))
            .Where(m => !m.GetParameters().Any(p =>
                p.ParameterType == typeof(IEngineLogger) ||
                p.ParameterType == typeof(ElementHandle) ||
                p.ParameterType == typeof(AppSession)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        Assert.True(offenders.Count == 0,
            $"These Actions methods have no log path — thread IEngineLogger? through, or take ElementHandle/AppSession, or add to Exempt with a reason: {string.Join(", ", offenders)}");
    }
}
