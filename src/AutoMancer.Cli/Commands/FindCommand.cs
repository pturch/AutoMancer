// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Cli.Commands;

// CLI command: find the first element matching a locator and print its properties.
internal static class FindCommand
{
    // Builds the 'find' sub-command with its arguments, options, and handler.
    internal static Command Build()
    {
        var locatorOpts = new LocatorOptions(required: true);

        var command = new Command("find", "Find the first element matching a locator and print its properties.");
        locatorOpts.AddTo(command);
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, async (pid, ct) =>
        {
            // Step 1: read the locator options and parse them before touching the app.
            var by         = pr.GetValue(locatorOpts.By)!;
            var value      = pr.GetValue(locatorOpts.Value)!;
            var scopeBy    = pr.GetValue(locatorOpts.Scope.By);
            var scopeValue = pr.GetValue(locatorOpts.Scope.Value);
            var target     = locatorOpts.Parse(pr)!;

            // Step 2: attach and find the first matching element, inside the scope when one was given.
            await using var app = await CliSession.AttachAsync(pid, ct);
            var el = await target.FindAsync(app, ct);

            // Step 3: build the aligned text view of the element's properties.
            var text = string.Join(Environment.NewLine,
                $"Name:         {el.Name ?? "(none)"}",
                $"AutomationId: {el.AutomationId ?? "(none)"}",
                $"ControlType:  {el.ControlType ?? "(none)"}",
                $"ClassName:    {el.ClassName ?? "(none)"}",
                $"ResolvedVia:  {el.ResolvedVia}",
                $"Id:           {el.Id}",
                $"Rect:         {el.BoundingRect.X}, {el.BoundingRect.Y}, {el.BoundingRect.Width}x{el.BoundingRect.Height}");

            // Step 4: build the JSON, including the locator and scope that were used and a runtime-id locator that can be reused while the element lives.
            var data = new
            {
                el.Name, el.AutomationId, el.ControlType, el.ClassName, el.ResolvedVia, el.Id,
                rect = el.BoundingRect,
                locator = new { by = by.ToLowerInvariant(), value },
                scope = scopeBy is null ? null : new { by = scopeBy.ToLowerInvariant(), value = scopeValue },
                runtimeLocator = new { by = "runtime", value = el.Id },
            };
            return new CliResult(text, data);
        }));
        return command;
    }

    // Parses an optional --by/--value pair: null when both are absent, ArgumentException when only one is given.
    internal static Locator? ParseOptionalLocator(string? by, string? value, string byName = "--by", string valueName = "--value")
    {
        if (by is null && value is null)
            return null;
        if (by is null || value is null)
            throw new ArgumentException($"{byName} and {valueName} must be given together.");
        return ParseLocator(by, value);
    }

    // Converts a strategy name + value string into a Locator; throws ArgumentException for unknown strategies.
    internal static Locator ParseLocator(string by, string value) => value.Length == 0
        ? throw new ArgumentException("A locator value can't be empty; an unset variable would match an arbitrary element.")
        : ParseStrategy(by, value);

    // Maps the strategy name to a locator; split out so the empty-value check above runs first.
    private static Locator ParseStrategy(string by, string value) => by.ToLowerInvariant() switch
    {
        "name"    => Locator.ByName(value),
        "id"      => Locator.ByAutomationId(value),
        "class"   => Locator.ByClassName(value),
        "control" => Locator.ByControlType(value),
        "path"    => Locator.ByPath(value),
        "runtime" => Locator.ByRuntimeId(value),
        "xpath"   => Locator.ByXPath(value),
        _         => throw new ArgumentException(
                         $"Unknown strategy '{by}'. Valid: name, id, class, control, path, runtime, xpath"),
    };
}
