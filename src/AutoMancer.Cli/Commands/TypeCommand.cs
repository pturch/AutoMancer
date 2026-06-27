// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.CommandLine.Invocation;
using AutoMancer.Engine;

namespace AutoMancer.Cli.Commands;

// CLI command: find an element and type text into it.
internal static class TypeCommand
{
    // Builds the 'type' sub-command with locator options and a text argument.
    internal static Command Build()
    {
        var sessionArg = new Argument<string>("session-id", "Session ID returned by 'launch'.");
        var textArg    = new Argument<string>("text",       "Text to type into the element.");
        var byOpt      = new Option<string>("--by",    "Locator strategy: name, id, class, control, path, runtime, xpath.") { IsRequired = true };
        var valueOpt   = new Option<string>("--value", "Value to match against the chosen strategy.") { IsRequired = true };

        var command = new Command("type", "Find an element and type text into it.");
        command.AddArgument(sessionArg);
        command.AddArgument(textArg);
        command.AddOption(byOpt);
        command.AddOption(valueOpt);
        command.SetHandler(async (InvocationContext ctx) =>
        {
            var sessionId = ctx.ParseResult.GetValueForArgument(sessionArg);
            var text      = ctx.ParseResult.GetValueForArgument(textArg);
            var by        = ctx.ParseResult.GetValueForOption(byOpt)!;
            var value     = ctx.ParseResult.GetValueForOption(valueOpt)!;

            var entry = SessionStore.Get(sessionId);
            if (entry is null)
            {
                Console.Error.WriteLine($"Session '{sessionId}' not found. Run 'automancer launch' first.");
                ctx.ExitCode = 1;
                return;
            }

            try
            {
                var locator = FindCommand.ParseLocator(by, value);
                await using var app = await App.AttachByPidAsync(entry.ProcessId, ct: ctx.GetCancellationToken());
                await app.TypeAsync(locator, text, ctx.GetCancellationToken());
                Console.WriteLine($"Typed {text.Length} character(s).");
            }
            catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); ctx.ExitCode = 1; }
            catch (Exception ex)         { Console.Error.WriteLine($"Type failed: {ex.Message}"); ctx.ExitCode = 1; }
        });
        return command;
    }
}
