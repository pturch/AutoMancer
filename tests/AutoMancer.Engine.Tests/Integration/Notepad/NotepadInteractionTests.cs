// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Stage 7 extended-interaction tests, exercised against a live Notepad Document control. No fixed delays: keyboard and mouse input share one ordered queue, so input needs no spacing, and every read of resulting state polls until it holds.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class NotepadInteractionTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    private static readonly Locator Document = Locator.ByControlType("Document");
    private readonly App _app = fixture.App.WithOptions(new AppOptions { ProviderChain = ["uia3"] });

    // Double-clicking a word selects it; clicks the center of the word's rect (via TextPattern.FindText) twice by point, since a locator would only give the whole Document — ActionDelayMs (150ms by default) keeps the pair well inside Windows' double-click time.
    [Fact]
    public async Task DoubleClickAsync_SelectsWordUnderCursor()
    {
        await _app.ClickAsync(Document);
        await _app.ClearAsync(Document);
        await _app.TypeAsync(Document, "Hello AutoMancer");

        var (centerX, centerY) = (await WaitForWordRectAsync("AutoMancer")).Center;
        await _app.ClickAtAsync((int)centerX, (int)centerY);
        await _app.ClickAtAsync((int)centerX, (int)centerY);

        await ExpectSelectionAsync("AutoMancer");
    }

    // Ctrl+A should select the entire document contents.
    [Fact]
    public async Task PressKeyAsync_CtrlA_SelectsAllText()
    {
        const string text = "Hello AutoMancer";
        await _app.ClickAsync(Document);
        await _app.ClearAsync(Document);
        await _app.TypeAsync(Document, text);

        await _app.PressKeyAsync(Key.A, new KeyModifiers(Control: true));

        await ExpectSelectionAsync(text);
    }

    // Typing "AB" while holding Shift across both keys in one HotkeyAsync call proves the modifier stays active for the second key, not released after the first.
    [Fact]
    public async Task HotkeyAsync_MultipleKeys_HoldsModifierAcrossAllOfThem()
    {
        await _app.ClickAsync(Document);
        await _app.ClearAsync(Document);

        await _app.HotkeyAsync(new KeyModifiers(Shift: true), [Key.A, Key.B]);

        await Expect(_app, Document).ToHaveValueAsync("AB");
    }

    // Right-clicking the document opens its context menu; since WinUI3 Notepad pre-loads menu items into the UIA tree, focus moving off the Document is the reliable signal.
    [Fact]
    public async Task RightClickAsync_OpensContextMenu()
    {
        await _app.ClickAsync(Document);

        await _app.RightClickAsync(Document);
        await Expect(_app, FocusedControlTypeAsync).ToSatisfyAsync(type => type != UIA_ControlTypeIds.UIA_DocumentControlTypeId, "to move off the Document once the context menu opens");

        // Wait for the menu to close too, so it can't swallow the next test's input.
        NotepadFixture.SendEscape();
        await Expect(_app, FocusedControlTypeAsync).ToSatisfyAsync(type => type == UIA_ControlTypeIds.UIA_DocumentControlTypeId, "to return to the Document once the menu closes");
    }

    // Shift+click extends the selection from the caret to the click point: Key.Home sets a known start, then a Shift+click past the last character selects the whole line.
    [Fact]
    public async Task ClickAsync_WithShiftModifier_ExtendsSelectionToClickPoint()
    {
        const string text = "Hello AutoMancer";
        await _app.ClickAsync(Document);
        await _app.ClearAsync(Document);
        await _app.TypeAsync(Document, text);
        var wordRect = await WaitForWordRectAsync("AutoMancer");

        // A point just past the word's right edge — resolves to the boundary right after the last character.
        var endOfTextX = (int)(wordRect.X + wordRect.Width + 2);
        var endOfTextY = (int)wordRect.Center.Y;

        await _app.PressKeyAsync(Key.Home);
        await _app.KeyDownAsync(Key.LeftShift);
        await _app.ClickAtAsync(endOfTextX, endOfTextY);
        await _app.KeyUpAsync(Key.LeftShift);

        await ExpectSelectionAsync(text);
    }

    // Ctrl+click places the caret at the click point same as a plain click, proving the modifier doesn't interfere with placement; verified by typing a marker character and checking where it landed.
    [Fact]
    public async Task ClickAsync_WithControlModifier_PlacesCaretAtClickPoint()
    {
        await _app.ClickAsync(Document);
        await _app.ClearAsync(Document);
        await _app.TypeAsync(Document, "Hello AutoMancer");
        var wordRect = await WaitForWordRectAsync("AutoMancer");

        // A point just inside the left edge of "AutoMancer" — resolves to the boundary right before the "A".
        var startOfWordX = (int)(wordRect.X + 1);
        var startOfWordY = (int)wordRect.Center.Y;

        await _app.KeyDownAsync(Key.LeftControl);
        await _app.ClickAtAsync(startOfWordX, startOfWordY);
        await _app.KeyUpAsync(Key.LeftControl);
        await _app.TypeDirectAsync("X");

        await Expect(_app, Document).ToHaveValueAsync("Hello XAutoMancer");
    }

    // ClickAction's modifier path end to end through a locator (gate, then one batched Shift+click): the Document's center lies below and past the end of one short line, so Shift+clicking it from Home selects the whole line.
    [Fact]
    public async Task ClickAsync_Locator_WithShiftModifier_ExtendsSelectionToEnd()
    {
        const string text = "Hello AutoMancer";
        await _app.ClickAsync(Document);
        await _app.ClearAsync(Document);
        await _app.TypeAsync(Document, text);
        await _app.PressKeyAsync(Key.Home);

        await _app.ClickAsync(Document, MouseButton.Left, new KeyModifiers(Shift: true));

        await ExpectSelectionAsync(text);
    }

    // DoubleClickAction end to end through a locator: every line is one long unbroken word, so the Document's center is guaranteed to land on a word, and the double-click selects exactly one whole line's word.
    [Fact]
    public async Task DoubleClickAsync_Locator_SelectsWordAtDocumentCenter()
    {
        var word = string.Concat(Enumerable.Repeat("AutoMancer", 40));
        await _app.ClickAsync(Document);
        await _app.ClearAsync(Document);
        await _app.TypeAsync(Document, string.Join(Environment.NewLine, Enumerable.Repeat(word, 80)));
        await _app.PressKeyAsync(Key.Home, new KeyModifiers(Control: true));
        // The center only lands on a word once the lines have laid out.
        await WaitForWordRectAsync(word);

        await _app.DoubleClickAsync(Document);

        await ExpectSelectionAsync(word);
    }

    // HoverAsync moves the mouse without clicking; since Notepad has no reliable hover-triggered visual state, this verifies the OS cursor position via GetCursorPos instead.
    [Fact]
    public async Task HoverAsync_MovesCursorToElementCenter()
    {
        await _app.ClickAsync(Document);

        await _app.HoverAsync(Locator.ByName("File"));

        await ExpectCursorAtCenterOfFileAsync();
    }

    // MoveMouseRelativeAsync repositions the cursor by a pixel delta from wherever it currently is, unlike HoverAsync's absolute positioning. Asserts direction and rough magnitude only, not an exact pixel count: relative SendInput moves pass through the OS pointer-acceleration/speed curve, which was observed on this machine to roughly double the requested delta — that scaling is a per-machine mouse setting, not something this code controls.
    [Fact]
    public async Task MoveMouseRelativeAsync_MovesCursorByDelta()
    {
        await _app.HoverAsync(Locator.ByName("File"));
        var before = await ExpectCursorAtCenterOfFileAsync();

        await _app.MoveMouseRelativeAsync(30, -15);

        var after = await Expect(_app, CursorPositionAsync).ToSatisfyAsync(c => c.X != before.X || c.Y != before.Y, "to move off the File button");
        var (deltaX, deltaY) = (after.X - before.X, after.Y - before.Y);
        Assert.True(deltaX is > 5 and < 200, $"Unexpected X delta: {deltaX}");
        Assert.True(deltaY is < -5 and > -200, $"Unexpected Y delta: {deltaY}");
    }

    // Scroll wheel notches move the document's vertical scroll position; 100 lines always overflow the window, so it waits for the Document to become scrollable rather than skipping.
    [Fact]
    public async Task ScrollWheelAsync_ScrollsDocumentView()
    {
        await _app.ClickAsync(Document);
        await _app.ClearAsync(Document);
        var manyLines = string.Join("\r", Enumerable.Range(1, 100).Select(i => $"Line {i}"));
        await _app.TypeAsync(Document, manyLines);

        var editor = await _app.FindAsync(Document);
        var scrollPattern = (IUIAutomationScrollPattern)((IUIAutomationElement)editor.NativeHandle).GetCurrentPattern(UIA_PatternIds.UIA_ScrollPatternId);
        await Expect(_app, () => Task.FromResult(scrollPattern.CurrentVerticallyScrollable != 0)).ToSatisfyAsync(scrollable => scrollable, "to become scrollable once 100 lines lay out");
        var before = scrollPattern.CurrentVerticalScrollPercent;

        await _app.ScrollWheelAsync(Document, deltaX: 0, deltaY: -5);

        await Expect(_app, () => Task.FromResult(scrollPattern.CurrentVerticalScrollPercent)).ToSatisfyAsync(percent => percent > before, $"to scroll past {before}%");
    }

    // SetFocusAsync moves keyboard focus to the target element without clicking it.
    [Fact]
    public async Task SetFocusAsync_SetsKeyboardFocusToElement()
    {
        await _app.ClickAsync(Document);

        await _app.SetFocusAsync(Locator.ByName("File"));

        var fileButton = (IUIAutomationElement)(await _app.FindAsync(Locator.ByName("File"))).NativeHandle;
        await Expect(_app, () => Task.FromResult(fileButton.CurrentHasKeyboardFocus != 0)).ToSatisfyAsync(focused => focused, "File to have keyboard focus");

        await _app.ClickAsync(Document);
    }

    // Same through a UIA2-resolved handle, which SetFocusAction used to accept as a silent no-op.
    [Fact]
    public async Task SetFocusAsync_Uia2Handle_SetsKeyboardFocusToElement()
    {
        var uia2 = _app.WithOptions(new AppOptions { ProviderChain = ["uia2"] });
        await _app.ClickAsync(Document);

        await uia2.SetFocusAsync(Locator.ByName("File"));

        var fileButton = (System.Windows.Automation.AutomationElement)(await uia2.FindAsync(Locator.ByName("File"))).NativeHandle;
        await Expect(_app, () => Task.FromResult(fileButton.Current.HasKeyboardFocus)).ToSatisfyAsync(focused => focused, "File to have keyboard focus");

        await _app.ClickAsync(Document);
    }

    // Resolves the live Document element's TextPattern, for reading its text/selection and for TextRange bounding rects to click at.
    private async Task<IUIAutomationTextPattern> GetTextPatternAsync()
    {
        var editor = await _app.FindAsync(Document);
        return (IUIAutomationTextPattern)((IUIAutomationElement)editor.NativeHandle).GetCurrentPattern(UIA_PatternIds.UIA_TextPatternId);
    }

    // Polls until word has laid out in the Document and returns its on-screen rect; read straight after typing, it can come back missing or partly rendered.
    private async Task<Rect> WaitForWordRectAsync(string word)
    {
        var textPattern = await GetTextPatternAsync();
        return await Expect(_app, () => Task.FromResult(textPattern.DocumentRange.FindText(word, 0, 0)?.GetBoundingRectangles() is { Length: >= 4 } r ? new Rect(r[0], r[1], r[2], r[3]) : default))
            .ToSatisfyAsync(rect => rect.Width > 50, $"\"{word}\" to lay out");
    }

    // Polls until the Document's selected text is exactly expected.
    private async Task ExpectSelectionAsync(string expected)
    {
        var textPattern = await GetTextPatternAsync();
        await Expect(_app, () => Task.FromResult(textPattern.GetSelection() is { Length: > 0 } selection ? selection.GetElement(0).GetText(-1) : ""))
            .ToSatisfyAsync(selected => selected == expected, $"selection to be \"{expected}\"");
    }

    // Polls until the cursor sits on the File button's center, within SendInput's 0-65535 normalization rounding, and returns where it is.
    private async Task<NativeMethods.POINT> ExpectCursorAtCenterOfFileAsync()
    {
        var file = (await _app.FindAsync(Locator.ByName("File"))).BoundingRect;
        var (expectedX, expectedY) = ((int)(file.X + file.Width / 2), (int)(file.Y + file.Height / 2));
        return await Expect(_app, CursorPositionAsync).ToSatisfyAsync(c => Math.Abs(c.X - expectedX) <= 2 && Math.Abs(c.Y - expectedY) <= 2, $"to be within 2px of ({expectedX}, {expectedY})");
    }

    // Reads the OS cursor position.
    private static Task<NativeMethods.POINT> CursorPositionAsync()
    {
        NativeMethods.GetCursorPos(out var cursor);
        return Task.FromResult(cursor);
    }

    // Reads the control type of whichever element has keyboard focus, or -1 if none.
    private static Task<int> FocusedControlTypeAsync()
        => Task.Run(() => new CUIAutomation8Class().GetFocusedElement()?.CurrentControlType ?? -1);
}
