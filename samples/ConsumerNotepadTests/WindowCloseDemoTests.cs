// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace ConsumerNotepadTests;

// Launches its own Notepad instance rather than sharing NotepadFixture, since WinUI3 Notepad is single-instance; still shares the ConsumerNotepad collection to stay sequential.
[Collection("ConsumerNotepad")]
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
