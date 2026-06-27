// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.Json;

namespace AutoMancer.Cli;

// One persisted entry: a session ID paired with the PID of the target process.
public sealed record SessionEntry(string SessionId, int ProcessId, string ExecutablePath);

// JSON-backed registry of active CLI sessions written to %TEMP%\automancer-sessions.json.
public static class SessionStore
{
    private static readonly string FilePath =
        Path.Combine(Path.GetTempPath(), "automancer-sessions.json");

    private static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true };

    // Saves an entry; overwrites any existing entry with the same SessionId.
    public static void Save(SessionEntry entry)
    {
        var sessions = ReadAll();
        sessions[entry.SessionId] = entry;
        WriteAll(sessions);
    }

    // Returns the entry for the given session ID, or null if not found.
    public static SessionEntry? Get(string sessionId)
        => ReadAll().GetValueOrDefault(sessionId);

    // Removes the entry for the given session ID; no-op if not present.
    public static void Remove(string sessionId)
    {
        var sessions = ReadAll();
        if (sessions.Remove(sessionId))
            WriteAll(sessions);
    }

    // Returns all currently stored sessions.
    public static IReadOnlyList<SessionEntry> GetAll()
        => [.. ReadAll().Values];

    // Deserializes the store file; returns an empty dict if the file is absent or unreadable.
    private static Dictionary<string, SessionEntry> ReadAll()
    {
        if (!File.Exists(FilePath))
            return [];
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<Dictionary<string, SessionEntry>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    // Serializes the dict back to the store file, creating it if needed.
    private static void WriteAll(Dictionary<string, SessionEntry> sessions)
        => File.WriteAllText(FilePath, JsonSerializer.Serialize(sessions, JsonOptions));
}
