# AutoMancer

[![Build and Test](https://github.com/pturch/AutoMancer/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/pturch/AutoMancer/actions/workflows/build-and-test.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

**A Windows Automation Framework**

Bring older Windows applications back to life: AutoMancer enables modern testing patterns for UIA3 automation with a C# class library and CLI.

> **Status:** Phase 1 (engine + CLI) is complete and this is what's usable today. Phase 2 (richer locators, resilience, diagnostics, a visual fallback provider, and full UIA pattern coverage) is in progress — its completion is the v1 milestone. The HTTP daemon and Python/TypeScript SDKs are deliberately deferred, long-running future work, picked up once v1 has had real time in the field — not the next thing on the list. See [docs/roadmap-spec.md](docs/roadmap-spec.md) for where things actually stand.

## What it is

AutoMancer drives real Windows desktop applications the way a person would. It finds controls in the UI Automation tree and clicks, types, and drags on them, instead of scripting at the pixel or protocol level. The engine (`AutoMancer.Engine`) is a plain class library with no HTTP or process dependency. The CLI (`AutoMancer.Cli`) is a thin console wrapper around it for interactive use and scripting.

Windows automation is harder than web testing: locator rules are more complex, input simulation has more edge cases, and no single UI framework covers every app. Instead of leaving those details to the implementer this application treats provider fallback and flexible locators as first-class concerns. AutoMancer makes Windows automation as easy as modern web automation.

## Why it exists

AutoMancer is built to make Windows desktop automation reliable enough to build real test suites and tooling on top of:

- **Finds controls in complicated setups.** Not every control is well-exposed to UIA3. Falling back through UIA2 and raw Win32 window enumeration means a locator keeps working even against apps with weak or partial UIA support.
- **Runs like a function library from the command line.** Every action — `launch`, `find`, `click`, `type` — is its own subcommand taking explicit arguments and returning plain text and an exit code, so it composes into scripts and CI steps.
- **Built to run async.** Every engine call (`LaunchAsync`, `FindAsync`, `ClickAsync`, `TypeAsync`, …) returns a `Task` and accepts a `CancellationToken`, so it drops straight into modern `await`-based test methods.
- **A natural fit for AI-driven automation.** Each action is a small, self-contained, text-in/text-out operation against a stable session ID. Since this is the same shape an LLM tool call expects, an agent can drive a real desktop app through the same CLI surface a human or script uses.

## Prerequisites

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — Windows-only, no cross-platform support

## Build

```bash
dotnet build AutoMancer.slnx
```

## Tests

```bash
dotnet test tests/AutoMancer.Engine.Tests --filter "Category!=Integration"
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for running the test suite and the code style guide.

## Run the CLI

```bash
dotnet run --project src/AutoMancer.Cli -- <command> [args]
```

Or build once and run the executable directly:

```bash
dotnet build AutoMancer.slnx
src/AutoMancer.Cli/bin/Debug/net10.0-windows10.0.22621.0/automancer.exe <command> [args]
```

### Commands

`launch`, `find`, `tree`, `click`, and `type` all share a session model: `launch` starts an app and prints a session ID, and every command after that takes the ID as its first argument.

```bash
# Launch Notepad and capture the session ID it prints
automancer launch "C:\Windows\System32\notepad.exe"

# Snapshot the element tree
automancer tree <session-id>

# Find an element (--by: name, id, class, control, path, runtime, xpath)
automancer find <session-id> --by name --value "Text Editor"

# Click or double-/right-click an element
automancer click <session-id> --by name --value "Text Editor"
automancer click <session-id> --by name --value "Text Editor" --double
automancer click <session-id> --by name --value "Text Editor" --right

# Type text into an element
automancer type <session-id> "Hello, world!" --by name --value "Text Editor"
```

### Locator strategies

Every `find`/`click`/`type` command takes `--by <strategy> --value <value>`. The same strategies exist in C# as `Locator.By*` factory methods on `AutoMancer.Engine.Core.Locator`.

| `--by` | `Locator` factory | Matches | Match type |
|---|---|---|---|
| `name` | `Locator.ByName` | Visible text / accessible name | Exact |
| `id` | `Locator.ByAutomationId` | Developer-assigned automation ID — stable across relaunches | Exact |
| `class` | `Locator.ByClassName` | Win32 window class name (e.g. `Edit`, `Button`) | Exact |
| `control` | `Locator.ByControlType` | UIA semantic control type (e.g. `Button`, `Edit`), regardless of Win32 class | Exact |
| `runtime` | `Locator.ByRuntimeId` | Re-finds a specific element by the dotted ID from a previous find (`ElementHandle.Id`, e.g. `"42.333896.3.1"`) | Exact |
| `path` | `Locator.ByPath` | Position in the tree, level by level — see [Path locator syntax](#path-locator-syntax) | Segment-by-segment |
| `xpath` | `Locator.ByXPath` | XPath evaluated against the UIA tree — see [XPath locator syntax](#xpath-locator-syntax) | XPath 1.0 |

```bash
automancer find <session-id> --by id --value "PencilTool"
automancer find <session-id> --by control --value "Button"
automancer find <session-id> --by runtime --value "42.333896.3.1"
```

**Provider support:** `name`, `id`, `class`, `control`, and `runtime` all resolve through both the `uia3` and `uia2` providers. `path` and `xpath` only work against UIA3. The `win32` fallback only kicks in when neither UIA provider exposes an accessibility tree, and it only understands `name` (a case-insensitive substring match against the window title) and `class` (exact window class, case-insensitive). Narrow `ProviderChain` down to `["win32"]` alone and `id`, `control`, `runtime`, `path`, and `xpath` simply won't resolve.

### Path locator syntax

`--by path` addresses an element by its exact position in the tree, using `>` to separate levels: `ControlType`, `ControlType["Name"]`, `ControlType[index]`, or a `*` wildcard for any control type. The first segment has to match the session's root window itself, and every segment after that only matches the direct children of the previous match. There's no `//`-style "search anywhere below" like `--by xpath` has, so every level in between has to be spelled out. Leave off `[index]` and every matching child at that level continues; add it (0-based) to pick one specific child, counted only among siblings that already match that segment's control type.

```bash
# Walk from the root window down to Notepad's File menu item
automancer find <session-id> --by path --value 'Window > Pane[2] > Pane > MenuBar > MenuItem["File"]'
```

If you don't want to spell out every intervening level, or need to filter on more than an exact name, reach for `--by xpath` instead. It runs real XPath against the same UIA tree rather than a fixed parent-child chain.

### XPath locator syntax

`--by xpath` runs a real XPath 1.0 expression against the live UIA tree, so `//` can search at any depth instead of naming every level the way `--by path` requires. The tree is exposed as XML, with the control type as the tag name and `Name`/`AutomationId`/`ClassName` as attributes. No other UIA properties are queryable this way.

```bash
# Find the File menu item anywhere in the tree, regardless of nesting depth
automancer find <session-id> --by xpath --value "//MenuItem[@Name='File']"

# Anchor the search under a specific ancestor
automancer find <session-id> --by xpath --value "//MenuBar//MenuItem[@Name='File']"
```

Positional predicates use 0-based indices, so `//Button[0]` is the first `Button`, matching every other AutoMancer locator even though XPath itself is 1-based under the hood.

### Example: Calculator

Here's the same kind of interaction from C#, using the engine directly instead of the CLI. This is roughly what a real test looks like:

```csharp
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

public class CalculatorTests
{
    private const string CalculatorAumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";

    // Launches Calculator, computes 7 + 3, and asserts the result display reads 10.
    [Fact]
    public async Task Calculator_AddsTwoNumbers()
    {
        await using var app = await App.LaunchPackagedAsync(CalculatorAumid);

        await app.ClickAsync(Locator.ByAutomationId("num7Button"));
        await app.ClickAsync(Locator.ByAutomationId("plusButton"));
        await app.ClickAsync(Locator.ByAutomationId("num3Button"));
        await app.ClickAsync(Locator.ByAutomationId("equalButton"));

        var result = await app.FindAsync(Locator.ByAutomationId("CalculatorResults"));
        Assert.StartsWith("Display is 10", result.Name);
    }
}
```

`App.LaunchPackagedAsync` activates the app by AUMID, which UWP/MSIX apps like Calculator need. `ClickAsync` and `FindAsync` go through the same retry-driven resolver the CLI uses under the hood, and `app` disposes the session automatically at the end of the `using` block.

### Extended interactions

`App` covers the rest of the mouse and keyboard surface too, beyond `ClickAsync`/`TypeAsync`:

```csharp
await app.DoubleClickAsync(Locator.ByName("Report.docx"));
await app.RightClickAsync(Locator.ByName("Report.docx"));
await app.HoverAsync(Locator.ByName("Tooltip target"));
await app.HotkeyAsync(new KeyModifiers(Control: true), [Key.S]);          // Ctrl+S
await app.DragThroughAsync([(100, 100), (150, 140), (200, 180)]);         // click-drag through waypoints
await app.ScrollWheelAsync(Locator.ByControlType("Document"), deltaX: 0, deltaY: -3);
```

### Screenshots and waiting

```csharp
byte[] png = await app.ScreenshotAsync();                                 // full-window PNG capture

// Waits for a locator to disappear (e.g. a dialog closing) instead of guessing with a fixed delay.
await app.WaitUntilGoneAsync(Locator.ByName("Save changes?"));

// Waits for an arbitrary predicate against the resolved element, not just "found".
var button = await app.WaitForAsync(Locator.ByAutomationId("SubmitButton"), e => e.IsEnabled == true);
```

## Testing your own app

`AutoMancer.Testing` adds a retry-asserting `Expect()` API on top of `App`/`Locator`, so assertions poll instead of racing a single `FindAsync`:

```csharp
using static AutoMancer.Testing.Assertions;

await app.ClickAsync(Locator.ByControlType("Document"));
await app.TypeAsync(Locator.ByControlType("Document"), "Hello, AutoMancer.");

// Polls via App.WaitForAsync until the condition holds or the implicit wait expires.
await Expect(app, Locator.ByControlType("Document")).ToHaveValueAsync("Hello, AutoMancer.");

// One-shot check against an element you already resolved — no retry.
var element = await app.FindAsync(Locator.ByControlType("Document"));
Expect(element).ToBeVisible();
```

`AutoMancer.Testing.XUnit` builds on that for xUnit, with an `AppFixture` for launch/teardown and an `AutoMancerTest` base class that exposes `App` and `Expect` to your test classes. A failed `Expect` captures a screenshot automatically when `AutoMancerTestOptions.CaptureScreenshotsOnFailure` is on, and folds the path into the assertion message.

See **[TESTING.md](TESTING.md)** for the full guide, including the fixture pattern, and [`samples/ConsumerNotepadTests`](samples/ConsumerNotepadTests) for a complete working test project built entirely on public API.

## Logging

`AppOptions.Logger` takes an `IEngineLogger` that traces element resolution and action execution. By default it's a plain-text `EngineLogger` writing JSON lines to `Console.Error` (stderr, so it never mixes into a CLI command's stdout), so every `App` logs out of the box with no setup. Pass `Logger = null` to turn it off, or your own logger to redirect it:

```csharp
var logger = new EngineLogger(new StreamWriter("automancer.log") { AutoFlush = true }, LogLevel.Debug);
await using var app = await App.LaunchAsync("notepad.exe", new AppOptions { Logger = logger });
```

`IEngineLogger` is a small interface: `Debug`/`Info`/`Warn`/`Error`, each taking a message and optional structured data. `EngineLogger` is just the default implementation, writing JSON lines to a `TextWriter`, whether that's `Console.Error`, a file, or a `StringWriter` in tests, filtered by a minimum `LogLevel`. If you're embedding AutoMancer in a larger app, it's usually easier to implement `IEngineLogger` directly against your own logging stack (`ILogger`, Serilog, xUnit's `ITestOutputHelper`, whatever you've got) than to adapt a `TextWriter`:

```csharp
public sealed class HostLogger(ILogger inner) : IEngineLogger
{
    public void Debug(string message, object? data = null) => inner.LogDebug("{Message} {@Data}", message, data);
    public void Info(string message, object? data = null) => inner.LogInformation("{Message} {@Data}", message, data);
    public void Warn(string message, object? data = null) => inner.LogWarning("{Message} {@Data}", message, data);
    public void Error(string message, object? data = null) => inner.LogError("{Message} {@Data}", message, data);
}
```

Once it's set on `App`, the logger rides along automatically. `ElementResolver` stamps it onto every `ElementHandle` it resolves, so actions like `ClickAsync`/`TypeAsync` log through it without you passing a logger to each call.

See [`samples/ConsumerNotepadTests/EngineLoggerDemoTests.cs`](samples/ConsumerNotepadTests/EngineLoggerDemoTests.cs) for a working example against a live app, including a custom `IEngineLogger` whose entries get asserted against the actual resolve/click/clear/type sequence.

## License

Apache License 2.0 — see [LICENSE](LICENSE).

