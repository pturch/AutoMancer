// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Diagnostics;

// Severity of an IEngineLogger entry, also used as the minimum-level filter.
public enum LogLevel { Debug = 0, Info = 1, Warn = 2, Error = 3 }

// Structured logging sink for engine trace output — implement this to bridge into a host app's own logging stack (ILogger, Serilog, xUnit output, …) instead of the default JSON-lines writer.
public interface IEngineLogger
{
    void Debug(string message, object? data = null);
    void Info(string message, object? data = null);
    void Warn(string message, object? data = null);
    void Error(string message, object? data = null);

    // Dispatches to Debug/Info/Warn/Error for a level only known at runtime, e.g. when translating an external log source; default routes through this instance's own methods.
    void Log(LogLevel level, string message, object? data = null)
    {
        switch (level)
        {
            case LogLevel.Debug: Debug(message, data); break;
            case LogLevel.Info: Info(message, data); break;
            case LogLevel.Warn: Warn(message, data); break;
            default: Error(message, data); break;
        }
    }

    // Returns and removes this instance's own Warn/Error entries timestamped at or after since — the "issues AutoMancer ran into" — for another artifact (e.g. a combined timeline with Windows Event Log entries) to merge in; empty by default.
    IReadOnlyList<(DateTime Timestamp, LogLevel Level, string Message, object? Data)> PullIssuesSince(DateTime since) => [];
}
