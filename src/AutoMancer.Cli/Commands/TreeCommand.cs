// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.CommandLine.Invocation;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Cli.Commands;

// CLI command: snapshot the element tree and print it as an indented list.
internal static class TreeCommand
{
    // Builds the 'tree' sub-command with its argument and handler.
    internal static Command Build()
    {
        var sessionArg = new Argument<string>("session-id", "Session ID returned by 'launch'.");
        var command = new Command("tree", "Snapshot the element tree and print it.");
        command.AddArgument(sessionArg);
        command.SetHandler(async (InvocationContext ctx) =>
        {
            var sessionId = ctx.ParseResult.GetValueForArgument(sessionArg);
            var entry = SessionStore.Get(sessionId);
            if (entry is null)
            {
                Console.Error.WriteLine($"Session '{sessionId}' not found. Run 'automancer launch' first.");
                ctx.ExitCode = 1;
                return;
            }

            try
            {
                await using var app = await App.AttachByPidAsync(entry.ProcessId, ct: ctx.GetCancellationToken());
                var roots = await app.SnapshotAsync(ctx.GetCancellationToken());
                if (roots is null || roots.Count == 0)
                {
                    Console.WriteLine("(empty tree)");
                    return;
                }
                foreach (var root in roots)
                    Print(root, 0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Tree failed: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });
        return command;
    }

    // Recursively prints a snapshot node and its children with depth-based indentation.
    private static void Print(ElementSnapshot node, int depth)
    {
        var indent = new string(' ', depth * 2);
        var type   = node.ControlType ?? node.ClassName ?? "?";
        var name   = node.Name is { Length: > 0 } n ? $"\"{n}\"" : "(no name)";
        var aid    = node.AutomationId is { Length: > 0 } a ? $"  [{a}]" : string.Empty;
        Console.WriteLine($"{indent}{type}  {name}{aid}");
        foreach (var child in node.Children)
            Print(child, depth + 1);
    }
}
