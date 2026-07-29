// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.CommandLine.Invocation;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Cli.Commands;

// CLI command: find the first element matching a locator and print its properties.
internal static class FindCommand
{
    // Builds the 'find' sub-command with its arguments, options, and handler.
    internal static Command Build()
    {
        var sessionArg = new Argument<string>("session-id", "Session ID returned by 'launch'.");
        var byOpt     = new Option<string>("--by",    "Locator strategy: name, id, class, control, path, runtime, xpath.") { IsRequired = true };
        var valueOpt  = new Option<string>("--value", "Value to match against the chosen strategy.") { IsRequired = true };

        var command = new Command("find", "Find the first element matching a locator and print its properties.");
        command.AddArgument(sessionArg);
        command.AddOption(byOpt);
        command.AddOption(valueOpt);
        command.SetHandler(async (InvocationContext ctx) =>
        {
            var sessionId = ctx.ParseResult.GetValueForArgument(sessionArg);
            var by        = ctx.ParseResult.GetValueForOption(byOpt)!;
            var value     = ctx.ParseResult.GetValueForOption(valueOpt)!;

            var entry = SessionStore.Get(sessionId);
            if (entry is null)
            {
                Console.Error.WriteLine($"Session '{sessionId}' not found. Run 'automancer launch' first.");
                ctx.ExitCode = 1;
                return;
            }

            Locator locator;
            try { locator = ParseLocator(by, value); }
            catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); ctx.ExitCode = 1; return; }

            try
            {
                await using var app = await App.AttachByPidAsync(entry.ProcessId, ct: ctx.GetCancellationToken());
                var el = await app.FindAsync(locator, ctx.GetCancellationToken());
                Console.WriteLine($"Name:         {el.Name ?? "(none)"}");
                Console.WriteLine($"AutomationId: {el.AutomationId ?? "(none)"}");
                Console.WriteLine($"ControlType:  {el.ControlType ?? "(none)"}");
                Console.WriteLine($"ClassName:    {el.ClassName ?? "(none)"}");
                Console.WriteLine($"ResolvedVia:  {el.ResolvedVia}");
                Console.WriteLine($"Id:           {el.Id}");
                Console.WriteLine($"Rect:         {el.BoundingRect.X}, {el.BoundingRect.Y}, {el.BoundingRect.Width}x{el.BoundingRect.Height}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Find failed: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });
        return command;
    }

    // Converts a strategy name + value string into a Locator; throws ArgumentException for unknown strategies.
    internal static Locator ParseLocator(string by, string value) => by.ToLowerInvariant() switch
    {
        "name"    => Locator.ByName(value),
        "id"      => Locator.ByAutomationId(value),
        "class"   => Locator.ByClassName(value),
        "control" => Locator.ByControlType(value),
        "path"    => Locator.ByPath(value),
        "runtime" => Locator.ByRuntimeId(value),
        "xpath"   => Locator.ByXPath(value),
        _         => throw new ArgumentException(
                         $"Unknown strategy '{by}'. Valid: name, id, class, control, path, runtime, xpath"),
    };
}
