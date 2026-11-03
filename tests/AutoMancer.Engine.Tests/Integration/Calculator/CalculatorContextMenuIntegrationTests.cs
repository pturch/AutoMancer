// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Calculator's results-display context menu, a packaged app's popup window: Copy puts the displayed value on the clipboard.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class CalculatorContextMenuIntegrationTests : IAsyncLifetime
{
    private const string CalculatorAumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
    private static readonly Locator Results = Locator.ByAutomationId("CalculatorResults");

    private App? _app;

    // Launches Calculator and warms up on its results display, since a packaged app's first UIA reads can lag its window.
    public async Task InitializeAsync()
    {
        _app = await App.LaunchPackagedAsync(CalculatorAumid);
        await _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 8_000 }).FindAsync(Results);
    }

    // Kills Calculator; no-ops if InitializeAsync failed to launch.
    public async Task DisposeAsync()
    {
        if (_app is not null) await _app.KillAsync();
    }

    // Enters 739 (unlikely to be on the clipboard already), copies it from the display's context menu, and polls the clipboard for it.
    [Fact]
    public async Task ClickContextMenuItemAsync_Copy_PutsDisplayedValueOnClipboard()
    {
        await _app!.ClickAsync(Locator.ByAutomationId("num7Button"));
        await _app.ClickAsync(Locator.ByAutomationId("num3Button"));
        await _app.ClickAsync(Locator.ByAutomationId("num9Button"));
        await Expect(_app, Results).ToContainTextAsync("739");

        await _app.ClickContextMenuItemAsync(Results, "Copy");

        await Expect(_app, () => Task.FromResult(ReadClipboardText())).ToSatisfyAsync(text => text == "739", "to hold the displayed value");

        // Ends on real synthesized input: the Copy click went through UIA Invoke, which sends none, and Windows can then refuse to foreground the next app launched.
        var results = await _app.FindAsync(Results);
        await _app.ClickAtAsync((int)results.BoundingRect.Center.X, (int)results.BoundingRect.Center.Y);
    }

    // Reads the clipboard's Unicode text, or null if it holds none or another process has it open.
    private static string? ReadClipboardText()
    {
        if (!OpenClipboard(IntPtr.Zero))
            return null;
        try
        {
            var data = GetClipboardData(CfUnicodeText);
            if (data == IntPtr.Zero)
                return null;
            var text = Marshal.PtrToStringUni(GlobalLock(data));
            GlobalUnlock(data);
            return text;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private const uint CfUnicodeText = 13;

    // Opens the clipboard for reading; fails while another process has it open.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    // Returns the clipboard's data in format, or IntPtr.Zero if it has none.
    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    // Closes the clipboard after OpenClipboard.
    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    // Locks a global memory handle and returns a pointer to its contents.
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr memory);

    // Unlocks a handle locked by GlobalLock.
    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr memory);
}
