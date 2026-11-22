// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Cli.Commands;

// CLI command: snapshot the element tree and print it as an indented list.
internal static class TreeCommand
{
    // Builds the 'tree' sub-command with its argument and handler.
    internal static Command Build()
    {
        var depthOpt   = new Option<int?>("--depth") { Description = "Show at most this many levels below each root (0 = roots only); applied after --interactive-only." };
        var interactiveOpt = new Option<bool>("--interactive-only") { Description = "Keep only controls a user can act on (buttons, edits, list items, ...); their children move up to the nearest kept ancestor." };
        var scopeOpts  = new ScopeOptions();

        var command = new Command("tree", "Snapshot the element tree and print it.");
        command.Add(depthOpt);
        command.Add(interactiveOpt);
        scopeOpts.AddTo(command);
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, async (pid, ct) =>
        {
            // Step 1: read and validate the filter options before touching the app.
            var depth       = pr.GetValue(depthOpt);
            var interactive = pr.GetValue(interactiveOpt);
            if (depth < 0)
                throw new ArgumentException("--depth must be 0 or greater.");
            var scope = scopeOpts.Parse(pr);

            // Step 2: attach and take the full snapshot; every filter below only trims this result.
            await using var app = await CliSession.AttachAsync(pid, ct);
            IReadOnlyList<ElementSnapshot>? roots = await app.SnapshotAsync(ct);

            // Step 3: apply the filters in order: --within (start from one element), --interactive-only, then --depth.
            if (roots is not null && scope is not null)
            {
                var rootId = (await app.FindAsync(scope, ct)).Id;
                var found = roots.DescendantsAndSelf().FirstOrDefault(n => n.Id == rootId)
                    ?? throw new InvalidOperationException($"Element '{rootId}' resolved but is not in the tree snapshot.");
                roots = [found];
            }
            if (roots is not null && interactive)
                roots = [.. roots.SelectMany(KeepInteractive)];
            if (roots is not null && depth is { } maxDepth)
                roots = [.. roots.Select(r => Truncate(r, maxDepth))];

            // Step 4: render the remaining tree as indented text, or report it empty.
            if (roots is null || roots.Count == 0)
                return new CliResult("(empty tree)", new { roots = Array.Empty<ElementSnapshot>() });
            var sb = new System.Text.StringBuilder();
            foreach (var root in roots)
                Print(sb, root, 0);
            return new CliResult(sb.ToString().TrimEnd(), new { roots });
        }));
        return command;
    }

    // Control types treated as user-actionable by --interactive-only.
    private static readonly HashSet<string> InteractiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "CheckBox", "ComboBox", "Edit", "Hyperlink", "ListItem", "MenuItem", "RadioButton",
        "Slider", "Spinner", "SplitButton", "TabItem", "TreeItem", "Document", "DataItem",
    };

    // Returns this node if interactive (with filtered children), else its filtered descendants hoisted in its place.
    internal static IEnumerable<ElementSnapshot> KeepInteractive(ElementSnapshot node)
    {
        var children = node.Children.SelectMany(KeepInteractive).ToList();
        if (node.ControlType is { } t && InteractiveTypes.Contains(t))
            return [node with { Children = children }];
        return children;
    }

    // Copies the node, dropping everything deeper than remaining levels below it.
    internal static ElementSnapshot Truncate(ElementSnapshot node, int remaining)
        => node with { Children = remaining == 0 ? [] : [.. node.Children.Select(c => Truncate(c, remaining - 1))] };

    // Recursively prints a snapshot node and its children with depth-based indentation.
    private static void Print(System.Text.StringBuilder sb, ElementSnapshot node, int depth)
    {
        var indent = new string(' ', depth * 2);
        var type   = node.ControlType ?? node.ClassName ?? "?";
        var name   = node.Name is { Length: > 0 } n ? $"\"{n}\"" : "(no name)";
        var aid    = node.AutomationId is { Length: > 0 } a ? $"  [{a}]" : string.Empty;
        sb.AppendLine($"{indent}{type}  {name}{aid}");
        foreach (var child in node.Children)
            Print(sb, child, depth + 1);
    }
}
