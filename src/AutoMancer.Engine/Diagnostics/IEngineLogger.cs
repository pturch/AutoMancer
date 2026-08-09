// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Diagnostics;

// Structured logging sink for engine trace output — implement this to bridge into a host app's own logging stack (ILogger, Serilog, xUnit output, …) instead of the default JSON-lines writer.
public interface IEngineLogger
{
    void Debug(string message, object? data = null);
    void Info(string message, object? data = null);
    void Warn(string message, object? data = null);
    void Error(string message, object? data = null);
}
