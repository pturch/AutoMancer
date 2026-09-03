# Writing Tests with AutoMancer

A guide to testing your own app with AutoMancer's testing libraries, `AutoMancer.Testing` and `AutoMancer.Testing.XUnit`, which add a retry-aware assertion helper on top of the engine.


## Write your first test

There are three pieces to this, and none of them are much code.

First, a fixture that launches the app. It's a subclass of `AppFixture`, which plugs into xUnit's async lifecycle, so the app launches and tears down automatically with no manual setup or cleanup code in your tests.

```csharp
public sealed class NotepadFixture : AppFixture
{
    protected override async Task<App> CreateAppAsync()
    {
        var app = await App.LaunchAsync("notepad.exe");
        await app.FindAsync(Locator.ByControlType("Document")); // warm-up: don't return until the UI tree is ready
        return app;
    }
}
```

Then a collection that shares one fixture instance, and the single app it launches, across every test class in it. This matters for a single-instance app like Notepad. Letting each test class launch its own instance would just mean every class fighting over the same OS-level window, so instead the whole collection reuses the one app the fixture launches once.

```csharp
[CollectionDefinition("Notepad", DisableParallelization = true)]
public sealed class NotepadCollection : ICollectionFixture<NotepadFixture> { }
```

Disabling parallelization stops xUnit from running this collection's test classes against each other at the same time. That matters here because they're all driving the same window, not separate ones.

Then a test class extending the shared base class, tagged with the collection name and taking the fixture through its constructor. That base class gives you the running app and the assertion helper directly, so you're not touching fixture plumbing yourself.

```csharp
[Collection("Notepad")]
public sealed class MyFirstTests(NotepadFixture fixture) : AutoMancerTest(fixture)
{
    [Fact]
    public async Task Types_Into_Document()
    {
        var document = Locator.ByControlType("Document");
        await App.ClickAsync(document);
        await App.TypeAsync(document, "Hello, AutoMancer.");

        await Expect(document).ToHaveValueAsync("Hello, AutoMancer.");
    }
}
```

Every other test class that needs Notepad carries the same collection tag and constructor parameter, and they all share the one app the fixture already launched. See [`samples/ConsumerNotepadTests`](samples/ConsumerNotepadTests) for this pattern across a dozen files.

If your app under test can run multiple instances side by side, you don't need any of that. Skip the collection and use xUnit's ordinary class fixture instead, so each test class gets its own fresh app.

```csharp
public sealed class MyFirstTests(NotepadFixture fixture) : AutoMancerTest(fixture), IClassFixture<NotepadFixture>
{
    // same body as above
}
```

That's the whole pattern. Everything past this point is the same three pieces, aimed at different corners of the API.

## Two kinds of assertion

Asserting against a locator retries. It keeps polling until the condition holds or the implicit wait runs out, so it survives ordinary UI timing, like an element that isn't focused, enabled, or populated yet. Use this almost everywhere.

Asserting against an element you already resolved yourself doesn't retry. It checks once: no polling, no timeout, it either matches right now or it doesn't. Reach for it when you deliberately don't want a retry, or when you're checking a snapshot you took for some other reason anyway.

## Assertions reference

| Assertion | On | Checks |
|---|---|---|
| `ToHaveName(string)` / `ToHaveNameAsync(string)` | element / locator | `Name` equals exactly |
| `ToBeVisible()` / `ToBeVisibleAsync()` | element / locator | `BoundingRect` is non-zero |
| `ToHaveText(string)` / `ToHaveTextAsync(string)` | element / locator | `Name` contains the substring |
| `ToHaveValueAsync(string, timeoutMs?, pollIntervalMs?)` | locator only | The element's live value (not its `Name`) equals exactly. This is the one that reads what's actually typed into an editor. |

The locator-based, retrying assertions and the element-based, one-shot ones live in separate helper classes internally, but you call both the same way, through the `Expect` you saw above. Either kind throws on failure, and the message names what was expected alongside what was actually found.

## Failure diagnostics

Turn on `AutoMancerTestOptions.CaptureScreenshotsOnFailure` once, and every failed locator-based assertion saves a screenshot automatically and folds its path into the failure message. A module initializer in your test assembly is a reasonable place to set it:

```csharp
internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Configure() => AutoMancerTestOptions.CaptureScreenshotsOnFailure = true;
}
```

It's a process-wide switch, not a per-class setting, so setting it once covers every test class. Screenshots land in the configured directory, `%TEMP%` by default.

## A complete worked example

[`samples/ConsumerNotepadTests`](samples/ConsumerNotepadTests) is a full test project built entirely on this public API. Nothing in it reaches into AutoMancer's own internals. Its [README](samples/ConsumerNotepadTests/README.md) walks through what each file demonstrates, including what a failure message actually looks like in practice.
