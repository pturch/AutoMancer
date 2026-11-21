// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.Diagnostics;
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;

namespace AutoMancer.Cli.Commands;

// CLI command: show, move, resize, minimize/maximize, close, or kill the target app's window.
internal static class WindowCommand
{
    // Builds the 'window' sub-command: at most one action, and with none it just reports the window.
    internal static Command Build()
    {
        var moveOpt   = new Option<string?>("--move", "Move the window's top-left corner to x,y in screen pixels (negative values are fine, like --move -1200,40).") { ArgumentHelpName = "x,y" };
        var resizeOpt = new Option<string?>("--resize", "Resize the window to width,height in pixels.") { ArgumentHelpName = "w,h" };
        var stateOpt  = new Option<string?>("--state", "Set the window state: normal, max, or min.") { ArgumentHelpName = "normal|max|min" };
        var closeOpt  = new Option<bool>("--close", "Close the window (like clicking its X). If the app then exits, it stops being the current target.");
        var killOpt   = new Option<bool>("--kill", "Terminate the app's process immediately and wait for it to exit. It stops being the current target.");

        var command = new Command("window", "Show the window's position and size, or move, resize, minimize/maximize, close, or kill it (one action per call).");
        foreach (var o in new Option[] { moveOpt, resizeOpt, stateOpt, closeOpt, killOpt })
            command.AddOption(o);
        command.SetHandler(ctx => CliSession.RunAsync(ctx, async (pid, ct) =>
        {
            // Step 1: read and validate the options before touching the app; at most one action is allowed.
            var r      = ctx.ParseResult;
            var move   = r.GetValueForOption(moveOpt);
            var resize = r.GetValueForOption(resizeOpt);
            var state  = r.GetValueForOption(stateOpt);
            var close  = r.GetValueForOption(closeOpt);
            var kill   = r.GetValueForOption(killOpt);
            if (new[] { move is not null, resize is not null, state is not null, close, kill }.Count(x => x) > 1)
                throw new ArgumentException("Use one of --move, --resize, --state, --close, or --kill per call.");
            var movePoint = move is null ? default : ParsePair(move, "--move", allowNegative: true);
            var resizeTo  = resize is null ? default : ParsePair(resize, "--resize", allowNegative: false);
            var windowState = state is null ? default : ParseState(state);

            // Step 2: attach and do the one action, if any.
            await using var app = await CliSession.AttachAsync(pid, ct);
            if (kill)
            {
                await app.KillAsync(ct: ct);
                var forgotten = ForgetTarget(pid);
                return new CliResult($"Killed PID {pid}." + (forgotten ? " It is no longer the current target." : ""), new { killed = true, pid, targetCleared = forgotten });
            }
            if (close)
            {
                await app.CloseWindowAsync(ct);
                await app.WaitForExitAsync(3000, ct);
                if (IsRunning(pid))
                    return new CliResult($"Close requested, but PID {pid} is still running; it may be showing a dialog, or it may own other windows.", new { closeRequested = true, exited = false, pid });
                var forgotten = ForgetTarget(pid);
                return new CliResult($"Closed; PID {pid} has exited." + (forgotten ? " It is no longer the current target." : ""), new { closeRequested = true, exited = true, pid, targetCleared = forgotten });
            }
            if (move is not null)
                await app.MoveWindowAsync(movePoint.A, movePoint.B, ct);
            if (resize is not null)
                await app.ResizeWindowAsync(resizeTo.A, resizeTo.B, ct);
            if (state is not null)
                await app.SetWindowStateAsync(windowState, ct);

            // Step 3: report the window as it is now.
            var rect = await app.GetWindowSizeAsync(ct);
            var minimized = app.IsWindowMinimized();
            var text = minimized
                ? "Window: minimized"
                : $"Window: x={rect.X}, y={rect.Y}, {rect.Width}x{rect.Height}";
            return new CliResult(text, new { rect, minimized });
        }));
        return command;
    }

    // Parses "a,b" into two whole numbers; throws ArgumentException naming the option when it isn't that shape, or when a value must be positive and isn't.
    internal static (int A, int B) ParsePair(string text, string option, bool allowNegative)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var a) || !int.TryParse(parts[1], out var b))
            throw new ArgumentException($"{option} needs two whole numbers separated by a comma, like {(allowNegative ? "100,50" : "800,600")}.");
        if (!allowNegative && (a < 1 || b < 1))
            throw new ArgumentException($"{option} needs positive numbers.");
        return (a, b);
    }

    // Maps normal, max/maximized, and min/minimized (any case) to the engine's WindowState; throws ArgumentException for anything else.
    internal static WindowState ParseState(string text) => text.ToLowerInvariant() switch
    {
        "normal" => WindowState.Normal,
        "max" or "maximized" => WindowState.Maximized,
        "min" or "minimized" => WindowState.Minimized,
        _ => throw new ArgumentException($"Unknown --state '{text}'. Use normal, max, or min."),
    };

    // Clears the current target if it is this process, so later commands don't try to use an app that is gone; returns whether it did.
    private static bool ForgetTarget(int pid)
    {
        var state = CliState.Load();
        if (state.Target?.Pid != pid)
            return false;
        CliState.Save(state with { Target = null });
        return true;
    }

    // True while a process with this ID exists and hasn't exited.
    private static bool IsRunning(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch (ArgumentException) { return false; }
    }
}
