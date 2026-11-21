// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using AutoMancer.Cli.Commands;
using AutoMancer.Engine.Actions;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class InputCommandTests
{
    // Each input command with the arguments that make its parse valid.
    public static TheoryData<string, string[]> Cases() => new()
    {
        { "key", ["enter"] },
        { "hotkey", ["ctrl+a"] },
        { "hover", ["--by", "name", "--value", "x"] },
    };

    // Builds the named input command.
    private static Command Build(string name) => name switch
    {
        "key" => KeyCommand.Build(),
        "hotkey" => HotkeyCommand.Build(),
        _ => HoverCommand.Build(),
    };

    // Runs the named command against this test process as the target.
    private static Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(string name, params string[] args)
        => CliTestHelper.RunAsync(Build(name), [.. CliTestHelper.Self, .. args]);

    // With no --pid and no current target, the command fails with the target-not-found exit code.
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NoTarget_ExitsTargetNotFound(string name, string[] args)
    {
        CliTestHelper.ClearState();

        var (_, stderr, exitCode) = await CliTestHelper.RunAsync(Build(name), args);

        Assert.Equal(CliExitCode.TargetNotFound, exitCode);
        Assert.Contains("No target", stderr);
    }

    // A bad key, chord, or strategy is a usage error raised before attaching to the process.
    [Theory]
    [InlineData("key", "bogus")]
    [InlineData("key", "5000")]
    [InlineData("hotkey", "ctrl+shift")]
    [InlineData("hotkey", "ctrl++a")]
    [InlineData("hotkey", "ctrl+bogus")]
    public async Task BadKeyOrChord_IsUsageError(string name, string arg)
    {
        var (_, _, exitCode) = await RunAsync(name, arg);

        Assert.Equal(CliExitCode.Usage, exitCode);
    }

    // An unknown --by strategy on hover is a usage error.
    [Fact]
    public async Task Hover_UnknownStrategy_IsUsageError()
    {
        var (_, stderr, exitCode) = await RunAsync("hover", "--by", "css", "--value", "x");

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Contains("Unknown strategy 'css'", stderr);
    }

    // Key names resolve case-insensitively, digits map to D0-D9, and aliases work.
    [Theory]
    [InlineData("enter", Key.Enter)]
    [InlineData("ENTER", Key.Enter)]
    [InlineData("F5", Key.F5)]
    [InlineData("a", Key.A)]
    [InlineData("7", Key.D7)]
    [InlineData("0", Key.D0)]
    [InlineData("esc", Key.Escape)]
    [InlineData("pgdn", Key.PageDown)]
    [InlineData("PageDown", Key.PageDown)]
    public void ParseKey_ResolvesNamesDigitsAndAliases(string name, Key expected)
    {
        Assert.Equal(expected, KeyParser.ParseKey(name));
    }

    // Names that aren't keys, including numbers that would otherwise parse as enum values, are rejected.
    [Theory]
    [InlineData("")]
    [InlineData("bogus")]
    [InlineData("12")]
    [InlineData("999")]
    public void ParseKey_Invalid_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => KeyParser.ParseKey(name));
    }

    // Keys print as something that can be typed back in: digits as "7", everything else by its name.
    [Theory]
    [InlineData(Key.D7, "7")]
    [InlineData(Key.D0, "0")]
    [InlineData(Key.Enter, "Enter")]
    [InlineData(Key.A, "A")]
    public void Name_PrintsDigitsAsDigits(Key key, string expected)
    {
        Assert.Equal(expected, KeyParser.Name(key));
        Assert.Equal(key, KeyParser.ParseKey(KeyParser.Name(key)));
    }

    // A chord splits into held modifiers and pressed keys, in any case and with spaces around the plus signs.
    [Fact]
    public void ParseChord_SplitsModifiersAndKeys()
    {
        var (modifiers, keys) = KeyParser.ParseChord("Ctrl + Shift+s");

        Assert.Equal(new KeyModifiers(Shift: true, Control: true), modifiers);
        Assert.Equal([Key.S], keys);
    }

    // Several non-modifier keys form a multi-key chord, and the Windows and Alt aliases work.
    [Fact]
    public void ParseChord_MultipleKeysAndAliases()
    {
        var (modifiers, keys) = KeyParser.ParseChord("control+alt+win+a+k");

        Assert.Equal(new KeyModifiers(Control: true, Alt: true, Win: true), modifiers);
        Assert.Equal([Key.A, Key.K], keys);
    }

    // A chord with no non-modifier key, or an empty segment, is rejected.
    [Theory]
    [InlineData("ctrl")]
    [InlineData("ctrl+shift")]
    [InlineData("ctrl++a")]
    [InlineData("+a")]
    public void ParseChord_Invalid_Throws(string chord)
    {
        Assert.Throws<ArgumentException>(() => KeyParser.ParseChord(chord));
    }
}
