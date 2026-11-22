// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.CommandLine.Parsing;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

namespace AutoMancer.Cli.Commands;

// A locator plus an optional parent element to search inside; the one place a command turns "find this, maybe within that" into engine calls.
internal sealed record ScopedLocator(Locator Target, Locator? Scope)
{
    // Finds the first match, inside the scope when there is one.
    public Task<ElementHandle> FindAsync(App app, CancellationToken ct)
        => Scope is null ? app.FindAsync(Target, ct) : app.FindScopedAsync(Target, Scope, ct);

    // Counts the matches, inside the scope when there is one.
    public async Task<int> CountAsync(App app, CancellationToken ct)
        => (Scope is null ? await app.FindAllAsync(Target, ct) : await app.FindAllScopedAsync(Target, Scope, ct)).Count;

    // The locator to hand an action: the plain one when unscoped, otherwise the runtime-ID locator of the scoped match, since the engine's actions take no scope.
    public async Task<Locator> ResolveAsync(App app, CancellationToken ct)
        => Scope is null ? Target : Locator.ByRuntimeId((await app.FindScopedAsync(Target, Scope, ct)).Id);
}

// The --within-by/--within-value pair: an optional parent element to search inside.
internal sealed class ScopeOptions
{
    public Option<string?> By { get; } = new("--within-by") { Description = "Search only inside the element matching this strategy: name, id, class, control, path, runtime, xpath (requires --within-value)." };
    public Option<string?> Value { get; } = new("--within-value") { Description = "Value to match against --within-by." };

    // Adds both options to the command.
    public void AddTo(Command command)
    {
        command.Add(By);
        command.Add(Value);
    }

    // Parses the pair: null when neither is given; ArgumentException when only one is, or the strategy is unknown.
    public Locator? Parse(ParseResult result)
        => FindCommand.ParseOptionalLocator(result.GetValue(By), result.GetValue(Value), "--within-by", "--within-value");
}

// The --by/--value pair (required or optional) together with the scope pair, parsed into one ScopedLocator.
internal sealed class LocatorOptions
{
    public Option<string?> By { get; }
    public Option<string?> Value { get; }
    public ScopeOptions Scope { get; } = new();

    // Creates the options; byHelp overrides the --by description for commands where the locator means something specific.
    public LocatorOptions(bool required, string? byHelp = null)
    {
        By = new("--by") { Description = byHelp ?? "Locator strategy: name, id, class, control, path, runtime, xpath.", Required = required };
        Value = new("--value") { Description = "Value to match against the chosen strategy.", Required = required };
    }

    // Adds all four options to the command.
    public void AddTo(Command command)
    {
        command.Add(By);
        command.Add(Value);
        Scope.AddTo(command);
    }

    // Parses the locator and scope; null when no locator was given (only possible when the options are optional), ArgumentException for a lone half of a pair, a scope without a locator, or an xpath/path locator with a scope.
    public ScopedLocator? Parse(ParseResult result)
    {
        var target = FindCommand.ParseOptionalLocator(result.GetValue(By), result.GetValue(Value));
        var scope = Scope.Parse(result);
        if (scope is not null && target is null)
            throw new ArgumentException("--within-by and --within-value need --by and --value: they say where to look, not what to find.");
        // The engine always searches xpath and path locators from the window root, so a scope would be silently ignored; refuse instead of searching outside it.
        if (scope is not null && target is { Strategy: LocatorStrategy.AutoMancerXPath or LocatorStrategy.AutoMancerPath })
            throw new ArgumentException("--by xpath and --by path always search from the window root, so they can't be combined with --within-by/--within-value. Use name, id, class, control, or runtime to search inside a scope.");
        return target is null ? null : new ScopedLocator(target, scope);
    }
}
