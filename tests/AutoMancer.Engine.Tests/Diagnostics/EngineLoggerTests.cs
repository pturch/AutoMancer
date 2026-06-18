// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.Json;
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Tests.Diagnostics;

public sealed class EngineLoggerTests
{
    [Fact]
    public void Info_WritesJsonLineWithExpectedFields()
    {
        var sw = new StringWriter();
        var logger = new EngineLogger(sw);

        logger.Info("test message", new { key = "value" });

        var line = sw.ToString().Trim();
        var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        Assert.Equal("Info", root.GetProperty("level").GetString());
        Assert.Equal("test message", root.GetProperty("message").GetString());
        Assert.Equal("value", root.GetProperty("data").GetProperty("key").GetString());
        Assert.True(root.TryGetProperty("ts", out _));
    }

    [Fact]
    public void BelowMinimumLevel_WritesNothing()
    {
        var sw = new StringWriter();
        var logger = new EngineLogger(sw, LogLevel.Warn);

        logger.Debug("suppressed");
        logger.Info("also suppressed");

        Assert.Empty(sw.ToString());
    }

    [Fact]
    public void AtMinimumLevel_IsIncluded()
    {
        var sw = new StringWriter();
        var logger = new EngineLogger(sw, LogLevel.Warn);

        logger.Warn("exactly at threshold");

        Assert.NotEmpty(sw.ToString());
    }

    [Fact]
    public void NullData_WritesLineWithoutDataField()
    {
        var sw = new StringWriter();
        var logger = new EngineLogger(sw);

        logger.Info("no data");

        var doc = JsonDocument.Parse(sw.ToString().Trim());
        Assert.False(doc.RootElement.TryGetProperty("data", out _));
    }
}
