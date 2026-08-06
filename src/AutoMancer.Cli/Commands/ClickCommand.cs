// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.CommandLine.Invocation;
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;

namespace AutoMancer.Cli.Commands;

// CLI command: find an element and click it.
internal static class ClickCommand
{
    // Builds the 'click' sub-command with locator options, --double, and --right flags.
    internal static Command Build()
    {
        var sessionArg  = new Argument<string>("session-id", "Session ID returned by 'launch'.");
        var byOpt       = new Option<string>("--by",    "Locator strategy: name, id, class, control, path, runtime, xpath.") { IsRequired = true };
        var valueOpt    = new Option<string>("--value", "Value to match against the chosen strategy.") { IsRequired = true };
        var doubleOpt   = new Option<bool>("--double", "Send a double-click instead of a single click.");
        var rightOpt    = new Option<bool>("--right",  "Send a right-click instead of a left click.");

        var command = new Command("click", "Find an element and click it.");
        command.AddArgument(sessionArg);
        command.AddOption(byOpt);
        command.AddOption(valueOpt);
        command.AddOption(doubleOpt);
        command.AddOption(rightOpt);
        command.SetHandler(async (InvocationContext ctx) =>
        {
            var sessionId = ctx.ParseResult.GetValueForArgument(sessionArg);
            var by        = ctx.ParseResult.GetValueForOption(byOpt)!;
            var value     = ctx.ParseResult.GetValueForOption(valueOpt)!;
            var isDouble  = ctx.ParseResult.GetValueForOption(doubleOpt);
            var isRight   = ctx.ParseResult.GetValueForOption(rightOpt);

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
                if (isDouble)
                    await app.DoubleClickAsync(locator, ctx.GetCancellationToken());
                else
                    await app.ClickAsync(locator, isRight ? MouseButton.Right : MouseButton.Left, ct: ctx.GetCancellationToken());
                Console.WriteLine("Clicked.");
            }
            catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); ctx.ExitCode = 1; }
            catch (Exception ex)         { Console.Error.WriteLine($"Click failed: {ex.Message}"); ctx.ExitCode = 1; }
        });
        return command;
    }
}
