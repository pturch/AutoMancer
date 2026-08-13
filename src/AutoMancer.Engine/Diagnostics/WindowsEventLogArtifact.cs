// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Diagnostics;

// Writes the end-of-run Windows Event Log artifact and, if requested, a combined timeline merging it with the run's own AutoMancer issues — called once from App.DisposeAsync.
internal static class WindowsEventLogArtifact
{
    // Queries the Windows Event Log since the run started and writes matches to eventLogger; if combinedLogger is set, also pulls primaryLogger's buffered issues and interleaves both by timestamp into combinedLogger. No-ops if neither eventLogger nor combinedLogger is configured.
    internal static void Write(DateTime since, IEngineLogger? primaryLogger, IEngineLogger? eventLogger, IEngineLogger? combinedLogger)
    {
        if (eventLogger is null && combinedLogger is null) return;

        var events = WindowsEventLogReader.QuerySince(since).ToList();
        foreach (var e in events)
            eventLogger?.Log(e.Level, $"[{e.ProviderName}] Event {e.EventId}", new { e.ProviderName, e.EventId, e.Message });

        if (combinedLogger is null) return;

        var issues = primaryLogger?.PullIssuesSince(since) ?? [];
        var timeline = issues
            .Select(i => (i.Timestamp, i.Level, Source: "AutoMancer", Message: i.Message, Data: i.Data))
            .Concat(events.Select(e => (e.Timestamp, e.Level, Source: "WindowsEventLog", Message: e.Message, Data: (object?)new { e.ProviderName, e.EventId })))
            .OrderBy(entry => entry.Timestamp);

        foreach (var entry in timeline)
            combinedLogger.Log(entry.Level, $"[{entry.Source}] {entry.Message}", entry.Data);
    }
}
