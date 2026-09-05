// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Tests.Integration;

// Groups all Task Manager integration tests into one sequential collection — its launcher-stub/real-host activation handoff races badly against parallel classes touching the same instance.
[CollectionDefinition("TaskManager", DisableParallelization = true)]
public sealed class TaskManagerTestCollection { }
