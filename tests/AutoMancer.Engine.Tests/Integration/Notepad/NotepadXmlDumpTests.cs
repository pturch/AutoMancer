// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using Xunit.Abstractions;

namespace AutoMancer.Engine.Tests.Integration;

// Diagnostic test — snapshots the live Notepad UIA tree and dumps it as the XML that XPathEvaluator works against.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class NotepadXmlDumpTests(NotepadFixture fixture, ITestOutputHelper output) : IClassFixture<NotepadFixture>
{
    // Snapshots the Notepad UIA3 tree and writes the XML to test output so XPath expressions can be designed against it.
    [Fact]
    public async Task DumpUia3XmlTree()
    {
        var snapshots = await fixture.App.SnapshotAsync() ?? [];
        var xml = XPathEvaluator.ToXml(snapshots);
        output.WriteLine(xml);
        Assert.NotEmpty(xml);
    }

    // Verifies that the AutoMancerXPath locator strategy resolves a live element end-to-end via Uia3Provider.
    [Fact]
    public async Task XPathLocator_FindsFileMenuItem()
    {
        var element = await fixture.App.FindAsync(Locator.ByXPath("//MenuItem[@Name='File']"));

        Assert.NotNull(element);
        Assert.Equal("File", element.Name);
        Assert.Equal("uia3", element.ResolvedVia);
    }
}
