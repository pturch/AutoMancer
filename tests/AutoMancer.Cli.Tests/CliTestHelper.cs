// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.CommandLine.Parsing;

namespace AutoMancer.Cli.Tests;

// Invokes a CLI Command in-process, capturing its stdout/stderr and exit code without spawning a process.
internal static class CliTestHelper
{
    // Runs the complete command tree (as Program does) with the given arguments, capturing stdout, stderr and the exit code.
    internal static async Task<(string Stdout, string Stderr, int ExitCode)> RunAppAsync(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var exitCode = await CliSession.InvokeAsync(CliApp.BuildRoot(), args);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    // Arguments that point a command at this test process, which is running, so target resolution succeeds without launching anything.
    internal static readonly string[] Self = ["--pid", Environment.ProcessId.ToString()];

    // Arguments naming a PID no process has, to exercise the unattachable-target path.
    internal static readonly string[] Dead = ["--pid", "2000000000"];

    // Clears the saved current target so a test starts with none.
    internal static void ClearState() => CliState.Save(new CliStateData());

    // Mounts the command under a root carrying the global --json and --pid options, then redirects Console.Out/Error around a single command invocation and returns everything it wrote.
    internal static async Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(Command command, params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var root = new RootCommand();
            root.Add(CliSession.JsonOption);
            root.Add(CliSession.PidOption);
            root.Add(CliSession.VerboseOption);
            root.Add(command);
            var exitCode = await CliSession.InvokeAsync(root, [command.Name, .. args]);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
