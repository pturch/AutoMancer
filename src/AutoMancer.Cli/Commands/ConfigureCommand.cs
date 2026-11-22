// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Cli.Commands;

// CLI command: show or change the current target app and the saved engine settings.
internal static class ConfigureCommand
{
    private static readonly string[] ProviderNames = ["uia3", "uia2", "win32"];

    // Builds the 'configure' sub-command: attach/clear the target, save or reset settings, and show the result.
    internal static Command Build()
    {
        var showOpt        = new Option<bool>("--show", "Show the current target and settings (this is also what happens with no options).");
        var attachOpt      = new Option<string?>("--attach", "Attach to a running app and make it the current target: a number is a PID, otherwise a process name (notepad or notepad.exe), otherwise part of a window title. Several matches is an error that lists them.") { ArgumentHelpName = "pid-or-title" };
        var clearOpt       = new Option<bool>("--clear", "Forget the current target.");
        var resetOpt       = new Option<bool>("--reset", "Reset all saved settings to the engine defaults.");
        var implicitOpt    = new Option<int?>("--implicit-wait", "How long, in ms, a find waits for an element to appear (default 5000).") { ArgumentHelpName = "ms" };
        var pollOpt        = new Option<int?>("--poll-interval", "How often, in ms, the engine re-checks while waiting (default 500).") { ArgumentHelpName = "ms" };
        var actionOpt      = new Option<int?>("--action-delay", "Pause, in ms, after each action so the UI can settle (default 150).") { ArgumentHelpName = "ms" };
        var foregroundOpt  = new Option<int?>("--foreground-timeout", "How long, in ms, to retry bringing a window to the front (default 3000).") { ArgumentHelpName = "ms" };
        var reresolveOpt   = new Option<bool?>("--reresolve-on-stale", "Re-find an element that went stale mid-action: true or false (default true).") { Arity = ArgumentArity.ExactlyOne, ArgumentHelpName = "true|false" };
        var providersOpt   = new Option<string?>("--provider-chain", "Comma-separated element providers to try, in order, from uia3, uia2, win32 (default uia3,uia2,win32).") { ArgumentHelpName = "list" };

        var command = new Command("configure", "Show or change the current target app and the saved engine settings.");
        foreach (var o in new Option[] { showOpt, attachOpt, clearOpt, resetOpt, implicitOpt, pollOpt, actionOpt, foregroundOpt, reresolveOpt, providersOpt })
            command.AddOption(o);
        command.SetHandler(ctx => CliSession.RunAsync(ctx, async ct =>
        {
            // Step 1: read and validate everything before changing any state.
            var r        = ctx.ParseResult;
            var attach   = r.GetValueForOption(attachOpt);
            var clear    = r.GetValueForOption(clearOpt);
            var reset    = r.GetValueForOption(resetOpt);
            var implicitWait = r.GetValueForOption(implicitOpt);
            var poll         = r.GetValueForOption(pollOpt);
            var action       = r.GetValueForOption(actionOpt);
            var foreground   = r.GetValueForOption(foregroundOpt);
            var reresolve    = r.GetValueForOption(reresolveOpt);
            var providers    = r.GetValueForOption(providersOpt)?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (attach is not null && clear)
                throw new ArgumentException("--attach and --clear can't be used together.");
            if (implicitWait < 0 || action < 0 || foreground < 0)
                throw new ArgumentException("Timing values must be 0 or greater.");
            if (poll < 1)
                throw new ArgumentException("--poll-interval must be at least 1.");
            if (providers is not null && (providers.Length == 0 || providers.Any(p => !ProviderNames.Contains(p, StringComparer.OrdinalIgnoreCase))))
                throw new ArgumentException($"--provider-chain must be a comma-separated list of: {string.Join(", ", ProviderNames)}.");

            // Step 2: apply the changes in order: reset settings, then new settings, then clear or attach the target.
            var state = CliState.Load();
            var settings = reset ? new CliSettings() : state.Settings;
            settings = settings with
            {
                ImplicitWaitMs = implicitWait ?? settings.ImplicitWaitMs,
                PollIntervalMs = poll ?? settings.PollIntervalMs,
                ActionDelayMs = action ?? settings.ActionDelayMs,
                ForegroundActivationTimeoutMs = foreground ?? settings.ForegroundActivationTimeoutMs,
                ReresolveOnStale = reresolve ?? settings.ReresolveOnStale,
                ProviderChain = providers?.Select(p => p.ToLowerInvariant()).Distinct().ToArray() ?? settings.ProviderChain,
            };
            var target = clear ? null : state.Target;
            if (attach is not null)
                target = await AttachAsync(attach, ct);
            state = new CliStateData { Target = target, Settings = settings };
            CliState.Save(state);

            // Step 3: report the resulting state.
            return Describe(state);
        }));
        return command;
    }

    // Attaches to a running app and returns it as the new target; fails if the text matches no app, matches several, or the app has no main window.
    private static async Task<CliTarget> AttachAsync(string pidOrName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pidOrName))
            throw new ArgumentException("--attach needs a PID or a process name.");
        var pid = ResolveAttachTarget(pidOrName, WindowedApps());
        try
        {
            // The App is deliberately not disposed: disposing an attached App would kill the user's app.
            var app = await App.AttachByPidAsync(pid, new CliSettings().ToAppOptions(verbose: CliSession.Verbose), ct: ct);
            return CliTarget.ForProcess(app.ProcessId);
        }
        catch (AppLaunchError ex)
        {
            throw new TargetNotFoundException($"{ex.Message} Pass the PID of the process that owns the window.", ex);
        }
    }

    // One running app that has a main window, as the attach matcher sees it.
    internal sealed record WindowedApp(int Pid, string Name, string Title);

    // Lists the running processes that have a main window.
    private static List<WindowedApp> WindowedApps()
    {
        var apps = new List<WindowedApp>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                        apps.Add(new WindowedApp(process.Id, process.ProcessName, process.MainWindowTitle));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // The process exited or can't be inspected; it can't be a target anyway.
                }
            }
        }
        return apps;
    }

    // Turns what the user typed into one PID: a number is a PID, then an exact process name (with or without .exe), then a window-title match; no match is target-not-found and several matches is a usage error listing them.
    internal static int ResolveAttachTarget(string text, IReadOnlyList<WindowedApp> apps)
    {
        if (int.TryParse(text, out var pid))
            return pid;

        var name = text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? text[..^4] : text;
        var matches = apps.Where(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
            matches = [.. apps.Where(a => a.Title.Contains(text, StringComparison.OrdinalIgnoreCase))];

        return matches.Count switch
        {
            1 => matches[0].Pid,
            0 => throw new TargetNotFoundException($"No running app with a window matches '{text}' by process name or window title. Pass a PID with --attach <pid>."),
            _ => throw new ArgumentException($"'{text}' matches several apps: {string.Join("; ", matches.Select(a => $"PID {a.Pid} {a.Name} \"{a.Title}\""))}. Pass one with --attach <pid>."),
        };
    }

    // Builds the text and JSON views of the current target and every effective setting with where its value came from.
    private static CliResult Describe(CliStateData state)
    {
        var s = state.Settings;
        var d = AppOptions.Default;
        var rows = new (string Name, string Flag, object Value, bool Saved)[]
        {
            ("implicitWaitMs", "implicit-wait", s.ImplicitWaitMs ?? d.ImplicitWaitMs, s.ImplicitWaitMs is not null),
            ("pollIntervalMs", "poll-interval", s.PollIntervalMs ?? d.PollIntervalMs, s.PollIntervalMs is not null),
            ("actionDelayMs", "action-delay", s.ActionDelayMs ?? d.ActionDelayMs, s.ActionDelayMs is not null),
            ("foregroundActivationTimeoutMs", "foreground-timeout", s.ForegroundActivationTimeoutMs ?? d.ForegroundActivationTimeoutMs, s.ForegroundActivationTimeoutMs is not null),
            ("reresolveOnStale", "reresolve-on-stale", s.ReresolveOnStale ?? d.ReresolveOnStale, s.ReresolveOnStale is not null),
            ("providerChain", "provider-chain", string.Join(",", s.ProviderChain ?? [.. d.ProviderChain]), s.ProviderChain is not null),
        };

        var alive = state.Target?.IsAlive();
        var targetText = state.Target is { } t
            ? $"PID {t.Pid} ({t.Name}), {(alive == true ? "running" : "no longer running")}"
            : "(none)";
        var text = string.Join(Environment.NewLine,
            [$"Target: {targetText}", "Settings:", .. rows.Select(x => $"  {x.Flag,-20} {x.Value,-18} ({(x.Saved ? "saved" : "default")})")]);

        var settings = rows.ToDictionary(x => x.Name, x => (object)new { value = x.Value, source = x.Saved ? "saved" : "default" });
        var target = state.Target is { } tt ? new { pid = tt.Pid, name = tt.Name, alive = alive == true } : null;
        return new CliResult(text, new { target, settings });
    }
}
