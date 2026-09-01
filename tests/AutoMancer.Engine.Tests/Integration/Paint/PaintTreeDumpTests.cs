// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text;
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using Xunit.Abstractions;

namespace AutoMancer.Engine.Tests.Integration;

// Diagnostic: launches Paint, snapshots the full UIA3 tree, and writes it to %TEMP% as text — run once to find toolbar AutomationIds.
[Collection("Paint")]
[Trait("Category", "Integration")]
public sealed class PaintTreeDumpTests(ITestOutputHelper output) : IAsyncLifetime
{
    private App _app = null!;

    // Launches Paint and waits for the toolbar to be ready, then dismisses the welcome popup.
    public async Task InitializeAsync()
    {
        _app = await App.LaunchAsync("mspaint.exe");
        var warmup = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 10_000 });
        await warmup.FindAsync(Locator.ByAutomationId("PencilTool"));

        var quick = _app.WithOptions(new AppOptions { ProviderChain = ["uia3"], ImplicitWaitMs = 2_000 });
        try { await quick.ClickAsync(Locator.ByXPath("//Window[@Name='Popup']//Button[@Name='Close']")); }
        catch { }

        await Task.Delay(300);
    }

    // Kills Paint without triggering the save dialog.
    public async Task DisposeAsync()
    {
        await _app.KillAsync();
    }

    // Snapshots the full UIA3 element tree and writes it to %TEMP%\automancer-paint-tree.txt.
    [Fact]
    public async Task DumpTree_WritesToTempFile()
    {
        var snapshot = await _app.SnapshotAsync();
        Assert.NotNull(snapshot);

        var sb = new StringBuilder();
        foreach (var root in snapshot!)
            AppendNode(sb, root, depth: 0);

        var outPath = Path.Combine(Path.GetTempPath(), "automancer-paint-tree.txt");
        await File.WriteAllTextAsync(outPath, sb.ToString());

        output.WriteLine($"Tree written — open to view: {outPath}");
        Assert.True(File.Exists(outPath));
    }

    // Recursively appends one element per line with indentation; empty/null fields are omitted.
    private static void AppendNode(StringBuilder sb, ElementSnapshot node, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.Append(indent).Append(node.ControlType ?? "?");

        if (!string.IsNullOrEmpty(node.Name))
            sb.Append($" \"{node.Name}\"");
        if (!string.IsNullOrEmpty(node.AutomationId))
            sb.Append($" [AutomationId={node.AutomationId}]");
        if (!string.IsNullOrEmpty(node.ClassName))
            sb.Append($" [Class={node.ClassName}]");

        sb.AppendLine();
        foreach (var child in node.Children)
            AppendNode(sb, child, depth + 1);
    }
}
