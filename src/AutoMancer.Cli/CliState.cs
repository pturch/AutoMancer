// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics;
using System.Text.Json;
using AutoMancer.Engine;
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Cli;

// The app commands act on by default: its process ID, when that process started, and its name.
public sealed record CliTarget(int Pid, DateTime StartUtc, string Name)
{
    // Builds a target for a running process, recording its start time so a later process that reuses the PID can be told apart.
    public static CliTarget ForProcess(int pid)
    {
        using var process = Process.GetProcessById(pid);
        return new CliTarget(pid, process.StartTime.ToUniversalTime(), process.ProcessName);
    }

    // True when the process exists, hasn't exited, and started when this target was recorded rather than being a later process with the same ID.
    public bool IsAlive()
    {
        try
        {
            using var process = Process.GetProcessById(Pid);
            // StartTime is only precise to about a second, so allow a little slack.
            return !process.HasExited && Math.Abs((process.StartTime.ToUniversalTime() - StartUtc).TotalSeconds) < 2;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

// Engine settings saved by 'configure'; a null value means "use the engine's default".
public sealed record CliSettings
{
    public int? ImplicitWaitMs { get; init; }
    public int? PollIntervalMs { get; init; }
    public int? ActionDelayMs { get; init; }
    public int? ForegroundActivationTimeoutMs { get; init; }
    public bool? ReresolveOnStale { get; init; }
    public string[]? ProviderChain { get; init; }

    // Builds the engine options: saved values over engine defaults, with the per-call implicit wait and launch arguments applied last; the engine's trace log goes to stderr only when verbose, otherwise just its warnings.
    public AppOptions ToAppOptions(int? implicitWaitMs = null, string? arguments = null, bool verbose = false)
    {
        var d = AppOptions.Default;
        return new AppOptions
        {
            Arguments = arguments,
            ImplicitWaitMs = implicitWaitMs ?? ImplicitWaitMs ?? d.ImplicitWaitMs,
            PollIntervalMs = PollIntervalMs ?? d.PollIntervalMs,
            ActionDelayMs = ActionDelayMs ?? d.ActionDelayMs,
            ForegroundActivationTimeoutMs = ForegroundActivationTimeoutMs ?? d.ForegroundActivationTimeoutMs,
            ReresolveOnStale = ReresolveOnStale ?? d.ReresolveOnStale,
            ProviderChain = ProviderChain ?? d.ProviderChain,
            Logger = new EngineLogger(Console.Error, verbose ? LogLevel.Info : LogLevel.Warn),
        };
    }
}

// Everything the CLI remembers between calls: the current target and the saved settings.
public sealed record CliStateData
{
    public CliTarget? Target { get; init; }
    public CliSettings Settings { get; init; } = new();
}

// Reads and writes the CLI's state file, %LOCALAPPDATA%\AutoMancer\state.json.
public static class CliState
{
    // Where the state file lives; tests point this at a throwaway path.
    internal static string FilePath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoMancer", "state.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Reads the state file; a missing or corrupt file gives the defaults (no target, no saved settings), but a file another CLI process has open is retried, so a busy file is never mistaken for an empty one.
    public static CliStateData Load()
    {
        try
        {
            var state = JsonSerializer.Deserialize<CliStateData>(WithRetry(() => File.ReadAllText(FilePath)));
            return state is null ? new CliStateData() : state with { Settings = state.Settings ?? new CliSettings() };
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new CliStateData();
        }
    }

    // Writes the state by saving a temp file and renaming it over the real one, so a reader never sees a half-written file; the rename is retried while another CLI process has the file open, and the temp file never outlives a failure.
    public static void Save(CliStateData state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
            WithRetry(() => { File.Move(temp, FilePath, overwrite: true); return 0; });
        }
        finally
        {
            File.Delete(temp);
        }
    }

    // Runs a file operation, retrying with a short growing pause while the file is in use by another process; a missing file is not retried.
    private static T WithRetry<T>(Func<T> operation)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return operation();
            }
            catch (Exception ex) when (attempt < 10 && ex is (IOException or UnauthorizedAccessException) and not (FileNotFoundException or DirectoryNotFoundException))
            {
                Thread.Sleep(15 * (attempt + 1));
            }
        }
    }
}
