// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.Diagnostics;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Cli.Commands;

// Thrown by 'assert' when any check fails; the details list every check with expected vs actual.
internal sealed class AssertionFailedException(IReadOnlyList<AssertCheck> checks)
    : CliCheckException("assertion_failed",
        "Assertion failed: " + string.Join("; ", checks.Where(c => !c.Passed).Select(c =>
            $"{c.Check}: expected {Show(c.Expected)}, actual {Show(c.Actual)}{(c.Detail is null ? "" : $" ({c.Detail})")}")),
        new { checks })
{
    // Renders a value for the message, quoting strings and spelling out null as "unknown".
    private static string Show(object? v) => v switch { null => "unknown", string s => $"\"{s}\"", _ => v.ToString()! };
}

// CLI command: assert properties or counts of an element, window stability, or that two PNG files match, with exit 1 on failure.
internal static class AssertCommand
{
    // Builds the 'assert' sub-command with an optional locator, repeatable --check, --tolerance, and --timeout.
    internal static Command Build()
    {
        // Declare the arguments and options; --check is repeatable, the rest are single-valued.
        var locatorOpts  = new LocatorOptions(required: false, byHelp: "Locator strategy: name, id, class, control, path, runtime, xpath. Omit --by/--value to run the window-level check (stable) or an image comparison.");
        var checkOpt     = new Option<string[]>("--check") { Description = "A check to assert; repeat for several (all must hold). Forms: name, !name, name=value, name~value (contains), name=~regex. " +
            "Booleans: exists enabled visible clickable unobscured stable. Text: name text automation-id class-name control-type. " +
            "Also: count=N. With no --check, a locator asserts existence." };
        var expectedOpt  = new Option<string?>("--expected") { Description = "Image comparison: the reference PNG. Use with --actual." };
        var actualOpt    = new Option<string?>("--actual") { Description = "Image comparison: the PNG to check against --expected." };
        var toleranceOpt = new Option<double>("--tolerance") { DefaultValueFactory = _ => 0.01, Description = "For --expected/--actual: largest fraction of differing pixels (0-1) that still passes." };
        var timeoutOpt   = new Option<int?>("--timeout") { Description = "Total time to wait, in milliseconds: for the element to appear and for the checks to pass, re-checking every 100 ms (0 = a single pass, no waiting). Default: the saved implicit wait, else 5000. This makes assert double as a wait." };

        // Assemble the command and attach the handler.
        var command = new Command("assert", "Assert properties or counts of an element, the window's stability, or that two PNG files match; exits 1 and reports expected vs actual when any check fails.");
        locatorOpts.AddTo(command);
        foreach (var o in new Option[] { checkOpt, expectedOpt, actualOpt, toleranceOpt, timeoutOpt })
            command.Add(o);
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, async (pid, ct) =>
        {
            // Step 1: read the options and parse every --check expression up front, so syntax errors surface before touching the app.
            var r = pr;
            var target    = locatorOpts.Parse(r);
            var checks    = (r.GetValue(checkOpt) ?? []).Select(AssertChecks.Parse).ToList();
            var expected  = r.GetValue(expectedOpt);
            var actual    = r.GetValue(actualOpt);
            var tolerance = r.GetValue(toleranceOpt);
            var timeout   = r.GetValue(timeoutOpt) ?? CliState.Load().Settings.ImplicitWaitMs ?? 5000;

            // Step 2: reject invalid option combinations as usage errors, still before attaching.
            if (timeout < 0)
                throw new ArgumentException("--timeout must be 0 or greater.");
            if (!(tolerance is >= 0 and <= 1))
                throw new ArgumentException("--tolerance must be a number between 0 and 1.");
            if ((expected is null) != (actual is null))
                throw new ArgumentException("--expected and --actual must be given together.");
            var hasImages = expected is not null;
            if (hasImages && (Directory.Exists(expected) || Directory.Exists(actual)))
                throw new ArgumentException("--expected and --actual must be image files, not folders.");
            if (checks.Any(AssertChecks.IsAbsent) && (checks.Count > 1 || hasImages))
                throw new ArgumentException("!exists cannot be combined with other checks.");
            if (target is null && (checks.Count == 0 && !hasImages || !checks.All(AssertChecks.IsWindowLevel)))
                throw new ArgumentException("Without --by and --value only the window-level check (stable) or --expected/--actual is allowed.");

            // Step 3: run the image comparison first (it needs no app, and a missing or bad file should fail fast); an images-only assert never attaches.
            var imageCheck = hasImages ? AssertChecks.CompareImages(Path.GetFullPath(expected!), Path.GetFullPath(actual!), tolerance) : null;
            if (imageCheck is not null && target is null && checks.Count == 0)
                return Report([imageCheck]);

            // Step 4: attach with the engine's own wait set to --timeout, so finding the element and !exists honor it too; a lone unscoped !exists is its own path because it waits for absence instead of polling properties.
            await using var app = await CliSession.AttachAsync(pid, ct, implicitWaitMs: timeout);
            if (checks.Count == 1 && AssertChecks.IsAbsent(checks[0]) && target!.Scope is null)
            {
                try { await app.WaitUntilGoneAsync(target.Target, ct); }
                catch (ElementStillPresentError) { throw new AssertionFailedException([new AssertCheck("exists", false, true, false)]); }
                return new CliResult("Assertion passed: absent.", new { passed = true, checks = new[] { new AssertCheck("exists", false, false, true) } });
            }

            // Step 5: evaluate all checks, re-running every 100 ms until they all pass or the timeout elapses; the image result is fixed, so a failing one stops the retries.
            var clock = Stopwatch.StartNew();
            List<AssertCheck> results;
            while (true)
            {
                results = await AssertChecks.EvaluateAsync(app, target, checks, Math.Max(timeout - (int)clock.ElapsedMilliseconds, 250), ct);
                if (imageCheck is not null)
                    results.Add(imageCheck);
                if (results.All(c => c.Passed) || imageCheck is { Passed: false } || clock.ElapsedMilliseconds >= timeout)
                    break;
                await Task.Delay(100, ct);
            }

            return Report(results);
        }));
        return command;
    }

    // Step 6: any failing check throws (exit 1 with the full report); otherwise returns the passing report.
    private static CliResult Report(List<AssertCheck> results)
    {
        if (results.Any(c => !c.Passed))
            throw new AssertionFailedException(results);
        return new CliResult("Assertion passed: " + string.Join(", ", results.Select(c => c.Check)) + ".", new { passed = true, checks = results });
    }
}
