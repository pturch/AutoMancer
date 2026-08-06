// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli.Commands;
using AutoMancer.Engine.Core;

namespace AutoMancer.Cli.Tests.Commands;

public sealed class FindCommandTests
{
    // Every recognized --by strategy maps to the matching Locator with the given value carried through unchanged.
    [Theory]
    [InlineData("name", LocatorStrategy.Name)]
    [InlineData("id", LocatorStrategy.AutomationId)]
    [InlineData("class", LocatorStrategy.ClassName)]
    [InlineData("control", LocatorStrategy.ControlType)]
    [InlineData("path", LocatorStrategy.AutoMancerPath)]
    [InlineData("runtime", LocatorStrategy.RuntimeId)]
    [InlineData("xpath", LocatorStrategy.AutomancerXPath)]
    public void ParseLocator_KnownStrategy_ReturnsMatchingLocator(string by, LocatorStrategy expected)
    {
        var locator = FindCommand.ParseLocator(by, "some-value");

        Assert.Equal(expected, locator.Strategy);
        Assert.Equal("some-value", locator.Value);
    }

    // Strategy matching is case-insensitive.
    [Fact]
    public void ParseLocator_MixedCaseStrategy_StillMatches()
    {
        var locator = FindCommand.ParseLocator("NAME", "Text Editor");

        Assert.Equal(LocatorStrategy.Name, locator.Strategy);
    }

    // An unrecognized strategy throws ArgumentException naming the valid set, instead of silently defaulting.
    [Fact]
    public void ParseLocator_UnknownStrategy_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => FindCommand.ParseLocator("css", "Text Editor"));

        Assert.Contains("css", ex.Message);
        Assert.Contains("name, id, class, control, path, runtime, xpath", ex.Message);
    }

    // A session ID that was never launched fails with a clear message and a non-zero exit code, without touching UIA.
    [Fact]
    public async Task Find_UnknownSession_PrintsErrorAndExitsNonZero()
    {
        var sessionId = Guid.NewGuid().ToString("N");

        var (stdout, stderr, exitCode) = await CliTestHelper.RunAsync(
            FindCommand.Build(), sessionId, "--by", "name", "--value", "irrelevant");

        Assert.Equal(1, exitCode);
        Assert.Empty(stdout);
        Assert.Contains(sessionId, stderr);
        Assert.Contains("not found", stderr);
    }
}
