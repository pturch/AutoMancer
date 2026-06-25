// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoMancer.Engine.Diagnostics;

// Severity of an EngineLogger entry, also used as the minimum-level filter.
public enum LogLevel { Debug = 0, Info = 1, Warn = 2, Error = 3 }

// Writes structured JSON-line log entries to an injectable TextWriter, filtered by minimum severity.
public sealed class EngineLogger
{
    private readonly TextWriter _writer;
    private readonly LogLevel _minimumLevel;

    // Creates a logger that writes JSON lines to writer, suppressing entries below minimumLevel.
    public EngineLogger(TextWriter writer, LogLevel minimumLevel = LogLevel.Info)
    {
        _writer = writer;
        _minimumLevel = minimumLevel;
    }

    // Writes a structured log entry at the given severity; structured data is serialized as "data" field.
    public void Debug(string message, object? data = null) => Write(LogLevel.Debug, message, data);
    public void Info(string message, object? data = null) => Write(LogLevel.Info, message, data);
    public void Warn(string message, object? data = null) => Write(LogLevel.Warn, message, data);
    public void Error(string message, object? data = null) => Write(LogLevel.Error, message, data);

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
