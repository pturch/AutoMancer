// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;

namespace AutoMancer.Cli.Commands;

// Turns key names and chords like "ctrl+shift+s" from the command line into the engine's Key and KeyModifiers.
internal static class KeyParser
{
    private static readonly Dictionary<string, Key> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["esc"] = Key.Escape, ["return"] = Key.Enter, ["del"] = Key.Delete, ["ins"] = Key.Insert,
        ["pgup"] = Key.PageUp, ["pgdn"] = Key.PageDown,
    };

    // Parses one key name: an engine Key name (case-insensitive), a digit 0-9, or a short alias like esc; throws ArgumentException for anything else.
    internal static Key ParseKey(string name)
    {
        if (name.Length == 1 && char.IsAsciiDigit(name[0]))
            return Key.D0 + (name[0] - '0');
        if (Aliases.TryGetValue(name, out var alias))
            return alias;
        // A purely numeric name would parse as an enum's underlying value, so only accept names.
        if (!name.All(char.IsAsciiDigit) && Enum.TryParse<Key>(name, ignoreCase: true, out var key) && Enum.IsDefined(key))
            return key;
        throw new ArgumentException($"Unknown key '{name}'. Use a name like Enter, Tab, Escape, F5, A, 7, Left, PageDown.");
    }

    // The name to show for a key: digits print as "7", not the engine's "D7", so the output can be typed back as input.
    internal static string Name(Key key) => key is >= Key.D0 and <= Key.D9 ? (key - Key.D0).ToString() : key.ToString();

    // Parses a chord such as "ctrl+a" or "ctrl+shift+s" into its held modifiers and the keys pressed with them; needs at least one non-modifier key.
    internal static (KeyModifiers Modifiers, IReadOnlyList<Key> Keys) ParseChord(string chord)
    {
        bool shift = false, control = false, alt = false, win = false;
        var keys = new List<Key>();
        foreach (var raw in chord.Split('+'))
        {
            var token = raw.Trim();
            if (token.Length == 0)
                throw new ArgumentException($"Can't parse chord '{chord}'. Use keys joined by '+', like ctrl+shift+s.");
            switch (token.ToLowerInvariant())
            {
                case "ctrl" or "control": control = true; break;
                case "shift": shift = true; break;
                case "alt": alt = true; break;
                case "win" or "windows": win = true; break;
                default: keys.Add(ParseKey(token)); break;
            }
        }
        if (keys.Count == 0)
            throw new ArgumentException($"Chord '{chord}' has only modifiers; add a key, like {chord}+a.");
        return (new KeyModifiers(shift, control, alt, win), keys);
    }
}
