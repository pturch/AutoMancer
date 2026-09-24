// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Tests.Integration;

// Clears Windows 11 Notepad's tab-restore state once before the "Notepad" collection runs, so a stale or force-killed tab from an earlier session is never silently restored into a freshly launched instance.
public sealed class NotepadCollectionFixture
{
    // Best-effort delete of every file under Notepad's TabState folder; a locked file just means it survives for this run.
    public NotepadCollectionFixture()
    {
        var tabStateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "Microsoft.WindowsNotepad_8wekyb3d8bbwe", "LocalState", "TabState");

        if (!Directory.Exists(tabStateDir)) return;
        foreach (var file in Directory.EnumerateFiles(tabStateDir))
            try { File.Delete(file); } catch { /* best-effort */ }
    }
}
