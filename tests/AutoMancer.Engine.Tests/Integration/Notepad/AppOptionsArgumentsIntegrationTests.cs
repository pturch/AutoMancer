// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies AppOptions.Arguments actually reaches the launched OS process, not just AppSession's internal ProcessStartInfo construction; in the "Notepad" collection to serialize foreground/window use.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class AppOptionsArgumentsIntegrationTests
{
    [Fact]
    public async Task LaunchAsync_Arguments_OpensTheGivenFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"automancer-args-test-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(filePath, "AutoMancer Arguments test file.");
        try
        {
            var app = await App.LaunchAsync(
                "notepad.exe",
                new AppOptions { Arguments = $"\"{filePath}\"" },
                windowMatch: new WindowMatchOptions { RequireNewWindow = true });
            try
            {
                // The window first appears titled plain "Notepad" and only picks up the file's name once it loads (~400ms later), so poll rather than read once.
                var title = "";
                var deadline = Environment.TickCount64 + 5_000;
                while (!(title = Process.GetProcessById(app.ProcessId).MainWindowTitle).Contains(Path.GetFileName(filePath)) && Environment.TickCount64 < deadline)
                    await Task.Delay(100);
                Assert.Contains(Path.GetFileName(filePath), title);
            }
            finally
            {
                await app.KillAsync();
                // The file's tab survives the kill; without this the next Notepad launch (here or in samples/ConsumerNotepadTests) restores it after the file is deleted below and opens a modal "Cannot find the file" dialog.
                NotepadCollectionFixture.ClearTabState();
            }
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
