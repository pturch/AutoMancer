// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Automation;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Selects, deselects and reads list/combo item selection via SelectionItemPattern and SelectionPattern; writes no-op and reads come back false/empty when the pattern is unavailable.
public static class SelectionAction
{
    // Selects the item and clears any other selection; no-op if SelectionItemPattern is not supported.
    public static Task SelectAsync(ElementHandle element, CancellationToken ct = default)
        => RunItemAsync(element, "Select", "selected", p => p.Select, p => p.Select, ct);

    // Adds the item to the current selection (multi-select lists); no-op if SelectionItemPattern is not supported.
    public static Task AddToSelectionAsync(ElementHandle element, CancellationToken ct = default)
        => RunItemAsync(element, "AddToSelection", "added to selection", p => p.AddToSelection, p => p.AddToSelection, ct);

    // Removes the item from the current selection; no-op if SelectionItemPattern is not supported.
    public static Task RemoveFromSelectionAsync(ElementHandle element, CancellationToken ct = default)
        => RunItemAsync(element, "RemoveFromSelection", "removed from selection", p => p.RemoveFromSelection, p => p.RemoveFromSelection, ct);

    // Returns whether the item is currently selected; false if SelectionItemPattern is not supported.
    public static Task<bool> IsSelectedAsync(ElementHandle element, CancellationToken ct = default) => Task.Run(() =>
    {
        try
        {
            return element.NativeHandle switch
            {
                IUIAutomationElement uia3 => uia3.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId) is IUIAutomationSelectionItemPattern pattern && pattern.CurrentIsSelected != 0,
                AutomationElement uia2 => uia2.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern) && ((SelectionItemPattern)pattern).Current.IsSelected,
                _ => false, // Win32 handles have no SelectionItemPattern.
            };
        }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, element))
        {
            throw ElementInputHelpers.Stale(element, ex);
        }
    }, ct);

    // Returns the container's currently selected items as handles; empty if SelectionPattern is not supported or nothing is selected.
    public static Task<IReadOnlyList<ElementHandle>> GetSelectedItemsAsync(ElementHandle container, CancellationToken ct = default) => Task.Run(() =>
    {
        try
        {
            IEnumerable<ElementHandle?> items = [];
            if (container is { NativeHandle: IUIAutomationElement uia3, Provider: Uia3Provider uia3Provider }
                && uia3.GetCurrentPattern(UIA_PatternIds.UIA_SelectionPatternId) is IUIAutomationSelectionPattern uia3Pattern)
            {
                var selected = uia3Pattern.GetCurrentSelection();
                items = Enumerable.Range(0, selected?.Length ?? 0).Select(i => uia3Provider.Wrap(selected!.GetElement(i))).ToList();
            }
            else if (container is { NativeHandle: AutomationElement uia2, Provider: Uia2Provider uia2Provider }
                && uia2.TryGetCurrentPattern(SelectionPattern.Pattern, out var uia2Pattern))
                items = ((SelectionPattern)uia2Pattern).Current.GetSelection().Select(uia2Provider.Wrap).ToList();
            // Win32 handles and elements with no SelectionPattern fall through to the empty list.
            return (IReadOnlyList<ElementHandle>)items.OfType<ElementHandle>().ToList();
        }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, container))
        {
            throw ElementInputHelpers.Stale(container, ex);
        }
    }, ct);

    // Looks up SelectionItemPattern and runs the chosen write method, logging a no-op when the pattern is missing.
    private static Task RunItemAsync(ElementHandle element, string verb, string done,
        Func<IUIAutomationSelectionItemPattern, Action> uia3Method, Func<SelectionItemPattern, Action> uia2Method, CancellationToken ct) => Task.Run(() =>
    {
        // Both the pattern lookup and the call are live UIA calls that fail if the element has left the tree; surface that as StaleElementError, let any other failure propagate as-is.
        try
        {
            Action? invoke = element.NativeHandle switch
            {
                IUIAutomationElement uia3 => uia3.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId) is IUIAutomationSelectionItemPattern pattern ? uia3Method(pattern) : null,
                AutomationElement uia2 => uia2.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern) ? uia2Method((SelectionItemPattern)pattern) : null,
                _ => null, // Win32 handles have no SelectionItemPattern.
            };
            if (invoke is null)
            {
                element.Logger?.Info($"SelectionItemPattern not supported, nothing {done}", new { elementId = element.Id, resolvedVia = element.ResolvedVia });
                return;
            }
            invoke();
        }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, element))
        {
            throw ElementInputHelpers.Stale(element, ex);
        }
        element.Logger?.Info($"{verb} via SelectionItemPattern", new { elementId = element.Id });
    }, ct);
}
