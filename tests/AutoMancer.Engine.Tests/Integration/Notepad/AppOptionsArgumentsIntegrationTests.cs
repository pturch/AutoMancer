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
                Assert.Contains(Path.GetFileName(filePath), Process.GetProcessById(app.ProcessId).MainWindowTitle);
            }
            finally
            {
                await app.KillAsync();
            }
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
