// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.RegularExpressions;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Cli.Commands;

// One evaluated check: what was expected, what was observed, whether they matched, and an optional breakdown shown on failure.
internal sealed record AssertCheck(string Check, object? Expected, object? Actual, bool Passed, string? Detail = null);

// The shape of value a check takes on the command line.
internal enum CheckKind { Bool, Text, Number }

// How a check's value is compared: none (boolean), exact, substring, or regex.
internal enum CheckOp { None, Equals, Contains, Matches }

// One parsed --check expression, ready to evaluate.
internal sealed record ParsedCheck(string Name, CheckKind Kind, CheckOp Op, bool Want, string Value, int Number, Regex? Pattern)
{
    // The name shown in reports, spelling out the comparison for text checks (e.g. "name contains").
    internal string Label => Kind == CheckKind.Text && Op != CheckOp.Equals ? $"{Name} {(Op == CheckOp.Contains ? "contains" : "matches")}" : Name;
}

// Parses and evaluates the --check expression language used by 'assert'.
internal static class AssertChecks
{
    private static readonly Dictionary<string, CheckKind> Kinds = new()
    {
        ["exists"] = CheckKind.Bool, ["enabled"] = CheckKind.Bool, ["visible"] = CheckKind.Bool, ["clickable"] = CheckKind.Bool,
        ["unobscured"] = CheckKind.Bool, ["stable"] = CheckKind.Bool,
        ["name"] = CheckKind.Text, ["text"] = CheckKind.Text, ["automation-id"] = CheckKind.Text, ["class-name"] = CheckKind.Text, ["control-type"] = CheckKind.Text,
        ["count"] = CheckKind.Number,
    };

    // True for checks that run on the whole window without a locator.
    internal static bool IsWindowLevel(ParsedCheck c) => c.Name == "stable";

    // True for the check that asserts absence (!exists / exists=false).
    internal static bool IsAbsent(ParsedCheck c) => c.Name == "exists" && !c.Want;

    // Parses one expression: name, !name, name=value, name~value (contains), or name=~regex; throws ArgumentException on anything else.
    internal static ParsedCheck Parse(string expr)
    {
        // Step 1: strip a leading '!' (negation) and split the rest into the check name and the operator+value tail.
        var text = expr.TrimStart();
        var negate = text.StartsWith('!');
        if (negate)
            text = text[1..];
        var nameEnd = 0;
        while (nameEnd < text.Length && (char.IsAsciiLetterLower(text[nameEnd]) || text[nameEnd] == '-'))
            nameEnd++;
        var name = text[..nameEnd];
        var rest = text[nameEnd..];
        // Spaces after a bare name are noise, but after an operator they are part of the value.
        if (string.IsNullOrWhiteSpace(rest))
            rest = "";

        // Step 2: the name must be a known check; its kind decides what value shape is legal.
        if (!Kinds.TryGetValue(name, out var kind))
            throw new ArgumentException($"Unknown check '{name}' in '{expr}'. Valid: {string.Join(", ", Kinds.Keys)}");

        // Step 3: read the operator (=~ before = so regex isn't mistaken for equals) and the value after it.
        var (op, value) = rest switch
        {
            ""                           => (CheckOp.None, ""),
            _ when rest.StartsWith("=~") => (CheckOp.Matches, rest[2..]),
            _ when rest.StartsWith('=')  => (CheckOp.Equals, rest[1..]),
            _ when rest.StartsWith('~')  => (CheckOp.Contains, rest[1..]),
            _                            => throw new ArgumentException($"Can't parse check '{expr}'. Use name, !name, name=value, name~value or name=~regex."),
        };
        
        // '!' only makes sense on a bare check, never alongside a value.
        if (negate && op != CheckOp.None)
            throw new ArgumentException($"'{expr}': '!' negates a bare check and takes no value.");

        // Step 4: validate the operator and value for this kind and build the parsed check.
        switch (kind)
        {
            // Boolean: bare means true, '!' means false, or an explicit =true / =false.
            case CheckKind.Bool:
                var want = negate ? false : op == CheckOp.None ? true
                    : op == CheckOp.Equals && bool.TryParse(value, out var b) ? b
                    : throw new ArgumentException($"'{expr}': {name} takes no value, or =true / =false.");
                return new(name, kind, CheckOp.None, want, "", 0, null);
            // Text: needs an operator and a non-empty value; regexes compile now so a bad pattern fails as a usage error.
            case CheckKind.Text:
                if (negate || op == CheckOp.None || value.Length == 0)
                    throw new ArgumentException($"'{expr}': {name} needs =value, ~value (contains) or =~regex.");
                return new(name, kind, op, true, value, 0, op == CheckOp.Matches ? new Regex(value, RegexOptions.None, TimeSpan.FromSeconds(1)) : null);
            // Number: only =N with N >= 0.
            default:
                if (negate || op != CheckOp.Equals || !int.TryParse(value, out var n) || n < 0)
                    throw new ArgumentException($"'{expr}': {name} needs =N with N 0 or greater.");
                return new(name, kind, op, true, value, n, null);
        }
    }

    // Diffs two PNG files and reports the result; a size mismatch fails with the dimensions spelled out, since the pixel diff would only say 100%.
    internal static AssertCheck CompareImages(string expectedPath, string actualPath, double tolerance)
    {
        var expected = File.ReadAllBytes(expectedPath);
        var actual = File.ReadAllBytes(actualPath);
        var (ew, eh) = PngSize(expected, expectedPath);
        var (aw, ah) = PngSize(actual, actualPath);
        var wanted = $"<= {tolerance:P2} pixels differing";
        if ((ew, eh) != (aw, ah))
            return new("images-match", wanted, "sizes differ", false, $"expected is {ew}x{eh}, actual is {aw}x{ah}");

        var ratio = PixelDiff.DifferenceRatio(expected, actual);
        return new("images-match", wanted, $"{ratio:P2} pixels differing", ratio <= tolerance, $"expected={expectedPath} actual={actualPath}");
    }

    // Reads a PNG's width and height from its IHDR chunk without decoding it; throws ArgumentException if the bytes aren't a PNG.
    private static (int Width, int Height) PngSize(byte[] png, string path)
    {
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (png.Length < 24 || !png.AsSpan(0, 8).SequenceEqual(signature))
            throw new ArgumentException($"'{path}' is not a PNG file.");
        return (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
                System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
    }

    // Finds the element fresh (so retries see current state) and evaluates every check once; a locator with no element-level checks still asserts existence.
    internal static async Task<List<AssertCheck>> EvaluateAsync(App app, ScopedLocator? target, IReadOnlyList<ParsedCheck> checks, int stableTimeoutMs, CancellationToken ct)
    {
        // Step 1: decide whether any check needs the element itself; if so find it fresh and record that it exists.
        var results = new List<AssertCheck>();
        var needsElement = target is not null
            && (checks.Count == 0 || checks.Any(c => c.Kind == CheckKind.Text || c.Kind == CheckKind.Bool && !IsWindowLevel(c) && !IsAbsent(c)));
        ElementHandle? el = null;
        Locator? elLocator = null; // what to hand engine calls that take a locator and no scope
        if (needsElement)
        {
            el = await target!.FindAsync(app, ct);
            elLocator = target.Scope is null ? target.Target : Locator.ByRuntimeId(el.Id);
            results.Add(new("exists", true, true, true));
        }

        // Step 2: evaluate each requested check in order, appending one result per check.
        foreach (var c in checks)
        {
            switch (c.Name)
            {
                // Presence is implicit (FindAsync above found it or threw); only absence inside a scope is checked here, since the engine can't wait for absence within a scope.
                case "exists":
                    if (IsAbsent(c))
                    {
                        await app.FindAsync(target!.Scope!, ct); // the scope must exist, so a typo in it can't pass vacuously
                        var present = await target.CountAsync(app, ct);
                        results.Add(new("exists", false, present > 0, present == 0));
                    }
                    break;
                // Engine's own enabled rule (not reported disabled).
                case "enabled":
                    var enabled = WaitConditions.IsEnabled()(el!);
                    results.Add(new(c.Label, c.Want, enabled, enabled == c.Want));
                    break;
                // Engine's own visible rule (not offscreen, has area).
                case "visible":
                    var visible = WaitConditions.IsVisible()(el!);
                    results.Add(new(c.Label, c.Want, visible, visible == c.Want));
                    break;
                // No other window covers the element's center.
                case "unobscured":
                    var unobscured = await app.IsUnobscuredAsync(elLocator!, ct);
                    results.Add(new(c.Label, c.Want, unobscured, unobscured == c.Want));
                    break;
                // Enabled and visible and unobscured, with the three parts kept for the failure message.
                case "clickable":
                    var isEnabled = WaitConditions.IsEnabled()(el!);
                    var isVisible = WaitConditions.IsVisible()(el!);
                    var isUnobscured = await app.IsUnobscuredAsync(elLocator!, ct);
                    var clickable = isEnabled && isVisible && isUnobscured;
                    results.Add(new(c.Label, c.Want, clickable, clickable == c.Want, $"enabled={isEnabled} visible={isVisible} unobscured={isUnobscured}"));
                    break;
                // Text checks: read the property (text via the value pattern), then compare by equals, contains, or regex.
                case "name" or "text" or "automation-id" or "class-name" or "control-type":
                    var actual = c.Name switch
                    {
                        "name" => el!.Name,
                        "automation-id" => el!.AutomationId,
                        "class-name" => el!.ClassName,
                        "control-type" => el!.ControlType,
                        _ => await app.GetValueAsync(elLocator!, ct),
                    };
                    var passed = c.Op switch
                    {
                        CheckOp.Equals => actual == c.Value,
                        CheckOp.Contains => actual?.Contains(c.Value, StringComparison.Ordinal) == true,
                        _ => actual is not null && c.Pattern!.IsMatch(actual),
                    };
                    results.Add(new(c.Label, c.Value, actual, passed));
                    break;
                // Number of elements the locator matches, inside the scope when there is one; needs no element to exist.
                case "count":
                    var count = await target!.CountAsync(app, ct);
                    results.Add(new(c.Label, c.Number, count, count == c.Number));
                    break;
                // Window or element has stopped visibly changing, within the time left on the assert's timeout.
                default: // stable
                    var stable = true;
                    var region = target is null ? null : await target.ResolveAsync(app, ct);
                    try { await app.WaitForVisualStableAsync(region, timeout: TimeSpan.FromMilliseconds(stableTimeoutMs), ct: ct); }
                    catch (TimeoutException) { stable = false; }
                    results.Add(new(c.Label, c.Want, stable, stable == c.Want));
                    break;
            }
        }
        return results;
    }
}
