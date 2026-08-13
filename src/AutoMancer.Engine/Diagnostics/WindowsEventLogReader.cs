// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Diagnostics.Eventing.Reader;

namespace AutoMancer.Engine.Diagnostics;

// Queries the Windows Application and System event logs for Critical/Error/Warning entries created since a given time — the objective, OS-recorded signal that replaces guessing at an individual P/Invoke's GetLastWin32Error().
internal static class WindowsEventLogReader
{
    private static readonly string[] LogNames = ["Application", "System"];

    // Runs the query against both logs and yields every matching entry as a UTC-timestamped record, oldest first within each log.
    internal static IEnumerable<(DateTime Timestamp, LogLevel Level, string ProviderName, int EventId, string Message)> QuerySince(DateTime sinceUtc)
    {
        foreach (var logName in LogNames)
        {
            // Level 1=Critical, 2=Error, 3=Warning per the Windows Event Log schema. 
            // @SystemTime expects millisecond (3-digit) precision — the round-trip "o" format's 7 fractional digits can make the query engine reject the XPath or silently match nothing on some OS versions.
            var xpath = $"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[@SystemTime >= '{sinceUtc:yyyy-MM-ddTHH:mm:ss.fffZ}']]]";
            EventLogReader reader;
            try { reader = new EventLogReader(new EventLogQuery(logName, PathType.LogName, xpath)); }
            catch (EventLogNotFoundException) { continue; }

            using (reader)
            {
                EventRecord? record;
                while ((record = reader.ReadEvent()) is not null)
                    using (record)
                        yield return (
                            record.TimeCreated?.ToUniversalTime() ?? sinceUtc,
                            record.Level is 1 or 2 ? LogLevel.Error : LogLevel.Warn,
                            record.ProviderName ?? "Unknown",
                            record.Id,
                            Describe(record));
            }
        }
    }

    // FormatDescription can throw when a provider's message-table resource isn't installed locally; fall back to the raw event rather than dropping it.
    private static string Describe(EventRecord record)
    {
        try { return record.FormatDescription() is { Length: > 0 } description ? description : "(no description)"; }
        catch (EventLogException) { return "(description unavailable — provider's message resource not found locally)"; }
    }
}
