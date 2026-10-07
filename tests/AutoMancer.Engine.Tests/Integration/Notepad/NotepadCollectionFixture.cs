// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Tests.Integration;

// Clears Windows 11 Notepad's tab-restore state once before the "Notepad" collection runs, so a stale or force-killed tab from an earlier session is never silently restored into a freshly launched instance.
public sealed class NotepadCollectionFixture
{
    // Runs the one-time tab-state cleanup before the collection's first test.
    public NotepadCollectionFixture() => ClearTabState();

    // Best-effort delete of every file under Notepad's TabState folder, retrying for up to 2s since a just-killed Notepad still holds them locked briefly after it reports exited.
    public static void ClearTabState()
    {
        var tabStateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "Microsoft.WindowsNotepad_8wekyb3d8bbwe", "LocalState", "TabState");

        if (!Directory.Exists(tabStateDir)) return;
        var deadline = Environment.TickCount64 + 2_000;
        while (true)
        {
            foreach (var file in Directory.EnumerateFiles(tabStateDir))
                try { File.Delete(file); } catch { /* still locked — retried below */ }
            if (!Directory.EnumerateFiles(tabStateDir).Any() || Environment.TickCount64 > deadline)
                return;
            Thread.Sleep(100);
        }
    }
}
