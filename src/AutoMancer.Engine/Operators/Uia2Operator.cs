// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Automation;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Operators;

// Handles native element interactions for UIA2-resolved elements via InvokePattern and ValuePattern.
public sealed class Uia2Operator : IElementOperator
{
    // Invokes the element via InvokePattern if supported; returns false when it does not.
    public Task<bool> TryClickAsync(ElementHandle element, CancellationToken ct = default) => Task.Run(() =>
    {
        if (element.NativeHandle is not AutomationElement uia2Element)
            return false;
        try
        {
            if (uia2Element.GetCurrentPattern(InvokePattern.Pattern) is not InvokePattern invoke)
                return false;
            invoke.Invoke();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }, ct);

    // Sets the element's value via ValuePattern if supported and not read-only; returns false otherwise.
    public Task<bool> TrySetValueAsync(ElementHandle element, string value, CancellationToken ct = default) => Task.Run(() =>
    {
        if (element.NativeHandle is not AutomationElement uia2Element)
            return false;
        try
        {
            if (uia2Element.GetCurrentPattern(ValuePattern.Pattern) is not ValuePattern valuePattern)
                return false;
            if (valuePattern.Current.IsReadOnly)
                return false;
            valuePattern.SetValue(value);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }, ct);
}
