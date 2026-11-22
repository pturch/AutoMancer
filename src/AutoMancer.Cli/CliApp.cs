// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.CommandLine.Help;
using AutoMancer.Cli.Commands;

namespace AutoMancer.Cli;

// Builds the complete command tree, so the program and the tests exercise exactly the same one.
public static class CliApp
{
    // Creates the root command with its global options and every sub-command.
    public static RootCommand BuildRoot()
    {
        var root = new RootCommand("AutoMancer — Windows UI automation CLI");
        // With no subcommand, show the command list (a successful, harmless request) instead of a "command required" error.
        root.SetAction(pr => new HelpAction().Invoke(pr));
        root.Add(CliSession.JsonOption);
        root.Add(CliSession.PidOption);
        root.Add(CliSession.VerboseOption);
        root.Add(LaunchCommand.Build());
        root.Add(ConfigureCommand.Build());
        root.Add(FindCommand.Build());
        root.Add(TreeCommand.Build());
        root.Add(ClickCommand.Build());
        root.Add(TypeCommand.Build());
        root.Add(ScreenshotCommand.Build());
        root.Add(AssertCommand.Build());
        root.Add(KeyCommand.Build());
        root.Add(HotkeyCommand.Build());
        root.Add(HoverCommand.Build());
        root.Add(WindowCommand.Build());
        root.Add(FocusCommand.Build());
        return root;
    }
}
