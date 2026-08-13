# AutoMancer
A Windows Automation Framework

Windows UI automation engine — C# class library + CLI, built on UIA3.

## What it is

AutoMancer drives real Windows desktop applications the way a user would — finding controls in the UI Automation tree and clicking, typing, and dragging on them — instead of scripting at the pixel or protocol level. The engine (`AutoMancer.Engine`) is a standalone class library with no HTTP or process dependency; the CLI (`AutoMancer.Cli`) is a thin console wrapper over it for interactive use and scripting.

Element lookup runs a fallback chain (UIA3 → UIA2 → Win32) so a control that one provider can't see is still resolvable through another, with retry until an implicit wait expires. Locators support name, automation ID, class name, control type, a tree-path syntax, UIA RuntimeId, and XPath evaluated against the UIA tree.

## Why it exists

AutoMancer is built to make Windows desktop automation reliable enough to build real test suites and tooling on top of:

- **Finds controls in complicated setups.** Not every control is well-exposed to UIA3. Falling back through UIA2 and raw Win32 window enumeration means a locator keeps working even against apps with weak or partial UIA support.
- **Runs like a function library from the command line.** Every action — `launch`, `find`, `click`, `type` — is its own subcommand taking explicit arguments and returning plain text and an exit code, so it composes into scripts, CI steps, or any process that can shell out, without writing a line of C#.
- **Built async, not bolted on.** Every engine call (`LaunchAsync`, `FindAsync`, `ClickAsync`, `TypeAsync`, …) returns a `Task` and accepts a `CancellationToken`, so it drops straight into modern `await`-based test methods instead of sync-over-async wrappers or hand-rolled polling loops.
- **A natural fit for AI-driven automation.** Each action is a small, self-contained, text-in/text-out operation against a stable session ID — the same shape an LLM tool call expects — so an agent can drive a real desktop app through the same CLI surface a human or script uses, no custom bindings required.

## Prerequisites

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (the engine targets `net10.0-windows10.0.22621.0` — no cross-platform support)

## Build

```bash
dotnet build AutoMancer.slnx
```

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

`launch`, `find`, `tree`, and `click`/`type` share a session model: `launch` starts an app and prints a session ID, which subsequent commands take as their first argument.

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

Every `find`/`click`/`type` command takes `--by <strategy> --value <value>`; the same strategies are available from C# as `Locator.By*` factory methods on `AutoMancer.Engine.Core.Locator`.

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

**Provider support:** `name`, `id`, `class`, `control`, and `runtime` all resolve through both the `uia3` and `uia2` providers. `path` and `xpath` are UIA3-only. The `win32` fallback — last resort, used when neither UIA provider exposes an accessibility tree — only understands `name` (substring match against the window title, case-insensitive) and `class` (exact window class, case-insensitive); narrow `ProviderChain` down to `["win32"]` alone and `id`, `control`, `runtime`, `path`, and `xpath` will never resolve.

### Path locator syntax

`--by path` addresses an element by its exact position in the UIA tree, using `>` to separate levels: `ControlType`, `ControlType["Name"]`, `ControlType[index]`, or a `*` wildcard for any control type. The first segment must match the session's root window itself; every segment after that matches only the **direct children** of the prior match — there's no `//`-style "search anywhere below" the way `--by xpath` has, so every intervening level has to be spelled out. A segment with no `[index]` lets every matching child at that level continue; `[index]` (0-based) picks one specific child, counted only among siblings that already match that segment's control type.

```bash
# Walk from the root window down to Notepad's File menu item
automancer find <session-id> --by path --value 'Window > Pane[2] > Pane > MenuBar > MenuItem["File"]'
```

Prefer `--by xpath` when you don't want to spell out every intervening level, or need to filter on more than an exact name — it runs real XPath against the same UIA tree instead of a fixed parent-child chain.

### XPath locator syntax

`--by xpath` runs a real XPath 1.0 expression against the live UIA tree, so `//` can search at any depth instead of naming every intervening level the way `--by path` requires. The tree is exposed as XML with the control type as the tag name and `Name`/`AutomationId`/`ClassName` as attributes — no other UIA properties are queryable this way.

```bash
# Find the File menu item anywhere in the tree, regardless of nesting depth
automancer find <session-id> --by xpath --value "//MenuItem[@Name='File']"

# Anchor the search under a specific ancestor
automancer find <session-id> --by xpath --value "//MenuBar//MenuItem[@Name='File']"
```

Positional predicates use 0-based indices (`//Button[0]` is the first `Button`), matching every other AutoMancer locator, even though XPath itself is 1-based under the hood.

### Example: Calculator

The same interactions from C#, using the engine directly instead of the CLI — this is what a real test looks like:

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

`App.LaunchPackagedAsync` activates the app by AUMID (needed for UWP/MSIX apps like Calculator); `ClickAsync`/`FindAsync` await the same retry-driven resolver the CLI uses under the hood, and `app` disposes the session automatically at the end of the `using` block.

## Logging

`AppOptions.Logger` takes an `IEngineLogger` that traces element resolution and action execution. It defaults to a plain-text `EngineLogger` writing JSON lines to `Console.Error` — stderr so it never mixes into a CLI command's stdout — so every `App` logs out of the box with no setup. Pass `Logger = null` to disable it, or your own logger to redirect it:

```csharp
var logger = new EngineLogger(new StreamWriter("automancer.log") { AutoFlush = true }, LogLevel.Debug);
await using var app = await App.LaunchAsync("notepad.exe", new AppOptions { Logger = logger });
```

`IEngineLogger` is a small interface (`Debug`/`Info`/`Warn`/`Error`, each taking a message and optional structured data) — `EngineLogger` is just the basic default implementation, writing JSON lines to a `TextWriter` (`Console.Error`, a file, a `StringWriter` for tests, filtered by a minimum `LogLevel`). If you're embedding AutoMancer in a larger engine, implement `IEngineLogger` directly against your own logging stack (`ILogger`, Serilog, xUnit's `ITestOutputHelper`, …) instead of adapting a `TextWriter`:

```csharp
public sealed class HostLogger(ILogger inner) : IEngineLogger
{
    public void Debug(string message, object? data = null) => inner.LogDebug("{Message} {@Data}", message, data);
    public void Info(string message, object? data = null) => inner.LogInformation("{Message} {@Data}", message, data);
    public void Warn(string message, object? data = null) => inner.LogWarning("{Message} {@Data}", message, data);
    public void Error(string message, object? data = null) => inner.LogError("{Message} {@Data}", message, data);
}
```

Once configured on `App`, the logger rides along automatically: `ElementResolver` stamps it onto every `ElementHandle` it resolves, so actions like `ClickAsync`/`TypeAsync` log through it without you passing a logger to each call.

See [`samples/ConsumerNotepadTests/EngineLoggerDemoTests.cs`](samples/ConsumerNotepadTests/EngineLoggerDemoTests.cs) for a working example against a live app, including a custom `IEngineLogger` whose captured entries are asserted against the actual resolve/click/clear/type sequence.

## Tests

See [CONTRIBUTING.md](CONTRIBUTING.md) for running the test suite and code style guidelines.
