// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text;
using AutoMancer.Engine.Diagnostics;
using Xunit.Abstractions;

namespace AutoMancer.Testing.XUnit;

// IEngineLogger that writes structured JSON-line entries straight to xUnit's ITestOutputHelper, filtered by minimum severity; delegates to EngineLogger via a TextWriter adapter so the JSON shape and level filtering stay defined in exactly one place.
public sealed class XunitEngineLogger : IEngineLogger
{
    private readonly EngineLogger _inner;

    // Wraps output in a TextWriter adapter and hands it to a plain EngineLogger.
    public XunitEngineLogger(ITestOutputHelper output, LogLevel minimumLevel = LogLevel.Info)
        => _inner = new EngineLogger(new TestOutputWriter(output), minimumLevel);

    public void Debug(string message, object? data = null) => _inner.Debug(message, data);
    public void Info(string message, object? data = null) => _inner.Info(message, data);
    public void Warn(string message, object? data = null) => _inner.Warn(message, data);
    public void Error(string message, object? data = null) => _inner.Error(message, data);

    // Forwards each completed line to ITestOutputHelper.WriteLine; EngineLogger only ever calls WriteLine(string), never the character-at-a-time Write members.
    private sealed class TestOutputWriter(ITestOutputHelper output) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string? value) => output.WriteLine(value ?? "");
    }
}
