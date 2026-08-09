// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Operators;

// Handles native element interactions for UIA3-resolved elements via InvokePattern and ValuePattern.
public sealed class Uia3Operator : IElementOperator
{
    // Invokes the element via InvokePattern if it exposes one; returns false when it does not.
    public Task<bool> TryClickAsync(ElementHandle element, CancellationToken ct = default) => Task.Run(() =>
    {
        if (element.NativeHandle is not IUIAutomationElement uiaElement)
            return false;
        if (uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) is not IUIAutomationInvokePattern invoke)
            return false;
        invoke.Invoke();
        return true;
    }, ct);

    // Sets the element's value via ValuePattern if it is supported and not read-only; returns false otherwise.
    public Task<bool> TrySetValueAsync(ElementHandle element, string value, CancellationToken ct = default) => Task.Run(() =>
    {
        if (element.NativeHandle is not IUIAutomationElement uiaElement)
            return false;
        if (uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId) is not IUIAutomationValuePattern valuePattern)
            return false;
        if (valuePattern.CurrentIsReadOnly != 0)
            return false;
        valuePattern.SetValue(value);
        return true;
    }, ct);

    // Reads the element's value via ValuePattern if supported, falling back to TextPattern's full document text; returns null when neither pattern is available.
    public Task<string?> TryGetValueAsync(ElementHandle element, CancellationToken ct = default) => Task.Run(() =>
    {
        if (element.NativeHandle is not IUIAutomationElement uiaElement)
            return (string?)null;
        if (uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId) is IUIAutomationValuePattern valuePattern)
            return valuePattern.CurrentValue;
        if (uiaElement.GetCurrentPattern(UIA_PatternIds.UIA_TextPatternId) is IUIAutomationTextPattern textPattern)
            return textPattern.DocumentRange.GetText(-1); // -1 means no limit, the entire text
        return null;
    }, ct);
}
