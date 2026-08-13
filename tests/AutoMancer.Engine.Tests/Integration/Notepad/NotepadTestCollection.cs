// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Tests.Integration;

// Groups all Notepad integration tests into one sequential collection — WinUI3 Notepad is single-instance, so parallel classes would fight over the same HWND.
[CollectionDefinition("Notepad", DisableParallelization = true)]
public sealed class NotepadTestCollection { }
