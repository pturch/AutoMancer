// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;

namespace AutoMancer.Cli.Commands;

// CLI command: move the mouse over an element without clicking, to trigger hover states and tooltips.
internal static class HoverCommand
{
    // Builds the 'hover' sub-command with locator options.
    internal static Command Build()
    {
        var locatorOpts = new LocatorOptions(required: true);

        var command = new Command("hover", "Move the mouse over an element without clicking.");
        locatorOpts.AddTo(command);
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, async (pid, ct) =>
        {
            // Step 1: read the locator options and parse them before touching the app.
            var target = locatorOpts.Parse(pr)!;

            // Step 2: attach and move the pointer to the element's center.
            await using var app = await CliSession.AttachAsync(pid, ct);
            var locator = await target.ResolveAsync(app, ct);
            await app.HoverAsync(locator, ct);

            return new CliResult("Hovered.", new { hovered = true });
        }));
        return command;
    }
}
