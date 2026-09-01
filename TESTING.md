# Writing Tests with AutoMancer

A guide to testing your own app with `AutoMancer.Testing`/`AutoMancer.Testing.XUnit`, the retry-asserting `Expect()` API built on top of the engine.

Looking for how to run or contribute to AutoMancer's own test suite instead? See [CONTRIBUTING.md](CONTRIBUTING.md).

## Prerequisites

Same as the engine — see [README.md](README.md#prerequisites).

## Write your first test

There are three pieces to this, and none of them are much code.

First, a fixture that launches the app. `AppFixture` is `IAsyncLifetime`: it launches in `InitializeAsync` and tears down in `DisposeAsync` automatically.

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

Then a test class extending `AutoMancerTest`, paired with `IClassFixture<T>`. `IClassFixture` gives each test class its own fresh `NotepadFixture`, shared across every `[Fact]` in that class but not across classes. `AutoMancerTest` exposes `App` and `Expect` so you're not calling `Assertions.Expect` or touching fixture plumbing yourself.

```csharp
public sealed class MyFirstTests(NotepadFixture fixture) : AutoMancerTest(fixture), IClassFixture<NotepadFixture>
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

And if your app is single-instance like Notepad, a shared `[Collection]`:

```csharp
[CollectionDefinition("Notepad", DisableParallelization = true)]
public sealed class NotepadCollection { }
```

Carry `[Collection("Notepad")]` on every test class that shares this constraint. It doesn't share the `App` itself — that's still `IClassFixture`'s job, per class — it just stops xUnit from running these classes' launching `InitializeAsync`s in parallel, which would collide the same way running two Notepad-driving test projects at once does (more on that in [CONTRIBUTING.md](CONTRIBUTING.md)). Most apps don't need this at all.

That's the whole pattern. Everything past this point is the same three pieces, aimed at different corners of the API.

## Two kinds of Expect

`Expect(locator)` retries. It polls through `App.WaitForAsync` until the condition holds or the implicit wait runs out, so it survives ordinary UI timing, like an element that isn't focused, enabled, or populated yet. Use this almost everywhere.

`Expect(element)` doesn't retry. It runs once, against an `ElementHandle` you already resolved yourself: no polling, no timeout, it either matches right now or it doesn't. Reach for it when you deliberately don't want a retry, or when you're asserting on a snapshot you took for some other reason anyway.

## Assertions reference

| Assertion | On | Checks |
|---|---|---|
| `ToHaveName(string)` / `ToHaveNameAsync(string)` | element / locator | `Name` equals exactly |
| `ToBeVisible()` / `ToBeVisibleAsync()` | element / locator | `BoundingRect` is non-zero |
| `ToHaveText(string)` / `ToHaveTextAsync(string)` | element / locator | `Name` contains the substring |
| `ToHaveValueAsync(string, timeoutMs?, pollIntervalMs?)` | locator only | The element's `ValuePattern`/`TextPattern` value (not `Name`) equals exactly — the one that reads live content, e.g. what's actually typed into an editor |

The locator-based (`...Async`) forms live on `LocatorExpect`; the element-based, synchronous ones live on `ElementExpect`. Both throw `ExpectFailedError` on failure, and the message names what was expected alongside what was actually found.

## Failure diagnostics

Set `AutoMancerTestOptions.CaptureScreenshotsOnFailure = true` once, a `[ModuleInitializer]` in your test assembly is a reasonable place for it, and every failed locator-based `Expect` saves a screenshot to `AutoMancerTestOptions.ScreenshotDirectory` (`%TEMP%` by default) and folds the path into the exception message:

```csharp
internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Configure() => AutoMancerTestOptions.CaptureScreenshotsOnFailure = true;
}
```

It's a process-wide switch, not a per-class setting. Set it once and every test class picks it up through `AutoMancerTest`'s default `ExpectOptions`.

## A complete worked example

[`samples/ConsumerNotepadTests`](samples/ConsumerNotepadTests) is a full test project built entirely on this public API, nothing in it reaches into AutoMancer's own internals. Its [README](samples/ConsumerNotepadTests/README.md) walks through what each file demonstrates, including what an `Expect` failure message actually looks like in practice.
