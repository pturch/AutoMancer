// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;

namespace AutoMancer.Cli.Commands;

// CLI command: find an element and type text into it.
internal static class TypeCommand
{
    // Builds the 'type' sub-command with locator options and a text argument.
    internal static Command Build()
    {
        var textArg    = new Argument<string>("text") { Description = "Text to type into the element." };
        var locatorOpts = new LocatorOptions(required: true);

        var command = new Command("type", "Find an element and type text into it.");
        command.Add(textArg);
        locatorOpts.AddTo(command);
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, async (pid, ct) =>
        {
            // Step 1: read the arguments and parse the locator before touching the app.
            var text    = pr.GetRequiredValue(textArg);
            var target = locatorOpts.Parse(pr)!;

            // Step 2: attach and type the text into the element.
            await using var app = await CliSession.AttachAsync(pid, ct);
            var locator = await target.ResolveAsync(app, ct);
            await app.TypeAsync(locator, text, ct);

            return new CliResult($"Typed {text.Length} character(s).", new { typed = text.Length });
        }));
        return command;
    }
}
