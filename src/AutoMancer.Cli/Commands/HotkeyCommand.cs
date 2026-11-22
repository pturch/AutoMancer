// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;

namespace AutoMancer.Cli.Commands;

// CLI command: press a key combination such as ctrl+a against the window.
internal static class HotkeyCommand
{
    // Builds the 'hotkey' sub-command with a chord argument.
    internal static Command Build()
    {
        var chordArg   = new Argument<string>("chord") { Description = "Keys joined by '+', like ctrl+a or ctrl+shift+s. Modifiers: ctrl, shift, alt, win." };

        var command = new Command("hotkey", "Press a key combination (like ctrl+s) against the window.");
        command.Add(chordArg);
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, async (pid, ct) =>
        {
            // Step 1: parse the chord into modifiers and keys before touching the app.
            var chord = pr.GetRequiredValue(chordArg);
            var (modifiers, keys) = KeyParser.ParseChord(chord);

            // Step 2: attach and press all the keys together with the modifiers held.
            await using var app = await CliSession.AttachAsync(pid, ct);
            await app.HotkeyAsync(modifiers, keys, ct);

            return new CliResult($"Pressed {chord}.", new { chord, modifiers, keys = keys.Select(KeyParser.Name) });
        }));
        return command;
    }
}
