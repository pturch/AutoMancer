// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;

namespace AutoMancer.Cli.Commands;

// CLI command: find an element and give it keyboard focus.
internal static class FocusCommand
{
    // Builds the 'focus' sub-command with locator options.
    internal static Command Build()
    {
        var locatorOpts = new LocatorOptions(required: true);

        var command = new Command("focus", "Find an element and give it keyboard focus.");
        locatorOpts.AddTo(command);
        command.SetHandler(ctx => CliSession.RunAsync(ctx, async (pid, ct) =>
        {
            // Step 1: read the locator options and parse them before touching the app.
            var target = locatorOpts.Parse(ctx.ParseResult)!;

            // Step 2: attach and set focus.
            await using var app = await CliSession.AttachAsync(pid, ct);
            var locator = await target.ResolveAsync(app, ct);
            await app.SetFocusAsync(locator, ct);

            return new CliResult("Focused.", new { focused = true });
        }));
        return command;
    }
}
