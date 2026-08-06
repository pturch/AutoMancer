// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using Xunit;

// Integration test collections drive real mouse/keyboard input and steal the foreground window, so they can't run in parallel with each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
