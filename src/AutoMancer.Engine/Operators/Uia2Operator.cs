// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using System.Windows.Automation;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Operators;

// Handles native element interactions for UIA2-resolved elements via InvokePattern and ValuePattern.
public sealed class Uia2Operator : IElementOperator
{
    // Invokes the element via InvokePattern if supported; returns false when it does not or the element has gone stale.
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
        catch (Exception ex) when (ex is InvalidOperationException or ElementNotAvailableException)
        {
            return false;
        }
    }, ct);

    // Sets the element's value via ValuePattern if supported and not read-only; returns false otherwise or when the element has gone stale.
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
        catch (Exception ex) when (ex is InvalidOperationException or ElementNotAvailableException)
        {
            return false;
        }
    }, ct);

    // Reads the element's value via ValuePattern if supported, falling back to TextPattern's full document text; returns null when neither pattern is available, throws StaleElementError when the element has gone stale.
    public Task<string?> TryGetValueAsync(ElementHandle element, CancellationToken ct = default) => Task.Run(() =>
    {
        if (element.NativeHandle is not AutomationElement uia2Element)
            return (string?)null;
        try
        {
            // UIA2's GetCurrentPattern throws the same InvalidOperationException for a gone element as for a missing pattern, so read a Current property first to surface ElementNotAvailableException.
            _ = uia2Element.Current.ProcessId;
            // A detached element (e.g. from a closed XAML dialog) still answers reads, with blank values that would pass for a real empty value.
            if (ElementInputHelpers.IsDetached(element))
                throw ElementInputHelpers.Stale(element, null);
            if (uia2Element.GetCurrentPattern(ValuePattern.Pattern) is ValuePattern valuePattern)
                return valuePattern.Current.Value;
            if (uia2Element.GetCurrentPattern(TextPattern.Pattern) is TextPattern textPattern)
                return textPattern.DocumentRange.GetText(-1); // -1 means no limit, the entire text
            return (string?)null;
        }
        // Filtered before the general catch below, which would otherwise swallow a post-exit E_UNEXPECTED COMException — a read has no fallback to re-detect a gone element.
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, element))
        {
            throw ElementInputHelpers.Stale(element, ex);
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            return (string?)null;
        }
    }, ct);
}
