// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;

namespace AutoMancer.Cli.Commands;

// CLI command: press a single named key against the window.
internal static class KeyCommand
{
    // Builds the 'key' sub-command with a key-name argument.
    internal static Command Build()
    {
        var keyArg     = new Argument<string>("key") { Description = "Key to press: Enter, Tab, Escape, F5, A, 7, Left, PageDown, ... (see 'hotkey' for combinations)." };

        var command = new Command("key", "Press a single key against the window.");
        command.Add(keyArg);
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, async (pid, ct) =>
        {
            // Step 1: parse the key name before touching the app.
            var key = KeyParser.ParseKey(pr.GetRequiredValue(keyArg));

            // Step 2: attach and press the key.
            await using var app = await CliSession.AttachAsync(pid, ct);
            await app.PressKeyAsync(key, ct: ct);

            var name = KeyParser.Name(key);
            return new CliResult($"Pressed {name}.", new { key = name });
        }));
        return command;
    }
}
