// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using AutoMancer.Engine;

namespace AutoMancer.Cli.Commands;

// CLI command: launch an executable and make it the current target.
internal static class LaunchCommand
{
    // Builds the 'launch' sub-command with its argument and handler.
    internal static Command Build()
    {
        var execArg = new Argument<string>("executable", "Path to the executable to launch.");
        var argsOpt = new Option<string?>("--args", "Command-line arguments to pass to the executable.");
        var command = new Command("launch", "Launch an application and make it the current target.");
        command.AddArgument(execArg);
        command.AddOption(argsOpt);
        command.SetHandler(ctx => CliSession.RunAsync(ctx, async ct =>
        {
            // Step 1: read the options and start the app with the saved settings.
            var exe       = ctx.ParseResult.GetValueForArgument(execArg);
            var arguments = ctx.ParseResult.GetValueForOption(argsOpt);
            if (string.IsNullOrWhiteSpace(exe))
                throw new ArgumentException("launch needs the path or name of an executable.");
            var app       = await App.LaunchAsync(exe, CliState.Load().Settings.ToAppOptions(arguments: arguments, verbose: CliSession.Verbose), ct: ct);

            // Step 2: make it the current target, so later commands act on it unless --pid says otherwise.
            var state = CliState.Load();
            CliState.Save(state with { Target = CliTarget.ForProcess(app.ProcessId) });

            var text = $"PID:  {app.ProcessId}{Environment.NewLine}Path: {exe}{Environment.NewLine}This is now the default target for commands, to override pass --pid or use configure to change the default.";
            var data = new { pid = app.ProcessId, path = exe };
            return new CliResult(text, data);
        }));
        return command;
    }
}
