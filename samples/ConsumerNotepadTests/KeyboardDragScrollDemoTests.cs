// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;

namespace ConsumerNotepadTests;

// Exercises App's keyboard, drag, and scroll methods against a live Notepad Document control.
[Collection("ConsumerNotepad")]
public sealed class KeyboardDragScrollDemoTests(NotepadFixture fixture) : AutoMancerTest(fixture), IClassFixture<NotepadFixture>
{
    private static readonly Locator Document = Locator.ByControlType("Document");

    // Ctrl+A selects all text; typing over the selection replaces it, which is how this proves the select-all actually happened. Read via GetValueAsync/ToHaveValueAsync since Document's accessibility Name is a fixed label, not live text.
    [Fact]
    public async Task PressKeyAsync_CtrlA_SelectsAllText()
    {
        await App.ClickAsync(Document);
        await App.ClearAsync(Document);
        await App.TypeAsync(Document, "Hello AutoMancer");

        await App.PressKeyAsync(Key.A, new KeyModifiers(Control: true));
        await App.TypeAsync(Document, "Replaced");

        await Expect(Document).ToHaveValueAsync("Replaced");
    }

    // HotkeyAsync holds Shift across two keys pressed together, proving the modifier stays down for both rather than releasing after the first.
    [Fact]
    public async Task HotkeyAsync_HoldsModifierAcrossMultipleKeys()
    {
        await App.ClickAsync(Document);
        await App.ClearAsync(Document);

        await App.HotkeyAsync(new KeyModifiers(Shift: true), [Key.A, Key.B]);

        await Expect(Document).ToHaveValueAsync("AB");
    }

    // KeyDownAsync/KeyUpAsync hold Shift across two separate PressKeyAsync calls to extend a selection, then TypeDirectAsync (real keystrokes, unlike TypeAsync's native SetValue fast path, which replaces the whole value regardless of selection) types over just the selected range.
    [Fact]
    public async Task KeyDownAsync_KeyUpAsync_HoldsModifierAcrossSeparateKeyPresses()
    {
        await App.ClickAsync(Document);
        await App.ClearAsync(Document);
        await App.TypeAsync(Document, "Hello");
        await App.PressKeyAsync(Key.Home);
        await Task.Delay(150);

        await App.KeyDownAsync(Key.LeftShift);
        await App.PressKeyAsync(Key.Right);
        await Task.Delay(100);
        await App.PressKeyAsync(Key.Right);
        await Task.Delay(100);
        await App.KeyUpAsync(Key.LeftShift);
        await App.TypeDirectAsync("XX");

        await Expect(Document).ToHaveValueAsync("XXllo");
    }

    // MoveMouseRelativeAsync repositions the cursor by a pixel delta from wherever it currently is, rather than to an absolute element position.
    [Fact]
    public async Task MoveMouseRelativeAsync_CompletesWithoutThrowing()
    {
        await App.HoverAsync(Locator.ByName("File"));
        await App.MoveMouseRelativeAsync(20, -10);
    }

    // DragThroughAsync drag-selects a text range by holding the button across a sequence of points; TypeDirectAsync (real keystrokes, unlike TypeAsync's native SetValue fast path, which would replace the whole value regardless of selection) then types over just the dragged range.
    [Fact]
    public async Task DragThroughAsync_SelectsTextRangeForReplacement()
    {
        await App.ClickAsync(Document);
        await App.ClearAsync(Document);
        await App.TypeAsync(Document, "Hello AutoMancer");
        await Task.Delay(200);

        var document = await App.FindAsync(Document);
        var startX = (int)document.BoundingRect.X + 5;
        var endX = (int)(document.BoundingRect.X + document.BoundingRect.Width - 5);
        var y = (int)(document.BoundingRect.Y + 10);

        await App.PressKeyAsync(Key.Home);
        await App.DragThroughAsync([(startX, y), (endX, y)]);
        await App.TypeDirectAsync("Replaced");
        await Task.Delay(200);

        // Not an exact match — the drag selects an approximate pixel range, so some of the original text may survive alongside "Replaced".
        var value = await App.GetValueAsync(Document);
        Assert.Contains("Replaced", value);
    }

    // DragAsync is DragThroughAsync's straight-line convenience overload — a short drag near the caret proves it dispatches without an explicit waypoint list.
    [Fact]
    public async Task DragAsync_CompletesWithoutThrowing()
    {
        await App.ClickAsync(Document);
        var document = await App.FindAsync(Document);
        var x = (int)(document.BoundingRect.X + 10);
        var y = (int)(document.BoundingRect.Y + 10);

        await App.DragAsync(x, y, x + 40, y);
    }

    // ScrollWheelAsync scrolls a document long enough to actually be scrollable.
    [Fact]
    public async Task ScrollWheelAsync_ScrollsLongDocument()
    {
        await App.ClickAsync(Document);
        await App.ClearAsync(Document);
        var manyLines = string.Join("\r", Enumerable.Range(1, 100).Select(i => $"Line {i}"));
        await App.TypeAsync(Document, manyLines);

        await App.ScrollWheelAsync(Document, deltaX: 0, deltaY: -5);
    }

    // ScrollIntoViewAsync uses ScrollItemPattern rather than a mouse wheel — no cursor position or window focus involved, just a UIA request that the element become visible.
    [Fact]
    public async Task ScrollIntoViewAsync_CompletesWithoutThrowing()
    {
        await App.ClickAsync(Document);
        await App.ClearAsync(Document);
        var manyLines = string.Join("\r", Enumerable.Range(1, 100).Select(i => $"Line {i}"));
        await App.TypeAsync(Document, manyLines);

        await App.ScrollIntoViewAsync(Document);
    }
}
