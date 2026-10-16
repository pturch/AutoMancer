// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;
using static AutoMancer.Testing.Assertions;

namespace ConsumerVsCodeTests;

// Drives VS Code end to end: create, type, save, and run a batch script, then verify it executed. Uses its own isolated instance (VsCodeLauncher), so no shared fixture like NotepadFixture.
[Collection("VsCode")]
public sealed class HelloWorldScriptDemoTests
{
    // Keystrokes here go to whatever has focus, so each step first waits for the element that should receive them to appear.
    private static readonly Locator NewEditor = Locator.ByXPath("//Edit[@Name='Untitled-1']");
    private static readonly Locator PaletteInput = Locator.ByXPath("//Edit[@Name='Type the name of a command to run.']");
    private static readonly Locator TerminalInput = Locator.ByClassName("xterm-helper-textarea");

    [Fact]
    public async Task CreateSaveAndRunScript_ProducesExpectedOutput()
    {
        var sandbox = VsCodeLauncher.CreateSandbox();
        var app = await VsCodeLauncher.LaunchAsync(sandbox);
        try
        {
            await VsCodeLauncher.PrepareWorkbenchAsync(app);

            await app.PressKeyAsync(Key.N, new KeyModifiers(Control: true));
            await app.FindAsync(NewEditor);
            await TypeLineAsync(app, "@echo off");
            await TypeLineAsync(app, "echo Hello, World!");
            await app.TypeDirectAsync("echo Hello, World!>output.txt");

            var scriptPath = Path.Combine(sandbox.WorkspaceDir, "hello.bat");
            await SaveAsAsync(app, scriptPath);
            // Exact content, so a keystroke swallowed while the new editor took focus fails here rather than going unnoticed.
            Assert.Equal("@echo off\r\necho Hello, World!\r\necho Hello, World!>output.txt", await File.ReadAllTextAsync(scriptPath));

            // "Focus on Terminal View" over "Toggle Terminal" (which doesn't guarantee focus); still follow with a real click since even this command's focus handoff is unreliable.
            await RunCommandAsync(app, "Terminal: Focus on Terminal View");
            await app.ClickAsync(TerminalInput);

            // ".\hello.bat" not bare "hello.bat" — NoDefaultCurrentDirectoryInExePath can disable cmd.exe's implicit cwd search for bare filenames.
            await app.TypeDirectAsync(".\\hello.bat");
            await app.PressKeyAsync(Key.Enter);

            // The file the script writes is proof it ran to completion, not just got typed.
            var outputPath = Path.Combine(sandbox.WorkspaceDir, "output.txt");
            var output = await WaitForFileAsync(app, outputPath, timeoutMs: 15_000);
            Assert.Contains("Hello, World!", output);
        }
        catch (Exception ex)
        {
            // TestSetup's CaptureScreenshotsOnFailure only covers Expect() failures; this also covers Assert.* and the engine's own errors, so it captures its own screenshot.
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
        await app.FindAsync(PaletteInput);
        await app.TypeDirectAsync(commandTitle);
        await app.PressKeyAsync(Key.Enter);
    }

    // Types one line and presses Enter — Monaco doesn't treat TypeDirectAsync's '\n' as a line break.
    private static async Task TypeLineAsync(App app, string line)
    {
        await app.TypeDirectAsync(line);
        await app.PressKeyAsync(Key.Enter);
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

        await WaitForFileAsync(app, fullPath, timeoutMs: 5_000);
    }

    // Waits for path to be written and returns its contents; polled, because a file shows up some time after whatever writes it was set off.
    private static async Task<string> WaitForFileAsync(App app, string path, int timeoutMs)
        => (await Expect(app, () => TryReadAsync(path)).ToSatisfyAsync(text => text is not null, $"'{path}' to be written", timeoutMs: timeoutMs))!;

    // Reads path, or null while it doesn't exist yet or is still being written.
    private static async Task<string?> TryReadAsync(string path)
    {
        try { return await File.ReadAllTextAsync(path); }
        catch (IOException) { return null; }
    }
}
