// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Providers;

// Win32Provider's conversion of raw native menu text to the Name UIA3 reports for the same item.
public sealed class Win32ProviderMenuItemTests
{
    // Native item text must come out as the Name UIA3 reports for the same item (checked live against ContextMenuHost's TrackPopupMenuEx menu), so one itemName works on both paths. An owner-drawn item has no raw text at all, so it never gets here.
    [Theory]
    [InlineData("Charlie", "Charlie")]
    [InlineData("&Alpha\tCtrl+A", "Alpha")]
    [InlineData("B&ravo && Co", "Bravo && Co")]
    [InlineData("&Delta", "Delta")]
    [InlineData("Delta &One", "Delta One")]
    public void MenuItemDisplayName_MatchesUia3Name(string rawText, string expected) =>
        Assert.Equal(expected, Win32Provider.MenuItemDisplayName(rawText));
}
