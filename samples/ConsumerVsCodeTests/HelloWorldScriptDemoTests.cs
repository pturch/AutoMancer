// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Testing.XUnit;

namespace ConsumerVsCodeTests;

// Drives VS Code end to end: create, type, save, and run a batch script, then verify it executed. Uses its own isolated instance (VsCodeLauncher), so no shared fixture like NotepadFixture.
[Collection("VsCode")]
public sealed class HelloWorldScriptDemoTests
{
    [Fact]
    public async Task CreateSaveAndRunScript_ProducesExpectedOutput()
    {
        var sandbox = VsCodeLauncher.CreateSandbox();
        var app = await VsCodeLauncher.LaunchAsync(sandbox);
        try
        {
            await DismissFirstRunDialogIfPresentAsync(app);
            await Task.Delay(500); // dialog's close animation/focus handoff is still settling; an immediate keystroke can land mid-transition.

            // AI Chat panel can default-focus; Ctrl+N there runs "New Chat" instead. Click a known workbench element to move focus away first.
            await app.ClickAsync(Locator.ByName("Explorer (Ctrl+Shift+E)"));
            await app.HotkeyAsync(new KeyModifiers(Control: true, Alt: true), [Key.B]); // hide chat panel so it can't reclaim focus later.
            await Task.Delay(300);

            await app.PressKeyAsync(Key.N, new KeyModifiers(Control: true));
            await Task.Delay(300); // first keystroke on a brand-new buffer settles focus rather than being typed.
            await TypeLineAsync(app, "@echo off");
            await TypeLineAsync(app, "echo Hello, World!");
            await app.TypeDirectAsync("echo Hello, World!>output.txt");

            var scriptPath = Path.Combine(sandbox.WorkspaceDir, "hello.bat");
            await SaveAsAsync(app, scriptPath);

            // "Focus on Terminal View" over "Toggle Terminal" (which doesn't guarantee focus); still follow with a real click since even this command's focus handoff is unreliable.
            await RunCommandAsync(app, "Terminal: Focus on Terminal View");
            await Task.Delay(500); // panel still takes a moment to finish opening/rendering after focus moves.
            await ClickInWindowAsync(app, xFraction: 0.5, yFraction: 0.9); // terminal has no reliable UIA element to click instead.
            await Task.Delay(300);

            // ".\hello.bat" not bare "hello.bat" — NoDefaultCurrentDirectoryInExePath can disable cmd.exe's implicit cwd search for bare filenames.
            await app.TypeDirectAsync(".\\hello.bat");
            await app.PressKeyAsync(Key.Enter);

            var outputPath = Path.Combine(sandbox.WorkspaceDir, "output.txt");
            var output = await WaitForFileContentAsync(outputPath, timeoutMs: 15_000);
            Assert.Contains("Hello, World!", output);
        }
        catch (Exception ex)
        {
            // TestSetup's CaptureScreenshotsOnFailure only covers Expect() assertions; this test uses Assert.*, so it captures its own screenshot.
            var path = Path.Combine(AutoMancerTestOptions.ScreenshotDirectory, $"vscode-hello-world-failure-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png");
            await File.WriteAllBytesAsync(path, await app.ScreenshotAsync());
            throw new Exception($"{ex.Message} (screenshot saved to {path})", ex);
        }
        finally
        {
            await VsCodeLauncher.KillAndDeleteAsync(app, sandbox);
        }
    }

    // Opens the Command Palette and runs commandTitle by its exact display name — immune to a keybinding meaning something else in whichever panel currently has focus.
    private static async Task RunCommandAsync(App app, string commandTitle)
    {
        await app.HotkeyAsync(new KeyModifiers(Control: true, Shift: true), [Key.P]);
        await Task.Delay(300); // palette's input box takes a moment to mount and take focus.
        await app.TypeDirectAsync(commandTitle);
        await app.PressKeyAsync(Key.Enter);
    }

    // Types one line and presses Enter — Monaco doesn't treat TypeDirectAsync's '\n' as a line break.
    private static async Task TypeLineAsync(App app, string line)
    {
        await app.TypeDirectAsync(line);
        await app.PressKeyAsync(Key.Enter);
    }

    // Dismisses the fresh-profile first-run walkthrough (Copilot sign-in, then theme picker) if present, using short probes to avoid slowing runs where a step is absent.
    private static async Task DismissFirstRunDialogIfPresentAsync(App app)
    {
        await ClickIfPresentAsync(app, "Continue without Signing In");
        await ClickIfPresentAsync(app, "Get Started");
    }

    // Clicks a point at the given fraction of the window's width/height, for surfaces with no clickable UIA element.
    private static async Task ClickInWindowAsync(App app, double xFraction, double yFraction)
    {
        var rect = await app.GetWindowSizeAsync();
        await app.ClickAtAsync((int)(rect.X + rect.Width * xFraction), (int)(rect.Y + rect.Height * yFraction));
    }

    // Clicks the named element if it resolves within a short timeout; no-ops if it never appears.
    private static async Task ClickIfPresentAsync(App app, string name)
    {
        var probe = app.WithOptions(new AppOptions { ImplicitWaitMs = 2_000, PollIntervalMs = 300 });
        try
        {
            await probe.ClickAsync(Locator.ByName(name));
        }
        catch (ElementNotFoundError)
        {
            // Not shown this run — nothing to dismiss.
        }
    }

    // Ctrl+S on an untitled buffer opens the native Save As dialog; typing the path into its default-focused field saves without resolving any UIA element.
    private static async Task SaveAsAsync(App app, string fullPath)
    {
        await app.PressKeyAsync(Key.S, new KeyModifiers(Control: true));

        // The dialog's App wraps the same VS Code process, not a separate one — never Kill/Dispose it.
        var dialog = await App.FindDialogAsync(app.ProcessId, "Save")
            ?? throw new InvalidOperationException("The Save As dialog did not appear.");
        await dialog.TypeDirectAsync(fullPath);
        await dialog.PressKeyAsync(Key.Enter);

        for (var attempt = 0; attempt < 20 && !File.Exists(fullPath); attempt++)
            await Task.Delay(250);
        Assert.True(File.Exists(fullPath), $"Expected '{fullPath}' to exist after Save As.");
    }

    // Polls for the output file and reads it back; its presence means the script ran to completion, not just got typed.
    private static async Task<string> WaitForFileContentAsync(string path, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                try { return await File.ReadAllTextAsync(path); }
                catch (IOException) { /* still being written */ }
            }
            await Task.Delay(300);
        }
        throw new TimeoutException($"'{path}' did not appear within {timeoutMs} ms — the script may not have run.");
    }
}
