// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace ConsumerNotepadTests;

// Groups every test class in this sample into one sequential collection sharing a single NotepadFixture instance (via
// ICollectionFixture, not each class declaring its own IClassFixture — that would launch a competing Notepad per class).
// Required because Windows 11 WinUI3 Notepad is single-instance — a second concurrent launch opens a tab in the
// existing window instead of a new process, so parallel classes would fight over the same HWND.
[CollectionDefinition("ConsumerNotepad", DisableParallelization = true)]
public sealed class ConsumerNotepadCollection : ICollectionFixture<NotepadFixture> { }

// WindowCloseDemoTests' own collection: closing "its" Notepad closes the single-instance window the shared NotepadFixture drives, so it can't share that collection without breaking every class that runs after it. Non-parallel collections run one at a time, so it still never overlaps the shared one.
[CollectionDefinition("ConsumerNotepadWindowClose", DisableParallelization = true)]
public sealed class ConsumerNotepadWindowCloseCollection { }

// PaintEditColorsDemoTests' own collection: it drives its own Paint, and non-parallel collections run one at a time, so it never competes with the Notepad tests for the foreground.
[CollectionDefinition("ConsumerPaint", DisableParallelization = true)]
public sealed class ConsumerPaintCollection { }
