// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Testing.XUnit;
using System.Linq;
using System.Text.Json;
using Xunit.Abstractions;

namespace ConsumerNotepadTests;

// Demonstrates attaching an EngineLogger via AppOptions.Logger to capture AutoMancer's own operational trace — separate from Expect's on-failure diagnostics, which stay self-contained.
[Collection("ConsumerNotepad")]
public sealed class EngineLoggerDemoTests(NotepadFixture fixture, ITestOutputHelper output) : AutoMancerTest(fixture)
{
    // WithOptions attaches a logger to the shared session without relaunching Notepad; the captured trace shows both element resolution and the click's branch decision.
    [Fact]
    public async Task AppOptionsLogger_CapturesElementResolutionAndActionTrace()
    {
        var writer = new StringWriter();
        var loggedApp = App.WithOptions(new AppOptions { Logger = new EngineLogger(writer) });

        await loggedApp.ClickAsync(Locator.ByControlType("Document"));

        var trace = writer.ToString();
        Assert.Contains("\"message\":\"Element resolved\"", trace);
        Assert.Contains("Clicked via", trace);
    }

    // Omitting AppOptions.Logger (the default, used by every other test in this project) requires no special handling — it's simply a no-op.
    [Fact]
    public async Task AppOptionsLogger_OmittedByDefault_RequiresNoSpecialHandling()
    {
        var unloggedApp = App.WithOptions(new AppOptions());
        await unloggedApp.ClickAsync(Locator.ByControlType("Document"));
    }

    // EngineLogger works as a plain synchronous component too — a consumer can use it for their own logging without ever touching App or await.
    [Fact]
    public void RawEngineLogger_UsedDirectlyWithoutApp_WritesExpectedTrace()
    {
        var writer = new StringWriter();
        var logger = new EngineLogger(writer);

        logger.Info("Consumer log entry", new { step = "setup" });

        Assert.Contains("\"message\":\"Consumer log entry\"", writer.ToString());
    }

    // A consumer's own IEngineLogger implementation (e.g. bridging into ILogger/Serilog) works via AppOptions.Logger just like the built-in EngineLogger.
    [Fact]
    public async Task AppOptionsLogger_CustomIEngineLoggerImplementation_ReceivesEngineEntries()
    {
        var logger = new RecordingLogger();
        var loggedApp = App.WithOptions(new AppOptions { Logger = logger });

        await loggedApp.ClickAsync(Locator.ByControlType("Document"));

        Assert.Contains(logger.Entries, entry => entry.Message == "Element resolved");
    }

    // Runs a broad sweep of App operations — click, clear, type, hover, focus, a multi-match find, and a tree snapshot — through one custom logger, prints every captured entry to the test's output pane (visible via `dotnet test -v n` or the IDE's test output), then checks it against what actually happened: resolution count, every mechanism-level and App-level entry present, entries in the order the operations ran, and structured data (not just message text) matching reality.
    [Fact]
    public async Task AppOptionsLogger_CustomIEngineLoggerImplementation_CapturesFullOperationTraceWithVisibleOutput()
    {
        var logger = new RecordingLogger();
        var loggedApp = App.WithOptions(new AppOptions { Logger = logger });
        var document = Locator.ByControlType("Document");
        const string text = "Hello from EngineLoggerDemoTests.";

        await loggedApp.ClickAsync(document);
        await loggedApp.ClearAsync(document);
        await loggedApp.TypeAsync(document, text);
        await loggedApp.HoverAsync(document);
        await loggedApp.SetFocusAsync(document);
        var buttons = await loggedApp.FindAllAsync(Locator.ByControlType("Button"));
        var snapshot = await loggedApp.SnapshotAsync();
        await loggedApp.ClearAsync(document);

        output.WriteLine($"--- {logger.Entries.Count} entries captured by the consumer's own logger ---");
        for (var i = 0; i < logger.Entries.Count; i++)
        {
            var (message, data) = logger.Entries[i];
            output.WriteLine($"[{i}] {message}{(data is null ? "" : " " + JsonSerializer.Serialize(data))}");
        }

        Assert.True(buttons.Count > 1);
        Assert.NotNull(snapshot);

        // One "Element resolved" per FindAsync-backed call: Click, Clear x2, Type, Hover, SetFocus. FindAllAsync/SnapshotAsync don't go through FindAsync, so they add none.
        Assert.Equal(6, logger.Entries.Count(entry => entry.Message == "Element resolved"));

        // Mechanism-level entries — logged by the Actions themselves via the resolved element's stamped logger.
        foreach (var prefix in new[] { "Clicked via", "Cleared via", "Typed via", "Focused via" })
            Assert.Contains(logger.Entries, entry => entry.Message.StartsWith(prefix));

        // App-level entries — logged by App itself, carrying the locator rather than the element ID.
        string[] appLevelMessagesInOrder = ["Clicked", "Cleared", "Typed", "Hovered", "Focused", "Elements found", "Snapshot taken"];
        foreach (var message in appLevelMessagesInOrder)
            Assert.Contains(logger.Entries, entry => entry.Message == message);

        var order = appLevelMessagesInOrder.Select(message => logger.Entries.FindIndex(entry => entry.Message == message)).ToList();
        Assert.True(order.SequenceEqual(order.OrderBy(i => i)), "Expected App-level entries in the order the operations actually ran.");

        // Structured data, not just message text, reflects what actually happened.
        var elementsFound = logger.Entries.First(entry => entry.Message == "Elements found");
        Assert.Equal(buttons.Count, (int)elementsFound.Data!.GetType().GetProperty("count")!.GetValue(elementsFound.Data)!);

        var snapshotTaken = logger.Entries.First(entry => entry.Message == "Snapshot taken");
        Assert.Equal(snapshot!.Count, (int)snapshotTaken.Data!.GetType().GetProperty("count")!.GetValue(snapshotTaken.Data)!);

        var typedVia = logger.Entries.First(entry => entry.Message.StartsWith("Typed via"));
        Assert.Equal(text.Length, (int)typedVia.Data!.GetType().GetProperty("textLength")!.GetValue(typedVia.Data)!);
    }

    // Minimal IEngineLogger that records the message and its structured data, standing in for a consumer's own logging stack.
    private sealed class RecordingLogger : IEngineLogger
    {
        public List<(string Message, object? Data)> Entries { get; } = [];

        public void Debug(string message, object? data = null) => Entries.Add((message, data));
        public void Info(string message, object? data = null) => Entries.Add((message, data));
        public void Warn(string message, object? data = null) => Entries.Add((message, data));
        public void Error(string message, object? data = null) => Entries.Add((message, data));
    }
}
