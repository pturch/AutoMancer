// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Tests.Integration;

// Shared instance of the fixtures/GridHost WPF app (a DataGrid, a multi-select ListBox, a CheckBox, a ComboBox, a Slider and a ProgressBar) for a test class: built and launched once, killed after.
public sealed class GridHostFixture : IAsyncLifetime
{
    public static readonly Locator FruitGrid = Locator.ByAutomationId("fruitGrid");
    public static readonly Locator GreekList = Locator.ByAutomationId("greekList");
    public static readonly Locator LoggingCheck = Locator.ByAutomationId("loggingCheck");
    public static readonly Locator SizeCombo = Locator.ByAutomationId("sizeCombo");
    public static readonly Locator VolumeSlider = Locator.ByAutomationId("volumeSlider");
    public static readonly Locator LoadProgress = Locator.ByAutomationId("loadProgress");

    public App App { get; private set; } = null!;

    // The built exe, so a test can launch its own disposable instance.
    public string ExePath { get; private set; } = "";

    // Builds GridHost (incremental, so near-free when up to date), launches it and waits for the grid so the first test doesn't hit a cold start.
    public async Task InitializeAsync()
    {
        ExePath = await BuildAsync();
        App = await App.LaunchAsync(ExePath);
        await App.WithOptions(new AppOptions { ImplicitWaitMs = 5_000 }).FindAsync(FruitGrid);
    }

    // Runs dotnet build on the fixture project found by walking up to the solution root, and returns the exe's path; throws with the build output on failure.
    private static async Task<string> BuildAsync()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AutoMancer.slnx")))
            root = root.Parent;
        var project = Path.Combine(root?.FullName ?? throw new InvalidOperationException("AutoMancer.slnx not found above the test output."), "fixtures", "GridHost");

        using var build = Process.Start(new ProcessStartInfo("dotnet", $"build \"{project}\"") { UseShellExecute = false, RedirectStandardOutput = true })!;
        var output = await build.StandardOutput.ReadToEndAsync();
        await build.WaitForExitAsync();
        if (build.ExitCode != 0)
            throw new InvalidOperationException($"Building GridHost in {project} failed:\n{output}");
        return Path.Combine(project, "bin", "Debug", "net10.0-windows10.0.22621.0", "GridHost.exe");
    }

    // Kills the app; no-ops if InitializeAsync failed to launch, so that failure surfaces on its own.
    public async Task DisposeAsync()
    {
        if (App is null) return;
        await App.KillAsync();
        await App.DisposeAsync();
    }
}
