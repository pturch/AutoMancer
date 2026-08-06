// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;

namespace AutoMancer.Cli.Tests;

// Invokes a CLI Command in-process, capturing its stdout/stderr and exit code without spawning a process.
internal static class CliTestHelper
{
    // Redirects Console.Out/Error around a single command invocation and returns everything it wrote.
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
            var exitCode = await command.InvokeAsync(args);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
