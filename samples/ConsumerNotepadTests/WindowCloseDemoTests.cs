// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace ConsumerNotepadTests;

// Launches its own Notepad rather than using NotepadFixture's; WinUI3 Notepad is single-instance, so closing it closes the shared window too, which is why it runs in its own collection.
[Collection("ConsumerNotepadWindowClose")]
public sealed class WindowCloseDemoTests
{
    // Launches its own Notepad instance, then closes it via CloseWindowAsync — tries WindowPattern.Close() first, falls back to posting WM_CLOSE.
    [Fact]
    public async Task CloseWindowAsync_ClosesTheLaunchedWindow()
    {
        var app = await App.LaunchAsync("notepad.exe");
        try
        {
            await app.FindAsync(Locator.ByControlType("Document"));
            await app.CloseWindowAsync();
        }
        finally
        {
            await app.DisposeAsync();
        }
    }
}
