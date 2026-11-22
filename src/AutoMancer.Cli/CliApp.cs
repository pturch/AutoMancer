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
        root.SetHandler(ctx => ctx.HelpBuilder.Write(new HelpContext(ctx.HelpBuilder, root, Console.Out)));
        root.AddGlobalOption(CliSession.JsonOption);
        root.AddGlobalOption(CliSession.PidOption);
        root.AddGlobalOption(CliSession.VerboseOption);
        root.AddCommand(LaunchCommand.Build());
        root.AddCommand(ConfigureCommand.Build());
        root.AddCommand(FindCommand.Build());
        root.AddCommand(TreeCommand.Build());
        root.AddCommand(ClickCommand.Build());
        root.AddCommand(TypeCommand.Build());
        root.AddCommand(ScreenshotCommand.Build());
        root.AddCommand(AssertCommand.Build());
        root.AddCommand(KeyCommand.Build());
        root.AddCommand(HotkeyCommand.Build());
        root.AddCommand(HoverCommand.Build());
        root.AddCommand(WindowCommand.Build());
        root.AddCommand(FocusCommand.Build());
        return root;
    }
}
