// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Tests.Integration;

// Stage 7 extended-interaction tests, exercised against a live Notepad Document control.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class NotepadInteractionTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    private readonly App _app = fixture.App.WithOptions(new AppOptions { ProviderChain = ["uia3"] });

    // Double-clicking a word selects it; DoubleClickAction targets a synthetic handle over the word's rect (via TextPattern.FindText) since a locator would only give the whole Document.
    [Fact]
    public async Task DoubleClickAsync_SelectsWordUnderCursor()
    {
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await _app.ClearAsync(Locator.ByControlType("Document"));
        await _app.TypeAsync(Locator.ByControlType("Document"), "Hello AutoMancer");
        await Task.Delay(200);

        var (textPattern, uiaElement) = await GetTextPatternAsync();
        var wordRange = textPattern.DocumentRange.FindText("AutoMancer", 0, 0);
        Assert.NotNull(wordRange);

        var rects = wordRange.GetBoundingRectangles();
        Assert.True(rects.Length >= 4);
        var wordCenter = new ElementHandle("word-AutoMancer", "test", uiaElement)
        {
            BoundingRect = new Rect(rects[0], rects[1], rects[2], rects[3]),
        };

        await DoubleClickAction.ExecuteAsync(wordCenter);
        await Task.Delay(200);

        var selection = textPattern.GetSelection();
        Assert.True(selection.Length > 0);
        Assert.Equal("AutoMancer", selection.GetElement(0).GetText(-1));
    }

    // Ctrl+A should select the entire document contents.
    [Fact]
    public async Task PressKeyAsync_CtrlA_SelectsAllText()
    {
        const string text = "Hello AutoMancer";
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await _app.ClearAsync(Locator.ByControlType("Document"));
        await _app.TypeAsync(Locator.ByControlType("Document"), text);
        await Task.Delay(200);

        await _app.PressKeyAsync(Key.A, new KeyModifiers(Control: true));
        await Task.Delay(200);

        var (textPattern, _) = await GetTextPatternAsync();
        var selection = textPattern.GetSelection();

        Assert.True(selection.Length > 0);
        Assert.Equal(text, selection.GetElement(0).GetText(-1));
    }

    // Typing "AB" while holding Shift across both keys in one HotkeyAsync call proves the modifier stays active for the second key, not released after the first.
    [Fact]
    public async Task HotkeyAsync_MultipleKeys_HoldsModifierAcrossAllOfThem()
    {
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await _app.ClearAsync(Locator.ByControlType("Document"));
        await Task.Delay(100);

        await _app.HotkeyAsync(new KeyModifiers(Shift: true), [Key.A, Key.B]);
        await Task.Delay(200);

        var (textPattern, _) = await GetTextPatternAsync();
        Assert.Equal("AB", textPattern.DocumentRange.GetText(-1));
    }

    // Right-clicking the document opens its context menu; since WinUI3 Notepad pre-loads menu items into the UIA tree, focus moving off the Document is the reliable signal.
    [Fact]
    public async Task RightClickAsync_OpensContextMenu()
    {
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await Task.Delay(100);

        await _app.RightClickAsync(Locator.ByControlType("Document"));
        await Task.Delay(500);

        var focusedControlType = await Task.Run(() =>
        {
            var automation = new CUIAutomation8Class();
            return automation.GetFocusedElement()?.CurrentControlType ?? -1;
        });
        Assert.NotEqual(UIA_ControlTypeIds.UIA_DocumentControlTypeId, focusedControlType);

        NotepadFixture.SendEscape();
        await Task.Delay(200);
    }

    // Shift+click extends the selection from the caret to the click point: Key.Home sets a known start, then a Shift+click past the last character selects the whole line.
    [Fact]
    public async Task ClickAsync_WithShiftModifier_ExtendsSelectionToClickPoint()
    {
        const string text = "Hello AutoMancer";
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await _app.ClearAsync(Locator.ByControlType("Document"));
        await _app.TypeAsync(Locator.ByControlType("Document"), text);
        await Task.Delay(400);

        await _app.PressKeyAsync(Key.Home);
        await Task.Delay(100);

        var (textPattern, uiaElement) = await GetTextPatternAsync();
        var wordRange = textPattern.DocumentRange.FindText("AutoMancer", 0, 0);
        Assert.NotNull(wordRange);
        var rects = wordRange.GetBoundingRectangles();
        Assert.True(rects.Length >= 4);
        Assert.True(rects[2] > 50, $"Unexpectedly narrow word rect (width={rects[2]}) — text may not have finished rendering.");

        // A point just past the word's right edge — resolves to the boundary right after the last character.
        var endOfText = new ElementHandle("end-of-text", "test", uiaElement)
        {
            BoundingRect = new Rect(rects[0] + rects[2] + 2, rects[1] + rects[3] / 2, 0, 0),
        };

        await ClickAction.ExecuteAsync(endOfText, MouseButton.Left, new KeyModifiers(Shift: true));
        await Task.Delay(200);

        var selection = textPattern.GetSelection();
        Assert.True(selection.Length > 0);
        Assert.Equal(text, selection.GetElement(0).GetText(-1));
    }

    // Ctrl+click places the caret at the click point same as a plain click, proving the modifier doesn't interfere with placement; verified by typing a marker character and checking where it landed.
    [Fact]
    public async Task ClickAsync_WithControlModifier_PlacesCaretAtClickPoint()
    {
        const string text = "Hello AutoMancer";
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await _app.ClearAsync(Locator.ByControlType("Document"));
        await _app.TypeAsync(Locator.ByControlType("Document"), text);
        await Task.Delay(400);

        var (textPattern, uiaElement) = await GetTextPatternAsync();
        var wordRange = textPattern.DocumentRange.FindText("AutoMancer", 0, 0);
        Assert.NotNull(wordRange);
        var rects = wordRange.GetBoundingRectangles();
        Assert.True(rects.Length >= 4);

        // A point just inside the left edge of "AutoMancer" — resolves to the boundary right before the "A".
        var startOfWord = new ElementHandle("start-of-word", "test", uiaElement)
        {
            BoundingRect = new Rect(rects[0] + 1, rects[1] + rects[3] / 2, 0, 0),
        };

        await ClickAction.ExecuteAsync(startOfWord, MouseButton.Left, new KeyModifiers(Control: true));
        await Task.Delay(200);

        await _app.TypeDirectAsync("X");
        await Task.Delay(200);

        Assert.Equal("Hello XAutoMancer", textPattern.DocumentRange.GetText(-1));
    }

    // HoverAsync moves the mouse without clicking; since Notepad has no reliable hover-triggered visual state, this verifies the OS cursor position via GetCursorPos instead.
    [Fact]
    public async Task HoverAsync_MovesCursorToElementCenter()
    {
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await Task.Delay(100);

        var fileButton = await _app.FindAsync(Locator.ByName("File"));
        var expectedX = (int)(fileButton.BoundingRect.X + fileButton.BoundingRect.Width / 2);
        var expectedY = (int)(fileButton.BoundingRect.Y + fileButton.BoundingRect.Height / 2);

        await _app.HoverAsync(Locator.ByName("File"));
        await Task.Delay(100);

        NativeMethods.GetCursorPos(out var cursor);
        // Small tolerance for SendInput's 0-65535 normalization round-trip rounding.
        Assert.True(Math.Abs(cursor.X - expectedX) <= 2, $"Cursor X {cursor.X} not within tolerance of expected {expectedX}.");
        Assert.True(Math.Abs(cursor.Y - expectedY) <= 2, $"Cursor Y {cursor.Y} not within tolerance of expected {expectedY}.");
    }

    // MoveMouseRelativeAsync repositions the cursor by a pixel delta from wherever it currently is, unlike HoverAsync's absolute positioning. Asserts direction and rough magnitude only, not an exact pixel count: relative SendInput moves pass through the OS pointer-acceleration/speed curve, which was observed on this machine to roughly double the requested delta — that scaling is a per-machine mouse setting, not something this code controls.
    [Fact]
    public async Task MoveMouseRelativeAsync_MovesCursorByDelta()
    {
        await _app.HoverAsync(Locator.ByName("File"));
        await Task.Delay(100);
        NativeMethods.GetCursorPos(out var before);

        await _app.MoveMouseRelativeAsync(30, -15);
        await Task.Delay(100);

        NativeMethods.GetCursorPos(out var after);
        var (deltaX, deltaY) = (after.X - before.X, after.Y - before.Y);
        Assert.True(deltaX is > 5 and < 200, $"Unexpected X delta: {deltaX}");
        Assert.True(deltaY is < -5 and > -200, $"Unexpected Y delta: {deltaY}");
    }

    // Scroll wheel notches move the document's vertical scroll position; skipped as inconclusive if Document isn't scrollable, since that depends on window size/font/DPI.
    [Fact]
    public async Task ScrollWheelAsync_ScrollsDocumentView()
    {
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await _app.ClearAsync(Locator.ByControlType("Document"));
        var manyLines = string.Join("\r", Enumerable.Range(1, 100).Select(i => $"Line {i}"));
        await _app.TypeAsync(Locator.ByControlType("Document"), manyLines);
        await Task.Delay(300);

        var editor = await _app.FindAsync(Locator.ByControlType("Document"));
        var uiaElement = (IUIAutomationElement)editor.NativeHandle;
        if (uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_ScrollPatternId) is not IUIAutomationScrollPattern scrollPattern
            || scrollPattern.CurrentVerticallyScrollable == 0)
            return;

        var before = scrollPattern.CurrentVerticalScrollPercent;

        await _app.ScrollWheelAsync(Locator.ByControlType("Document"), deltaX: 0, deltaY: -5);
        await Task.Delay(300);

        Assert.True(scrollPattern.CurrentVerticalScrollPercent > before);
    }

    // SetFocusAsync moves keyboard focus to the target element without clicking it.
    [Fact]
    public async Task SetFocusAsync_SetsKeyboardFocusToElement()
    {
        await _app.ClickAsync(Locator.ByControlType("Document"));
        await Task.Delay(100);

        await _app.SetFocusAsync(Locator.ByName("File"));
        await Task.Delay(200);

        var fileButton = await _app.FindAsync(Locator.ByName("File"));
        var uiaElement = (IUIAutomationElement)fileButton.NativeHandle;
        Assert.NotEqual(0, uiaElement.CurrentHasKeyboardFocus);

        await _app.ClickAsync(Locator.ByControlType("Document"));
    }

    // Resolves the live Document element and its TextPattern together, since callers need both for reads and for building synthetic click targets from TextRange bounding rects.
    private async Task<(IUIAutomationTextPattern TextPattern, IUIAutomationElement Element)> GetTextPatternAsync()
    {
        var editor = await _app.FindAsync(Locator.ByControlType("Document"));
        var uiaElement = (IUIAutomationElement)editor.NativeHandle;
        var textPattern = (IUIAutomationTextPattern)uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_TextPatternId);
        return (textPattern, uiaElement);
    }
}
