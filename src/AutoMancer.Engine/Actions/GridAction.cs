// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows.Automation;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;

namespace AutoMancer.Engine.Actions;

// Reads a grid's row/column counts and fetches cells via GridPattern, falling back to TablePattern under UIA2; counts are 0 and the cell is null when neither is supported.
public static class GridAction
{
    // Returns the grid's row count, or 0 if it supports neither GridPattern nor TablePattern.
    public static Task<int> GetRowCountAsync(ElementHandle grid, CancellationToken ct = default) => Task.Run(() =>
        Guarded(grid, () => Read(grid, uia3: p => p.CurrentRowCount, uia2Grid: p => p.Current.RowCount, uia2Table: p => p.Current.RowCount)), ct);

    // Returns the grid's column count, or 0 if it supports neither GridPattern nor TablePattern.
    public static Task<int> GetColumnCountAsync(ElementHandle grid, CancellationToken ct = default) => Task.Run(() =>
        Guarded(grid, () => Read(grid, uia3: p => p.CurrentColumnCount, uia2Grid: p => p.Current.ColumnCount, uia2Table: p => p.Current.ColumnCount)), ct);

    // Returns the cell at the zero-based row/column as a normal handle; null if the grid supports neither pattern or UIA reports no element there, and an out-of-range index propagates UIA's own exception.
    public static Task<ElementHandle?> GetCellAsync(ElementHandle grid, int row, int column, CancellationToken ct = default) => Task.Run(() =>
        // UIA3's TablePattern has no GetItem, but a Table element always supports GridPattern too, so GridPattern alone covers it.
        Guarded(grid, () => grid switch
        {
            { NativeHandle: IUIAutomationElement element, Provider: Uia3Provider provider }
                when element.GetCurrentPattern(UIA_PatternIds.UIA_GridPatternId) is IUIAutomationGridPattern pattern
                => pattern.GetItem(row, column) is { } cell ? provider.Wrap(cell) : null,
            { NativeHandle: AutomationElement element, Provider: Uia2Provider provider }
                when element.TryGetCurrentPattern(GridPattern.Pattern, out var pattern)
                => ((GridPattern)pattern).GetItem(row, column) is { } cell ? provider.Wrap(cell) : null,
            { NativeHandle: AutomationElement element, Provider: Uia2Provider provider }
                when element.TryGetCurrentPattern(TablePattern.Pattern, out var pattern)
                => ((TablePattern)pattern).GetItem(row, column) is { } cell ? provider.Wrap(cell) : null,
            _ => null, // Win32 handles, or an element with neither pattern.
        }), ct);

    // Runs a live UIA read, surfacing an element that has left the tree as StaleElementError and letting any other failure propagate as-is.
    private static T Guarded<T>(ElementHandle grid, Func<T> read)
    {
        try { return read(); }
        catch (Exception ex) when (ElementInputHelpers.IsStaleFailure(ex, grid))
        {
            throw ElementInputHelpers.Stale(grid, ex);
        }
    }

    // Reads one integer from whichever pattern the element exposes, preferring GridPattern over TablePattern.
    private static int Read(ElementHandle grid, Func<IUIAutomationGridPattern, int> uia3, Func<GridPattern, int> uia2Grid, Func<TablePattern, int> uia2Table) => grid.NativeHandle switch
    {
        IUIAutomationElement element => element.GetCurrentPattern(UIA_PatternIds.UIA_GridPatternId) is IUIAutomationGridPattern pattern ? uia3(pattern) : 0,
        AutomationElement element when element.TryGetCurrentPattern(GridPattern.Pattern, out var pattern) => uia2Grid((GridPattern)pattern),
        AutomationElement element when element.TryGetCurrentPattern(TablePattern.Pattern, out var pattern) => uia2Table((TablePattern)pattern),
        _ => 0, // Win32 handles, or an element with neither pattern.
    };
}
