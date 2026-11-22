// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;

namespace AutoMancer.Cli.Commands;

// CLI command: capture the window, or one element, to a PNG file.
internal static class ScreenshotCommand
{
    // Builds the 'screenshot' sub-command with --out and an optional locator.
    internal static Command Build()
    {
        var outOpt     = new Option<string>("--out") { Description = "Path of the PNG file to write.", Required = true };
        var locatorOpts = new LocatorOptions(required: false, byHelp: "Capture just this element: strategy name (requires --value).");

        var command = new Command("screenshot", "Capture the window, or one element, to a PNG file.");
        command.Add(outOpt);
        locatorOpts.AddTo(command);
        command.SetAction((pr, token) => CliSession.RunAsync(pr, token, async (pid, ct) =>
        {
            // Step 1: read the options; the output path is made absolute so the report is unambiguous.
            var path    = Path.GetFullPath(pr.GetValue(outOpt)!);
            var target = locatorOpts.Parse(pr);
            if (Directory.Exists(path))
                throw new ArgumentException($"--out must be a file path, but {path} is a folder.");

            // Step 2: attach and capture the whole window, or just the element when a locator was given.
            await using var app = await CliSession.AttachAsync(pid, ct);
            var locator = target is null ? null : await target.ResolveAsync(app, ct);
            if (locator is null)
                await app.TakeScreenshotAsync(path, ct);
            else
                await app.TakeScreenshotAsync(locator, path, ct);

            return new CliResult($"Saved {path}", new { path });
        }));
        return command;
    }
}
