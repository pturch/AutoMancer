// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Shared instance of one test app for a test class: launched once, killed after; both apps have a status label showing the last pick (reset by clicking it), a fruit ComboBox and a label with a tooltip.
public abstract class TestAppFixture(string appName) : IAsyncLifetime
{
    public static readonly Locator Status = Locator.ByAutomationId("status");
    public static readonly Locator FruitCombo = Locator.ByAutomationId("fruitCombo");
    public static readonly Locator TipLabel = Locator.ByAutomationId("tipLabel");

    // The test apps live outside the repo, one project folder each: %TEMP%\AutoMancer.TestApps\<appName>\.
    private static readonly string AppsRoot = Path.Combine(Path.GetTempPath(), "AutoMancer.TestApps");

    public App App { get; private set; } = null!;

    // Launches the app and waits for its status label, so the first test doesn't hit a cold start.
    public async Task InitializeAsync()
    {
        App = await App.LaunchAsync(await EnsureBuiltAsync());
        await App.WithOptions(new AppOptions { ImplicitWaitMs = 5_000 }).FindAsync(Status);
    }

    // Builds the app with dotnet build if its exe isn't there yet, and returns the exe's path; throws with the build output if the build fails (e.g. the sources are missing).
    private async Task<string> EnsureBuiltAsync()
    {
        var project = Path.Combine(AppsRoot, appName);
        var exe = Path.Combine(project, "bin", "Debug", "net10.0-windows10.0.22621.0", $"{appName}.exe");
        if (File.Exists(exe))
            return exe;

        using var build = Process.Start(new ProcessStartInfo("dotnet", $"build \"{project}\"") { UseShellExecute = false, RedirectStandardOutput = true })!;
        var output = await build.StandardOutput.ReadToEndAsync();
        await build.WaitForExitAsync();
        if (build.ExitCode != 0)
            throw new InvalidOperationException($"Building test app {appName} in {project} failed:\n{output}");
        return exe;
    }

    // Clicks the status label back to "ready", so a test's status assertion can't pass on an earlier test's pick.
    public async Task ResetStatusAsync()
    {
        await App.ClickAsync(Status);
        await Expect(App, Status).ToHaveNameAsync("ready");
    }

    // Ends on a real synthesized click before killing the app: after a UIA Invoke (no input sent), Windows can refuse to foreground the next app launched. No-ops if InitializeAsync failed to launch.
    public async Task DisposeAsync()
    {
        if (App is null) return;
        await App.ClickAsync(Status);
        await App.KillAsync();
        await App.DisposeAsync();
    }
}

// ContextMenuHost: WinForms, with real Win32 #32768 menus, a ContextMenuStrip, a secondary window, and a message box.
public sealed class ContextMenuHostFixture() : TestAppFixture("ContextMenuHost")
{
    public static readonly Locator NativeArea = Locator.ByAutomationId("nativeArea");
    public static readonly Locator MoreButton = Locator.ByAutomationId("moreButton");
    public static readonly Locator StripArea = Locator.ByAutomationId("stripArea");
    public static readonly Locator OpenSecondary = Locator.ByAutomationId("openSecondary");
    public static readonly Locator SecondaryArea = Locator.ByAutomationId("secondaryArea");
    public static readonly Locator ShowMessage = Locator.ByAutomationId("showMessage");
}

// WpfMenuHost: a label with a ContextMenu, a TextBox with WPF's built-in menu, a "⋯" button whose left-click opens a menu, and a Filter button opening a flyout.
public sealed class WpfMenuHostFixture() : TestAppFixture("WpfMenuHost")
{
    public static readonly Locator MenuLabel = Locator.ByAutomationId("menuLabel");
    public static readonly Locator EditBox = Locator.ByAutomationId("editBox");
    public static readonly Locator MoreButton = Locator.ByAutomationId("moreButton");
    public static readonly Locator FilterButton = Locator.ByAutomationId("filterButton");
    public static readonly Locator FilterText = Locator.ByAutomationId("filterText");
}
