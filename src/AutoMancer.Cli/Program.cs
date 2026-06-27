// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using AutoMancer.Cli.Commands;

var root = new RootCommand("AutoMancer — Windows UI automation CLI");
root.AddCommand(LaunchCommand.Build());
root.AddCommand(FindCommand.Build());
root.AddCommand(TreeCommand.Build());
root.AddCommand(ClickCommand.Build());
root.AddCommand(TypeCommand.Build());
return await root.InvokeAsync(args);
