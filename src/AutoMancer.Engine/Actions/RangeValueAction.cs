// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Automation;
using AutoMancer.Engine.Core;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Sets and reads a slider/progress bar/scrollbar value via RangeValuePattern; the write no-ops and the read returns null when the pattern is unavailable.
public static class RangeValueAction
{
    // Sets the control's value; no-op if RangeValuePattern is not supported, and UIA's own exception propagates for a read-only control or an out-of-range value.
    public static Task SetValueAsync(ElementHandle element, double value, CancellationToken ct = default) => Task.Run(() =>
    {
        // Both the pattern lookup and SetValue are live UIA calls that fail if the element has left the tree; surface that as StaleElementError, let any other failure propagate as-is.
        try
        {
            Action? set = element.NativeHandle switch
            {
                IUIAutomationElement uia3 => uia3.GetCurrentPattern(UIA_PatternIds.UIA_RangeValuePatternId) is IUIAutomationRangeValuePattern pattern ? () => pattern.SetValue(value) : null,
                AutomationElement uia2 => uia2.TryGetCurrentPattern(RangeValuePattern.Pattern, out var pattern) ? () => ((RangeValuePattern)pattern).SetValue(value) : null,
                _ => null, // Win32 handles have no RangeValuePattern.
            };
            if (set is null)
            {
                element.Logger?.Info("RangeValuePattern not supported, nothing set", new { elementId = element.Id, resolvedVia = element.ResolvedVia });
                return;
            }
            set();
        }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, element))
        {
            throw ElementInputHelpers.Stale(element, ex);
        }
        element.Logger?.Info("Set value via RangeValuePattern", new { elementId = element.Id, value });
    }, ct);

    // Returns the control's current value and declared bounds; null if RangeValuePattern is not supported.
    public static Task<RangeValueInfo?> GetValueAsync(ElementHandle element, CancellationToken ct = default) => Task.Run(() =>
    {
        try
        {
            return element.NativeHandle switch
            {
                IUIAutomationElement uia3 => uia3.GetCurrentPattern(UIA_PatternIds.UIA_RangeValuePatternId) is IUIAutomationRangeValuePattern pattern
                    ? new RangeValueInfo(pattern.CurrentValue, pattern.CurrentMinimum, pattern.CurrentMaximum) : (RangeValueInfo?)null,
                AutomationElement uia2 => uia2.TryGetCurrentPattern(RangeValuePattern.Pattern, out var pattern)
                    ? new RangeValueInfo(((RangeValuePattern)pattern).Current.Value, ((RangeValuePattern)pattern).Current.Minimum, ((RangeValuePattern)pattern).Current.Maximum) : (RangeValueInfo?)null,
                _ => null, // Win32 handles have no RangeValuePattern.
            };
        }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, element))
        {
            throw ElementInputHelpers.Stale(element, ex);
        }
    }, ct);
}
