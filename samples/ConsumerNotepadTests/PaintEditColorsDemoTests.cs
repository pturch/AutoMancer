// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing;

namespace ConsumerNotepadTests;

// Opens Paint's "Edit colors" dialog and closes it, in a Paint this class launches and kills itself. The dialog's "Custom colors" grid reports hundreds of thousands of empty items to UI Automation, so any search that has to get past it (the dialog's Cancel button comes after it) can't finish.
[Collection("ConsumerPaint")]
public sealed class PaintEditColorsDemoTests
{
    // Checks the dialog through its color spectrum, which comes before the grid, and closes it with Escape rather than by finding Cancel.
    [Fact]
    public async Task EditColors_OpensAndCloses()
    {
        var paint = await App.LaunchAsync("mspaint.exe", new AppOptions { ImplicitWaitMs = 10_000 });
        try
        {
            var editColors = Locator.ByName("Edit colors");
            await Assertions.Expect(paint, editColors).ToBeEnabledAsync();

            await paint.ClickAsync(editColors);

            var spectrum = Locator.ByAutomationId("ColorSpectrum");
            await Assertions.Expect(paint, spectrum).ToBeVisibleAsync();
            await paint.PressKeyAsync(Key.Escape);
            await paint.WaitUntilGoneAsync(spectrum);
        }
        finally
        {
            await paint.KillAsync();
        }
    }
}
