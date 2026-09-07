// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace ConsumerVsCodeTests;

// Launches an isolated Visual Studio Code instance (its own user-data-dir/extensions-dir, so it never hands off to — or fights with — a VS Code window the developer already has open) and wraps it as an App, entirely through AutoMancer's public API.
internal static class VsCodeLauncher
{
    // One throwaway directory tree per launch: an isolated profile/extensions dir plus the workspace folder VS Code opens. Dispose via KillAndDeleteAsync.
    internal sealed record VsCodeSandbox(string RootDir, string UserDataDir, string ExtensionsDir, string WorkspaceDir);

    // Settings pre-seeded into the sandbox's User/settings.json before first launch; editor.accessibilitySupport is the load-bearing one — Monaco (the code editor and the integrated terminal's xterm view) renders as literally inaccessible to UI Automation until this is forced on, since VS Code otherwise only enables it when a real screen reader is detected.
    private const string SeedSettingsJson = """
        {
          "security.workspace.trust.enabled": false,
          "workbench.startupEditor": "none",
          "editor.accessibilitySupport": "on",
          "terminal.integrated.defaultProfile.windows": "Command Prompt",
          "update.mode": "none",
          "extensions.autoCheckUpdates": false,
          "extensions.autoUpdate": false,
          "telemetry.telemetryLevel": "off"
        }
        """;

    // Creates a fresh sandbox under the temp folder with settings.json pre-seeded; call KillAndDeleteAsync once the test is done with it.
    internal static VsCodeSandbox CreateSandbox()
    {
        var root = Path.Combine(Path.GetTempPath(), "ConsumerVsCodeTests", Guid.NewGuid().ToString("N"));
        var userDataDir = Path.Combine(root, "user-data");
        var extensionsDir = Path.Combine(root, "extensions");
        var workspaceDir = Path.Combine(root, "workspace");
        Directory.CreateDirectory(Path.Combine(userDataDir, "User"));
        Directory.CreateDirectory(extensionsDir);
        Directory.CreateDirectory(workspaceDir);
        File.WriteAllText(Path.Combine(userDataDir, "User", "settings.json"), SeedSettingsJson);
        return new VsCodeSandbox(root, userDataDir, extensionsDir, workspaceDir);
    }

    // Launches Code.exe against sandbox.WorkspaceDir in the isolated profile and waits for its own new window, then wraps it as an App.
    internal static async Task<App> LaunchAsync(VsCodeSandbox sandbox, CancellationToken ct = default)
    {
        var codeExe = FindCodeExe();
        var arguments = $"--new-window --user-data-dir \"{sandbox.UserDataDir}\" --extensions-dir \"{sandbox.ExtensionsDir}\" \"{sandbox.WorkspaceDir}\"";

        // Some dev environments (e.g. Claude Code's own host process) set this so their Node tooling can re-exec Electron as plain Node; VS Code inherits it and refuses to start as a GUI app at all ("bad option: --new-window") unless it's cleared — done here on the calling process since AppSession.LaunchAsync launches with UseShellExecute = true, which inherits the caller's environment rather than accepting a per-child override.
        Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", null);

        var app = await App.LaunchAsync(
            codeExe,
            new AppOptions { Arguments = arguments, LaunchTimeoutMs = 30_000, ImplicitWaitMs = 15_000, PollIntervalMs = 300, KillEntireProcessTree = true },
            // RequireNewWindow matters even for an isolated launch like this: without it, LaunchAsync's single-instance handoff detection could attach to a VS Code window the developer already has open elsewhere instead of waiting for this one.
            windowMatch: new WindowMatchOptions { RequireNewWindow = true },
            ct: ct);

        try
        {
            // Electron gives the window a real HWND well before the workbench UI has actually rendered, and Chromium only builds out its UI Automation tree lazily once queried, taking a few seconds either way — waiting for the "File" menu item to resolve rides out both before any keystrokes are sent.
            await app.WithOptions(new AppOptions { ImplicitWaitMs = 25_000, PollIntervalMs = 300 }).FindAsync(Locator.ByName("File"), ct);
        }
        catch
        {
            // The process launched successfully above, so it's ours to clean up if this warmup step is what fails — otherwise it (and the sandbox it holds file locks on) leaks, since the caller never got an App back to call KillAndDeleteAsync on. app already carries KillEntireProcessTree = true from the AppOptions passed to LaunchAsync above.
            await app.KillAsync();
            await app.DisposeAsync();
            throw;
        }
        return app;
    }

    // Kills VS Code (its whole process tree, via AppOptions.KillEntireProcessTree set at launch — VS Code's agent-host/shared-process/extension-host helpers aren't wrapped in a kill-on-close job object and would otherwise outlive a plain KillAsync, holding file locks on the sandbox dir), then best-effort deletes the sandbox.
    internal static async Task KillAndDeleteAsync(App app, VsCodeSandbox sandbox)
    {
        await app.KillAsync();
        await app.DisposeAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try { Directory.Delete(sandbox.RootDir, recursive: true); return; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 4)
                {
                    // Never seen in practice (KillEntireProcessTree above should leave nothing holding a lock), but silently giving up here would hide the one case that actually matters: KillEntireProcessTree failing to reach every helper process.
                    Console.Error.WriteLine($"Warning: could not delete sandbox directory '{sandbox.RootDir}': {ex.Message}");
                    return;
                }
                await Task.Delay(500);
            }
        }
    }

    // Checks the two install locations the official installer actually uses; per-user installs (the default from the VS Code website) land under LocalAppData, machine-wide installs under Program Files.
    private static string FindCodeExe()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft VS Code", "Code.exe"),
        ];
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("Visual Studio Code was not found at any known install location. Install it from https://code.visualstudio.com to run this suite.");
    }
}
