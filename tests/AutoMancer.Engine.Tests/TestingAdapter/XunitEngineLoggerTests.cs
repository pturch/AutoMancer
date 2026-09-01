// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.Json;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Testing.XUnit;
using Moq;
using Xunit.Abstractions;

namespace AutoMancer.Engine.Tests.TestingAdapter;

// EngineLoggerTests already covers the JSON shape and level-filtering logic this class delegates to;
// these tests only cover what's unique here — the ITestOutputHelper forwarding and minimumLevel wiring.
public sealed class XunitEngineLoggerTests
{
    [Fact]
    public void Info_ForwardsJsonLineToTestOutputHelper()
    {
        var output = new Mock<ITestOutputHelper>();
        var logger = new XunitEngineLogger(output.Object);

        logger.Info("test message", new { key = "value" });

        output.Verify(o => o.WriteLine(It.Is<string>(line =>
            JsonDocument.Parse(line).RootElement.GetProperty("message").GetString() == "test message")), Times.Once);
    }

    [Fact]
    public void BelowMinimumLevel_NeverCallsTestOutputHelper()
    {
        var output = new Mock<ITestOutputHelper>();
        var logger = new XunitEngineLogger(output.Object, LogLevel.Warn);

        logger.Debug("suppressed");
        logger.Info("also suppressed");

        output.Verify(o => o.WriteLine(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void PullIssuesSince_DelegatesToInnerLogger()
    {
        var output = new Mock<ITestOutputHelper>();
        var logger = new XunitEngineLogger(output.Object);
        var since = DateTime.UtcNow;

        logger.Warn("an issue");

        var issues = logger.PullIssuesSince(since);

        Assert.Single(issues);
        Assert.Equal("an issue", issues[0].Message);
    }
}
