// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;

namespace AutoMancer.Cli.Commands;

// CLI command: find an element and click it.
internal static class ClickCommand
{
    // Builds the 'click' sub-command with locator options, --double, and --right flags.
    internal static Command Build()
    {
        var locatorOpts = new LocatorOptions(required: true);
        var doubleOpt   = new Option<bool>("--double") { Description = "Send a double-click instead of a single click." };
        var rightOpt    = new Option<bool>("--right") { Description = "Send a right-click instead of a left click." };

        var command = new Command("click", "Find an element and click it.");
        locatorOpts.AddTo(command);
        command.Add(doubleOpt);
        command.Add(rightOpt);
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, async (pid, ct) =>
        {
            // Step 1: read the options and parse the locator before touching the app.
            var isDouble = pr.GetValue(doubleOpt);
            var isRight  = pr.GetValue(rightOpt);
            var target  = locatorOpts.Parse(pr)!;

            // Step 2: attach and send a double-click, or a single left or right click.
            await using var app = await CliSession.AttachAsync(pid, ct);
            var locator = await target.ResolveAsync(app, ct);
            if (isDouble)
                await app.DoubleClickAsync(locator, ct);
            else
                await app.ClickAsync(locator, isRight ? MouseButton.Right : MouseButton.Left, ct: ct);

            return new CliResult("Clicked.", new { clicked = true });
        }));
        return command;
    }
}
