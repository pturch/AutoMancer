// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.CommandLine.Invocation;
using AutoMancer.Engine;

namespace AutoMancer.Cli.Commands;

// CLI command: launch an executable and register the resulting session.
internal static class LaunchCommand
{
    // Builds the 'launch' sub-command with its argument and handler.
    internal static Command Build()
    {
        var execArg = new Argument<string>("executable", "Path to the executable to launch.");
        var argsOpt = new Option<string?>("--args", "Command-line arguments to pass to the executable.");
        var command = new Command("launch", "Launch an application and register it as a session.");
        command.AddArgument(execArg);
        command.AddOption(argsOpt);
        command.SetHandler(async (InvocationContext ctx) =>
        {
            var exe = ctx.ParseResult.GetValueForArgument(execArg);
            var arguments = ctx.ParseResult.GetValueForOption(argsOpt);
            try
            {
                var app = await App.LaunchAsync(exe, new AppOptions { Arguments = arguments }, ct: ctx.GetCancellationToken());
                var sessionId = Guid.NewGuid().ToString("N");
                SessionStore.Save(new SessionEntry(sessionId, app.ProcessId, exe));
                Console.WriteLine($"Session: {sessionId}");
                Console.WriteLine($"PID:     {app.ProcessId}");
                Console.WriteLine($"Path:    {exe}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Launch failed: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });
        return command;
    }
}
