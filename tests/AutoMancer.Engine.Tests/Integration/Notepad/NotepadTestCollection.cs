// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Tests.Integration;

// Groups all Notepad integration test classes into one sequential collection.
// Required because Windows 11 WinUI3 Notepad is single-instance — a second launch
// opens a tab in the existing window instead of a new process, so parallel test
// classes would fight over the same HWND.
[CollectionDefinition("Notepad", DisableParallelization = true)]
public sealed class NotepadTestCollection { }
