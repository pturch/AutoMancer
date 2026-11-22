// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Cli;

// What a command produced: human text for default output and an optional data object for --json output.
public sealed record CliResult(string Text, object? Data = null);

// Thrown when there is no app to act on: no current target, the target has exited, or the PID can't be attached to.
public sealed class TargetNotFoundException(string message, Exception? inner = null) : Exception(message, inner);

// Base for failures where the app ran fine but an expectation wasn't met; carries a stable JSON code and optional structured details.
public abstract class CliCheckException(string code, string message, object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public object? Details { get; } = details;
}

// Process exit codes returned by every CLI command.
public static class CliExitCode
{
    public const int Ok = 0;
    public const int CheckFailed = 1; // the app ran fine but the expectation wasn't met: not found, timeout, still present, visual mismatch
    public const int Usage = 2; // syntax error, basically
    public const int TargetNotFound = 3; // no app available to act on
    public const int Error = 4; // the tool or environment broke
}

// Writes a Rect as plain x/y/width/height, since the default would also emit its tuple-typed Center as an empty object.
internal sealed class RectJsonConverter : System.Text.Json.Serialization.JsonConverter<Rect>
{
    // Reading is never needed; the CLI only emits JSON.
    public override Rect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

    // Emits the rect's four numbers.
    public override void Write(Utf8JsonWriter writer, Rect value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteNumber("width", value.Width);
        writer.WriteNumber("height", value.Height);
        writer.WriteEndObject();
    }
}

// Shared command plumbing: target resolution, attach, --json output, and exception-to-exit-code mapping.
public static class CliSession
{
    // Global --json flag; the root command registers it so every command can emit machine-readable output.
    public static readonly Option<bool> JsonOption =
        new("--json") { Description = "Emit a single JSON object on stdout instead of text.", Recursive = true };

    // Global --verbose flag; shows the engine's trace log on stderr, which is otherwise limited to warnings.
    public static readonly Option<bool> VerboseOption =
        new("--verbose") { Description = "Show the engine's trace log on stderr.", Recursive = true };

    // Whether --verbose was given for the command now running; set once per invocation before the body runs.
    internal static bool Verbose { get; private set; }

    // Global --pid flag; names the app to act on, overriding the current target.
    public static readonly Option<int?> PidOption =
        new("--pid") { Description = "Process ID of the app to act on (default: the current target set by 'launch' or 'configure --attach').", Recursive = true };

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new RectJsonConverter() } };

    // Parses and runs the command line, except parse errors go through ReportParseErrors instead of System.CommandLine's default, which prints the whole help text to stdout and ignores --json.
    public static async Task<int> InvokeAsync(RootCommand root, string[] args)
    {
        var parseResult = root.Parse(args);
        if (parseResult.Errors.Count > 0 && parseResult.Action is ParseErrorAction)
            return ReportParseErrors(parseResult);

        // System.CommandLine cancels on Ctrl-C and SIGTERM but not Ctrl-Break, which would kill the process uncleanly.
        using var cts = new CancellationTokenSource();
        // Turns Ctrl-Break into cooperative cancellation.
        void OnCancelKey(object? sender, ConsoleCancelEventArgs e)
        {
            if (e.SpecialKey != ConsoleSpecialKey.ControlBreak) return;
            e.Cancel = true;
            cts.Cancel();
        }
        Console.CancelKeyPress += OnCancelKey;
        try { return await parseResult.InvokeAsync(cancellationToken: cts.Token); }
        finally { Console.CancelKeyPress -= OnCancelKey; }
    }

    // Reports command-line syntax errors the way every other failure is reported: a JSON usage error on stdout with --json, otherwise the message plus a hint on stderr; stdout stays clean and the exit code is Usage (2).
    private static int ReportParseErrors(ParseResult pr)
    {
        var message = string.Join(" ", pr.Errors.Select(e => FriendlyTypeNames(e.Message)));
        var command = pr.CommandResult.Command;
        var hint = command is RootCommand ? "Run 'automancer --help' for the commands." : $"Run 'automancer {command.Name} --help' for usage.";
        var json = pr.Tokens.Any(t => t.Value == "--json");
        if (json)
            Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = new { code = "usage", message = $"{message} {hint}", details = (object?)null } }, JsonOptions));
        else
            Console.Error.WriteLine($"{message}{Environment.NewLine}{hint}");
        return CliExitCode.Usage;
    }

    // Rewrites the .NET type names System.CommandLine puts in its messages into plain words.
    private static string FriendlyTypeNames(string message) => System.Text.RegularExpressions.Regex.Replace(
        message, @"as expected type '(?:System\.Nullable`1\[)?System\.(Int32|Double)\]?'",
        m => "(expected " + m.Groups[1].Value switch { "Int32" => "a whole number", _ => "a number" } + ")");

    // Runs a command body that needs no session, printing its result or mapping its exception to an exit code.
    public static async Task<int> RunAsync(ParseResult pr, CancellationToken token, Func<CancellationToken, Task<CliResult>> body)
    {
        var json = pr.GetValue(JsonOption);
        Verbose = pr.GetValue(VerboseOption);
        try
        {
            var result = await body(token);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { ok = true, data = result.Data }, JsonOptions));
            else
                Console.WriteLine(result.Text);
            return CliExitCode.Ok;
        }
        catch (Exception ex)
        {
            var (code, exitCode) = Map(ex);
            var message = ex is WindowMinimizedError ? $"{ex.Message} Run 'automancer window --state normal' to restore it." : ex.Message;
            var name = pr.CommandResult.Command.Name;
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(
                    new { ok = false, error = new { code, message, details = (ex as CliCheckException)?.Details } }, JsonOptions));
            else
                Console.Error.WriteLine(code == "error"
                    ? $"{char.ToUpperInvariant(name[0])}{name[1..]} failed: {message}"
                    : message);
            return exitCode;
        }
    }

    // Resolves the target PID (--pid, else the current target) and hands it to the body; the body attaches via AttachAsync after any argument validation.
    public static Task<int> RunAsync(ParseResult pr, CancellationToken token, Func<int, CancellationToken, Task<CliResult>> body)
        => RunAsync(pr, token, ct => body(ResolveTarget(pr), ct));

    // Picks the PID to act on: --pid if given, else the saved current target, which must still be the same running process.
    private static int ResolveTarget(ParseResult pr)
    {
        if (pr.GetValue(PidOption) is { } pid)
            return pid;

        var state = CliState.Load();
        if (state.Target is not { } target)
            throw new TargetNotFoundException("No target. Pass --pid, run 'automancer launch', or run 'automancer configure --attach'.");
        if (!target.IsAlive())
        {
            CliState.Save(state with { Target = null });
            throw new TargetNotFoundException($"The current target (PID {target.Pid}, {target.Name}) is no longer running. Run 'automancer launch' or 'automancer configure --attach'.");
        }
        return target.Pid;
    }

    // Attaches to the target as a non-owning App using the saved settings, because an owning App's disposal would kill the user's app; the owner is deliberately left undisposed since the CLI exits right after.
    public static async Task<App> AttachAsync(int pid, CancellationToken ct, int? implicitWaitMs = null)
    {
        try
        {
            var options = CliState.Load().Settings.ToAppOptions(implicitWaitMs, verbose: Verbose);
            var owner = await App.AttachByPidAsync(pid, options, ct: ct);
            return owner.WithOptions(options);
        }
        catch (AppLaunchError ex)
        {
            throw new TargetNotFoundException($"{ex.Message} Pass the PID of the process that owns the window; 'automancer launch' prints it.", ex);
        }
    }

    // Translates an exception into a stable string code plus its exit code.
    private static (string Code, int ExitCode) Map(Exception ex) => ex switch
    {
        ArgumentException               => ("usage", CliExitCode.Usage),
        TargetNotFoundException         => ("target_not_found", CliExitCode.TargetNotFound),
        ElementNotFoundError            => ("element_not_found", CliExitCode.CheckFailed),
        ElementStillPresentError        => ("still_present", CliExitCode.CheckFailed),
        ElementConditionTimeoutError    => ("condition_timeout", CliExitCode.CheckFailed),
        ElementNotInteractableError     => ("not_interactable", CliExitCode.CheckFailed),
        WindowMinimizedError            => ("window_minimized", CliExitCode.CheckFailed),
        CliCheckException check         => (check.Code, CliExitCode.CheckFailed),
        TimeoutException                => ("timeout", CliExitCode.CheckFailed),
        BaselineDpiMismatchError        => ("baseline_dpi_mismatch", CliExitCode.Error),
        _                               => ("error", CliExitCode.Error),
    };
}
