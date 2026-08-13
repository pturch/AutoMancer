// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoMancer.Engine.Diagnostics;

// Severity of an EngineLogger entry, also used as the minimum-level filter.
public enum LogLevel { Debug = 0, Info = 1, Warn = 2, Error = 3 }

// Default IEngineLogger: writes structured JSON-line log entries to an injectable TextWriter, filtered by minimum severity.
// The sole implementation of PullIssuesSince, so other loggers (XunitEngineLogger, a consumer's own) delegate to an inner instance rather than reimplementing it.
public sealed class EngineLogger : IEngineLogger
{
    private readonly TextWriter _writer;
    private readonly LogLevel _minimumLevel;
    // Scoped to this instance and pulled by timestamp via PullIssuesSince — sharing one EngineLogger across concurrent/unrelated App runs will cause overlap.
    private readonly List<(DateTime Timestamp, LogLevel Level, string Message, object? Data)> _issues = [];

    // Creates a logger that writes JSON lines to writer, suppressing entries below minimumLevel.
    public EngineLogger(TextWriter writer, LogLevel minimumLevel = LogLevel.Info)
    {
        _writer = writer;
        _minimumLevel = minimumLevel;
    }

    // Writes a structured log entry at the given severity; structured data is serialized as "data" field.
    public void Debug(string message, object? data = null) => Write(LogLevel.Debug, message, data);
    public void Info(string message, object? data = null) => Write(LogLevel.Info, message, data);

    // Also buffers a timestamped copy (unless suppressed by minimumLevel) for a later PullIssuesSince.
    public void Warn(string message, object? data = null)
    {
        if (LogLevel.Warn >= _minimumLevel)
            lock (_issues) _issues.Add((DateTime.UtcNow, LogLevel.Warn, message, data));
        Write(LogLevel.Warn, message, data);
    }

    // Also buffers a timestamped copy (unless suppressed by minimumLevel) for a later PullIssuesSince.
    public void Error(string message, object? data = null)
    {
        if (LogLevel.Error >= _minimumLevel)
            lock (_issues) _issues.Add((DateTime.UtcNow, LogLevel.Error, message, data));
        Write(LogLevel.Error, message, data);
    }

    // Returns and removes every buffered Warn/Error entry timestamped at or after since.
    public IReadOnlyList<(DateTime Timestamp, LogLevel Level, string Message, object? Data)> PullIssuesSince(DateTime since)
    {
        lock (_issues)
        {
            var matched = _issues.Where(i => i.Timestamp >= since).ToList();
            _issues.RemoveAll(i => i.Timestamp >= since);
            return matched;
        }
    }

    // Serializes and writes one JSON line; no-ops when level is below the configured minimum.
    private void Write(LogLevel level, string message, object? data)
    {
        if (level < _minimumLevel) return;

        var node = new JsonObject
        {
            ["ts"] = DateTime.UtcNow.ToString("O"),
            ["level"] = level.ToString(),
            ["message"] = message
        };

        if (data is not null)
            node["data"] = JsonSerializer.SerializeToNode(data, data.GetType());

        _writer.WriteLine(node.ToJsonString());
    }
}
