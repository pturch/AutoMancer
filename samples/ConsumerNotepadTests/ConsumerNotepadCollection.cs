// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace ConsumerNotepadTests;

// Groups every test class in this sample into one sequential collection so they share NotepadFixture's single launch.
// Required because Windows 11 WinUI3 Notepad is single-instance — a second concurrent launch opens a tab in the
// existing window instead of a new process, so parallel classes would fight over the same HWND.
[CollectionDefinition("ConsumerNotepad", DisableParallelization = true)]
public sealed class ConsumerNotepadCollection { }
