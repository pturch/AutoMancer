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

A popup from `OpenPopupAsync` and a window view from `WaitForWindowAsync` are `App`s too, so `Assertions.Expect(popup, locator)` retries the same way, searching only inside that popup or window.

Asserting against an element you already resolved yourself doesn't retry. It checks once: no polling, no timeout, it either matches right now or it doesn't. Reach for it when you deliberately don't want a retry, or when you're checking a snapshot you took for some other reason anyway.

## Assertions reference

| Assertion | On | Checks |
|---|---|---|
| `ToHaveName(string)` / `ToHaveNameAsync(string)` | element / locator | `Name` equals exactly |
| `ToBeVisible()` / `ToBeVisibleAsync()` | element / locator | Not reported offscreen, and `BoundingRect` is non-zero (the same rule actions wait on) |
| `ToBeEnabledAsync()` | locator only | Not reported disabled; an unknown enabled state passes (the same rule actions wait on) |
| `ToHaveText(string)` / `ToHaveTextAsync(string)` | element / locator | `Name` contains the substring |
| `ToContainTextAsync(string)` | locator only | `Name` contains the substring |
| `ToHaveCountAsync(int, timeoutMs?, pollIntervalMs?)` | locator only | Exactly that many elements match; waits for a list still populating, which `FindAllAsync` alone doesn't |
| `ToBeUnobscuredAsync(timeoutMs?, pollIntervalMs?)` / `ToBeObscuredAsync(...)` | locator only | A click at the element's center would / wouldn't reach it, via `App.IsUnobscuredAsync`; polls, since an overlay fading in or out can briefly report the old state |
| `ToHaveValueAsync(string, timeoutMs?, pollIntervalMs?)` | locator only | The element's live value (not its `Name`) equals exactly. This is the one that reads what's actually typed into an editor. |
| `ToMatchScreenshotAsync(path, maxDifferenceRatio?, timeoutMs?, pollIntervalMs?)` | app / locator | The window's (or element's) pixels match a stored baseline PNG within the ratio (default 1%). A missing baseline is recorded and the assertion fails so you review it. Same display scale only; see [Visual Stability and Baselines](README.md#visual-stability-and-baselines) for keeping captures stable |
| `Expect(app).ToHaveTitleAsync(string \| Regex)` | app | The window title equals the string exactly, or matches the pattern |
| `Expect(app).ToBeMinimizedAsync()` / `ToBeMaximizedAsync()` / `ToBeForegroundAsync()` | app | The window's state; minimize and maximize are applied asynchronously by Windows, so wait with these instead of acting straight away |
| `Expect(app).ToHaveBoundsAsync(Rect, tolerancePx = 2)` | app | The window's bounds match on every edge within the tolerance |
| `Expect(app).ToBeRunningAsync()` / `ToHaveExitedAsync()` / `ToBeRespondingAsync()` | app | Process state: running, exited (e.g. after a close), or processing window messages |
| `Expect(app).ToHaveWindowCountAsync(int)` | app | Exactly that many visible top-level windows, the main one included |
| `Expect(app).ToHaveDialogAsync(WindowMatchOptions)` / `ToHaveNoDialogAsync()` | app | A matching visible window other than the main one is open / none is |
| `Expect(app).ToBeVisuallyStableAsync(maxDifferenceRatio = 0.001)` | app | Two consecutive window captures differ by at most that fraction of pixels |
| `Expect(app).ToHaveMemoryBelowAsync(long)` / `ToHaveHandleCountBelowAsync(int)` | app | Private bytes / open handles are under the ceiling, for leak-style checks at the end of a test |

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

That switch covers `Expect` assertions. A find that fails through `App` directly (`FindAsync`, or an action that locates its element) throws `ElementNotFoundError` instead, and has its own switch: `AppOptions.AnnotateFailureScreenshots`. When it's on, the error saves a screenshot of the window to `%TEMP%` with a red box around the closest match (or around the whole window when nothing is close), and its message and `ScreenshotPath` carry the file's path. See `FailureShapeDemoTests` in [`samples/ConsumerNotepadTests`](samples/ConsumerNotepadTests).

## Checking from the command line

AutoMancer can work without C# through the command line interface:

```bash
automancer launch notepad.exe
automancer type "Hello" --by control --value Document
automancer assert --by control --value Document --check enabled --check "text=Hello" --timeout 3000 \
  || { echo "Notepad did not show the text"; exit 1; }
```

The available checks in the `assert` command line up with the library's assertions:

| Library assertion | `automancer assert` |
|---|---|
| `ToHaveNameAsync("Save")` | `--check "name=Save"` |
| `ToHaveTextAsync` / `ToContainTextAsync("Sav")` | `--check "name~Sav"` |
| `ToBeVisibleAsync()` | `--check visible` |
| `ToBeEnabledAsync()` | `--check enabled` |
| `ToHaveCountAsync(3)` | `--check count=3` |
| `ToBeUnobscuredAsync()` / `ToBeObscuredAsync()` | `--check unobscured` / `--check "!unobscured"` |
| `ToHaveValueAsync("abc")` | `--check "text=abc"` |
| `App.WaitForVisualStableAsync()` | `--check stable` |
| `ToMatchScreenshotAsync(path)` | `screenshot --out actual.png`, then `assert --expected path --actual actual.png` |

The [CLI README](src/AutoMancer.Cli/README.md#assert) explains every command, the exit codes, and the `--json` output for tools that parse the result.

## A complete worked example

[`samples/ConsumerNotepadTests`](samples/ConsumerNotepadTests) is a full test project built entirely on this public API. Nothing in it reaches into AutoMancer's own internals. Its [README](samples/ConsumerNotepadTests/README.md) walks through what each file demonstrates, including what a failure message actually looks like in practice.

[`samples/ConsumerVsCodeTests`](samples/ConsumerVsCodeTests) is a second worked example, against a more complex app: Visual Studio Code, a full Electron app rather than a single native control. Its own [README](samples/ConsumerVsCodeTests/README.md) covers what that difference actually demands — a lazily-activated UI Automation tree, a panel that steals keyboard focus, and other gotchas a simple native app doesn't have.
