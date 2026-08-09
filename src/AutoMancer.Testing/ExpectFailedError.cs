// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Testing;

// Thrown by an Expect(...) assertion when the checked condition is not met.
public sealed class ExpectFailedError : Exception
{
    // Wraps message as the assertion failure text surfaced to the test runner.
    public ExpectFailedError(string message) : base(message) { }
}
