// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using Xunit;

// CliTestHelper.RunAsync redirects Console.Out/Error globally, so parallel test classes would stomp on each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
