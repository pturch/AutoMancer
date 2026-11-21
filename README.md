# AutoMancer

[![Build and Test](https://github.com/pturch/AutoMancer/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/pturch/AutoMancer/actions/workflows/build-and-test.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

**A Windows Automation Framework**

Bring older Windows applications back to life. AutoMancer enables modern testing patterns for UI automation with a C# class library and CLI.

> **Status:** Phase 1 (engine + CLI) is complete and this is what's usable today. Phase 2 (richer locators, resilience, diagnostics, idle-wait and visual regression, and full UIA pattern coverage) is in progress. Its completion will be the v1 milestone. The HTTP daemon and Python/TypeScript SDKs are deliberately deferred future work, to be picked up once v1 has had real time in the field. See [docs/roadmap-spec.md](docs/roadmap-spec.md) for where things actually stand.

## Overview:

AutoMancer drives real Windows desktop applications the way a person would. It finds controls in the UI Automation tree and clicks, types, and drags on them, instead of scripting at the pixel or protocol level. The engine (`AutoMancer.Engine`) is a plain class library with no HTTP or process dependency. The CLI (`AutoMancer.Cli`) is a thin console wrapper around it for interactive use and scripting.

Windows automation is harder than web testing. Locator rules are more complex, input simulation has more edge cases, and no single UI framework covers every app. Instead of leaving those details to the implementer this application treats provider fallback and flexible locators as first-class concerns. AutoMancer makes Windows automation as easy as modern web automation.

## Features:

AutoMancer is built to make Windows desktop automation reliable enough to build real test suites and tooling on top of:

- **Finds controls in complicated setups.** Not every control is well-exposed to UIA3. Falling back through UIA2 and raw Win32 window enumeration means a locator keeps working even against apps with weak or partial UIA support.
- **Runs like a function library from the command line.** Every action — `launch`, `find`, `click`, `type`, `key`, `window`, `screenshot`, `assert`, … — is its own subcommand taking explicit arguments and returning plain text (or one JSON object) and an exit code, so it composes into scripts and CI steps. `assert` checks what happened and doubles as the wait.
- **Built to run async.** Every engine call (`LaunchAsync`, `FindAsync`, `ClickAsync`, `TypeAsync`, …) returns a `Task` and accepts a `CancellationToken`, so it drops straight into modern `await`-based test methods.
- **A natural fit for AI-driven automation.** Each action is a small, self-contained, text-in/text-out operation against an app identified by its process ID, with `--json` output and distinct exit codes for "the check failed" versus "something broke". Since this is the same shape an LLM tool call expects, an agent can drive a real desktop app through the same CLI surface a human or script uses.

## Getting Started:

### Prerequisites:

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — Windows-only, no cross-platform support

### Build:

```bash
dotnet build AutoMancer.slnx
```

Builds everything in the solution: the engine (`AutoMancer.Engine`), the CLI (`AutoMancer.Cli`), and every test project. To build just one project, point `dotnet build` at its `.csproj` instead, e.g. `dotnet build src/AutoMancer.Cli`.

### Tests:

```bash
dotnet test tests/AutoMancer.Engine.Tests --filter "Category!=Integration"
dotnet test tests/AutoMancer.Cli.Tests
```

`AutoMancer.Cli.Tests` covers the command line (argument handling, targeting, exit codes, the JSON output, `configure`). It needs no live UI, so it's safe to run alongside anything.

Runs the engine's unit tests. The `Category=Integration` tests and `samples/ConsumerNotepadTests` drive a real Notepad window, so they need an interactive Windows session and aren't part of CI. `samples/ConsumerVsCodeTests` needs Windows + Visual Studio Code installed instead, and launches its own isolated instance rather than sharing a window — but like the others, it still drives real keyboard/mouse input, so it shouldn't run concurrently with them either. See [CONTRIBUTING.md](CONTRIBUTING.md#build--test) for more information.

### Running via C#:

Reference `AutoMancer.Engine` from your own project and drive an app directly:

```csharp
using AutoMancer.Engine;
using AutoMancer.Engine.Core;

await using var app = await App.LaunchAsync("notepad.exe");

await app.ClickAsync(Locator.ByControlType("Document"));
await app.TypeAsync(Locator.ByControlType("Document"), "Hello, world!");

var element = await app.FindAsync(Locator.ByControlType("Document"));
Console.WriteLine(element.Name);
```

See [Example: Calculator](#example-calculator) below for a fuller worked example, including UWP/MSIX apps and the rest of the `App` surface.

### Running the CLI:

Once you've built locally, you can run the executable directly:

```bash
dotnet build AutoMancer.slnx
src/AutoMancer.Cli/bin/Debug/<net10 version>/automancer.exe <command> [args]
```

### CLI Commands:

Every command acts on one app. Name it with `--pid <n>`, or leave that out and the command uses the *current target*, which is whichever app `launch` or `configure --attach` pointed at last. Add `--json` to any command and you get a single JSON object back instead of text.

| Command | What it does |
|---|---|
| `launch <executable>` | Starts the app, makes it the current target, and prints its PID. `--args` passes command-line arguments straight through |
| `configure` | Shows or changes the current target (`--attach`, `--clear`) and the saved engine settings (`--implicit-wait`, `--action-delay`, …, `--reset`) |
| `find` | Resolves one element and prints its properties (name, automation ID, control type, class name, bounding rect, …) |
| `tree` | Snapshots the element tree and prints it as an indented list; `--depth` and `--interactive-only` keep it short |
| `click` | Resolves one element and clicks it; `--double` and `--right` change the click |
| `type <text>` | Resolves one element and types the text into it |
| `key <key>`, `hotkey <chord>` | Presses a key (`Enter`, `F5`, …) or a combination (`ctrl+shift+s`) |
| `hover`, `focus` | Moves the pointer over an element, or gives it keyboard focus |
| `screenshot --out file.png` | Saves the window, or one element, as a PNG |
| `window` | Reports the window's position and size, or moves, resizes, minimizes, maximizes, closes or kills it |
| `assert` | Checks properties, counts, stability, or two images, and exits 1 with what it expected and what it found when a check fails |

Commands that act on an element take `--by <strategy> --value <text>`, and can add `--within-by` and `--within-value` to search only inside another element. The [CLI README](src/AutoMancer.Cli/README.md) is the full reference: targeting, every `assert` check, the saved settings, and the shape of the JSON.

### Exit codes:

A script can tell what kind of problem it hit from the exit code. The split that matters most is between a failed check (1) and a broken tool (4):

| Exit | Meaning |
|---|---|
| 0 | Success |
| 1 | The app ran fine, but an expectation wasn't met (a failed assertion, an element not found, a timeout) |
| 2 | The command line was wrong |
| 3 | There was no app to act on: no target, it has exited, or the PID has no window |
| 4 | The tool or the environment broke |

### CLI Example:

A short session against Notepad. `launch` makes the app the current target, so nothing after it needs an ID:

```bash
# Launch Notepad; it becomes the current target
automancer launch "C:\Windows\System32\notepad.exe"

# --args passes command-line arguments straight through to the launched process
automancer launch "C:\...\Code.exe" --args "--new-window \"C:\my-project\""

# Snapshot the element tree to see what can be acted on
automancer tree --interactive-only --depth 1

# Find an element (--by: name, id, class, control, path, runtime, xpath)
automancer find --by control --value Document

# Click or double-/right-click an element
automancer click --by control --value Document
automancer click --by control --value Document --double
automancer click --by control --value Document --right

# Type text into an element and press keys
automancer type "Hello, world!" --by control --value Document
automancer hotkey ctrl+a

# Check the result; a failed check exits 1, and --timeout makes assert wait for it
automancer assert --by control --value Document --check enabled --check "text~Hello" --timeout 3000

# Act on a specific app when more than one is running
automancer --pid 31608 window --state max

# Attach to an app that's already open, by process name or PID
automancer configure --attach notepad
```

## Locator Reference:

### Locators:

A `Locator` is how you address an element. Every element-resolving operation, in C# or via the CLI, takes one. In C#, `Locator.By*` factory methods on `AutoMancer.Engine.Core.Locator` build one directly; `FindAsync`, `ClickAsync`, `TypeAsync`, and every other resolving `App` method accepts it. Every CLI command that takes `--by <strategy> --value <value>` builds the same `Locator` under the hood.

| Strategy | `Locator` factory (C#) | `--by` value (CLI) | Matches | Match type |
|---|---|---|---|---|
| Name | `Locator.ByName` | `name` | Visible text / accessible name | Exact |
| Automation ID | `Locator.ByAutomationId` | `id` | Developer-assigned automation ID — stable across relaunches | Exact |
| Class name | `Locator.ByClassName` | `class` | Win32 window class name (e.g. `Edit`, `Button`) | Exact |
| Control type | `Locator.ByControlType` | `control` | UIA semantic control type (e.g. `Button`, `Edit`), regardless of Win32 class | Exact |
| Runtime ID | `Locator.ByRuntimeId` | `runtime` | Re-finds an element you've already seen, by its dotted `ElementHandle.Id` (e.g. `"42.333896.3.1"`) | Exact |
| Path | `Locator.ByPath` | `path` | Position in the tree, level by level — see [Path locators](#path-locators) | Segment-by-segment |
| XPath | `Locator.ByXPath` | `xpath` | XPath evaluated against the UIA tree — see [XPath locators](#xpath-locators) | XPath 1.0 |

```csharp
Locator.ByAutomationId("PencilTool")
Locator.ByControlType("Button")
Locator.ByRuntimeId("42.333896.3.1")
```

```bash
automancer find --by id --value "PencilTool"
automancer find --by control --value "Button"
automancer find --by runtime --value "42.333896.3.1"
```

**Provider support:** Name, automation ID, class name, control type, and runtime ID all resolve through both the `uia3` and `uia2` providers. Path and XPath locators only work against UIA3. The `win32` fallback only kicks in when neither UIA provider exposes an accessibility tree, and it only understands name (a case-insensitive substring match against the window title) and class name (exact window class, case-insensitive). Narrow `ProviderChain` down to `["win32"]` alone and automation ID, control type, runtime ID, path, and XPath locators simply won't resolve.

**A runtime ID only lasts as long as its element.** Unlike automation ID, which is stable across relaunches, a runtime ID is only valid for the lifetime of the app instance that produced it. Use it to confirm you're still looking at the same element after doing something to it — not as a locator you write into a test ahead of time, since you won't have one until you've already found the element once.

### Locators vs. Element Handles:

Apps rebuild their UI all the time. Lists frequently re-render, dialogs close and reopen. Elements found a moment ago can be gone by the time you use them. Retrying logic can get dicey, so who deals with what depends on what you handed AutoMancer:

**A `Locator` is AutoMancer's problem.** Any method that takes one (`ClickAsync`, `GetValueAsync`, `FindAllScopedAsync`, ...) re-finds the element when it needs it. If the element turns out to be gone, AutoMancer runs that locator again until the implicit wait runs out. `AppOptions.ReresolveOnStale` (default `true`) controls this; set it to `false` and you get a `StaleElementError` instead, for you to handle however you'd like.

**An `ElementHandle` is your problem.** A handle (what `FindAsync` returns) points at one specific element. If that element leaves the UI tree, acting on the handle or reading its value throws `StaleElementError`. AutoMancer has no locator to re-run, and following a look-alike replacement could act on the wrong thing. A handle's properties (`Name`, `BoundingRect`, `IsEnabled`, ...) are a snapshot from when it was found: they never throw, but they never update either, and `Expect(handle)` asserts against that same snapshot.

Pass locators wherever you can (especially for scopes), and hold a handle only when you specifically want that one element. To pass a handle where a locator is expected, wrap it in `Locator.ByRuntimeId(handle.Id)`. That locator only ever matches that exact element, so if the element is gone it won't follow a replacement; you'll get `ElementNotFoundError` once the implicit wait runs out.

### Path Locators:

A path locator addresses an element by its exact position in the tree, using `>` to separate levels: `ControlType`, `ControlType["Name"]`, `ControlType[index]`, or a `*` wildcard for any control type. The first segment has to match the root window itself, and every segment after that only matches the direct children of the previous match. There's no `//`-style "search anywhere below" like an XPath locator has, so every level in between has to be spelled out. Leave off `[index]` and every matching child at that level continues; add it (0-based) to pick one specific child, counted only among siblings that already match that segment's control type.

```csharp
Locator.ByPath("Window > Pane[2] > Pane > MenuBar > MenuItem[\"File\"]")
```

```bash
# Walk from the root window down to Notepad's File menu item
automancer find --by path --value 'Window > Pane[2] > Pane > MenuBar > MenuItem["File"]'
```

If you don't want to spell out every intervening level, or need to filter on more than an exact name, reach for an XPath locator instead — it runs real XPath against the same UIA tree rather than a fixed parent-child chain.

### XPath Locators:

An XPath locator runs a real XPath 1.0 expression against the live UIA tree, so `//` can search at any depth instead of naming every level the way a path locator requires. The tree is exposed as XML, with the control type as the tag name and `Name`/`AutomationId`/`ClassName` as attributes. No other UIA properties are queryable this way.

```csharp
Locator.ByXPath("//MenuItem[@Name='File']")
Locator.ByXPath("//MenuBar//MenuItem[@Name='File']")
```

```bash
# Find the File menu item anywhere in the tree, regardless of nesting depth
automancer find --by xpath --value "//MenuItem[@Name='File']"

# Anchor the search under a specific ancestor
automancer find --by xpath --value "//MenuBar//MenuItem[@Name='File']"
```

Positional predicates use 0-based indices, so `//Button[0]` is the first `Button`, matching every other AutoMancer locator even though XPath itself is 1-based under the hood.

### Searching inside an element:

A name or control type often repeats across an app: a dozen `Button`s, or an `OK` in every dialog. To search inside just one element, scope the lookup to it. In C#, `FindScopedAsync` and `FindAllScopedAsync` take the scope as a `Locator`. On the command line, add `--within-by` and `--within-value` to any command that takes `--by`/`--value`:

```bash
# A menu item inside Notepad's menu bar, not one of the others in the window
automancer find --by control --value MenuItem --within-by id --within-value MenuBar

# How many menu items are inside it? (File, Edit, View)
automancer assert --by control --value MenuItem --check count=3 --within-by id --within-value MenuBar
```

The scope is resolved from the window root, and the first match is used if several elements match. Path and XPath locators always search from the window root, so the CLI refuses to combine them with a scope instead of quietly ignoring it.

## Example: Calculator

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

### Extended Interactions:

`App` covers the rest of the mouse and keyboard surface too, beyond `ClickAsync`/`TypeAsync`:

```csharp
await app.DoubleClickAsync(Locator.ByName("Report.docx"));
await app.RightClickAsync(Locator.ByName("Report.docx"));
await app.HoverAsync(Locator.ByName("Tooltip target"));
await app.HotkeyAsync(new KeyModifiers(Control: true), [Key.S]);          // Ctrl+S
await app.DragThroughAsync([(100, 100), (150, 140), (200, 180)]);         // click-drag through waypoints
await app.ScrollWheelAsync(Locator.ByControlType("Document"), deltaX: 0, deltaY: -3);
```

### Popups, Context Menus and Windows:

An `App` only searches inside the window it's rooted at, so a menu or dialog that opens in a window of its own won't turn up in a plain `FindAsync`. Instead, you can scope to it:

**For a popup (a menu, dropdown, tooltip or flyout),** `OpenPopupAsync` triggers it on a target and waits for it to appear. The trigger is a left-click by default, but it could be a right-click, Shift+F10, Alt+Down or hover. `expect` describes what the popup contains, so the correct one is found. You get back a `Popup`, which works like an `App` that only searches inside the popup: every action, wait and `Expect` behaves as usual.

**For a dialog or second window,** `WaitForWindowAsync` waits for a matching window of the same app and returns an `App` rooted at it, called a window view. Disposing a view never closes the app.

```csharp
// Context menus: the one-call shortcut, or open, inspect and act.
await app.ClickContextMenuItemAsync(Locator.ByControlType("Document"), "Undo");
var menu = await app.OpenContextMenuAsync(Locator.ByControlType("Document"));
var items = await menu.FindAllAsync(Locator.ByControlType("MenuItem"));
await menu.CloseAsync();

// Any other popup: a dropdown, tooltip or flyout, named by what it contains.
var dropdown = await app.OpenPopupAsync(Locator.ByAutomationId("Units1"), PopupTrigger.AltDown, expect: Locator.ByControlType("ListItem"));
await dropdown.ClickAsync(Locator.ByName("Inches"));

// A dialog: wait for its window, then work inside it.
await app.HotkeyAsync(new KeyModifiers(Control: true), [Key.O]);
var dialog = await app.WaitForWindowAsync(new WindowMatchOptions { TitleContains = "Open" });
await dialog.TypeAsync(Locator.ByClassName("Edit"), "notes.txt");
await dialog.ClickAsync(Locator.ByName("Cancel"));
```

Once a popup closes it stays closed. Searching it throws `StaleElementError`, so open it again from its target. `CloseAsync` presses Escape, and does nothing if the popup is already gone. Tooltips ignore Escape, so move the pointer away and call `WaitUntilClosedAsync` instead.

A submenu is just a popup opened from inside another one, with `menu.OpenPopupAsync(...)`. Escape closes one level at a time, so close nested popups innermost first: `submenu.CloseAsync()`, then `menu.CloseAsync()`.

Classic Win32 menus that UI Automation can't read are still reachable through the `win32` provider. Real apps get some of this wrong, and [WINDOWS_TESTING.md](WINDOWS_TESTING.md) lists what we found and the workaround for each.

### Actionability:

Element actions (`ClickAsync`, `TypeAsync`, `ClearAsync`, `DoubleClickAsync`, `HoverAsync`, `ScrollWheelAsync`, ...) only act on an element a user could use: enabled, and visible — not offscreen, with a real on-screen size. Simulated mouse input also waits for the element to stop moving, so a click can't miss something still sliding into place, like an opening dropdown. The check reads the element right before acting, and applies to the app's accessibility shortcuts as well as simulated mouse and keyboard input. AutoMancer stays grounded in only performing behaviors that a user could.

If the element isn't usable yet the action finds it again and keeps checking until the implicit wait runs out, then throws `ElementNotInteractableError` saying why (disabled, offscreen, or no on-screen size). `ScrollIntoViewAsync`, `SetFocusAsync`, and `GetValueAsync` don't check, since making an element visible or reading it is their job. `WaitConditions.IsVisible()` and `IsEnabled()` use the same rules, so a wait and the action after it agree on what counts as usable.

#### Forcing an action:

Sometimes acting on an element no user could reach is exactly what you want. Can you verify no text gets entered into a disabled textbox? You might just need a workaround for an app that wrongly reports a visible control as offscreen. `app.Force` runs the same actions without the readiness checks:

```csharp
await app.Force.ClickAsync(Locator.ByName("New tab"));   // clicks it even while its menu is closed
```

It also skips waiting for the element to stop moving. A stale element still throws `StaleElementError`, and simulated mouse inputs still need an on-screen point to aim at. Through `app.Force` those fail right away instead of waiting. `app.Force` is the same session as `app`, so disposing it never closes the app, and keys held through it are released when `app` is disposed.

#### Checking for something in the way:

Actions don't check whether something is covering the element. `IsUnobscuredAsync` is an opt-in check of whether a click at the element's center would actually reach it:

```csharp
bool clear = await app.IsUnobscuredAsync(Locator.ByAutomationId("SettingsButton"));
```

It returns `false` when another window covers that point, when an overlay (like a dimmed backdrop) covers it, or when the element has no on-screen size. 
Overlays inside the app are only detected for elements found by the `uia3` provider. For `uia2` and `win32` elements it checks other windows only. 

### Screenshots and Waiting:

Use these methods to help with test execution and debugging:

```csharp
byte[] png = await app.TakeScreenshotAsync();                                 // full-window PNG capture

// Waits for a locator to disappear (e.g. a dialog closing) instead of guessing with a fixed delay.
await app.WaitUntilGoneAsync(Locator.ByName("Save changes?"));

// Waits for an arbitrary predicate against the resolved element, not just "found".
var button = await app.WaitForAsync(Locator.ByAutomationId("SubmitButton"), e => e.IsEnabled == true);

// Snapshots the whole element tree as nested ElementSnapshot roots; DescendantsAndSelf flattens it to search or LINQ over.
var roots = await app.SnapshotAsync();
var everyButton = roots!.DescendantsAndSelf().Where(e => e.ControlType == "Button");
```

### Visual Stability and Baselines:

For apps with animations, or UI you can only check by how it looks, AutoMancer works from the window's pixels:

```csharp
// Waits until two consecutive captures match (default: at most 0.1% of pixels differ, polled every 100ms, 5s timeout, TimeoutException after that).
await app.WaitForVisualStableAsync();
await app.WaitForVisualStableAsync(Locator.ByAutomationId("Chart"));   // watches only that element's pixels

byte[] chart = await app.TakeScreenshotAsync(Locator.ByAutomationId("Chart"));   // just the element, cropped from the window

// Save a capture to a file (missing folders are created), then compare a later capture against it.
await app.TakeScreenshotAsync("baselines/main-window.png");
VisualComparison result = await app.CompareScreenshotAsync("baselines/main-window.png");   // result.Passed, result.DifferenceRatio

// Compares against a stored baseline PNG (default: at most 1% of pixels differ), retrying until it matches or the wait expires.
// The first run records the baseline and fails so you can review it; rerun to compare.
await Expect(app).ToMatchScreenshotAsync("baselines/main-window.png");
await Expect(app, Locator.ByAutomationId("Chart")).ToMatchScreenshotAsync("baselines/chart.png");
```

Comparisons are same-display-scale only. Each capture records the window's DPI, and comparing against a screenshot recorded at a different scale (100% vs 150%) fails with a message saying to re-record the baseline at the new scale. 

Captures are the window's visible frame, so an opaque app's pixels don't depend on what's behind it. 

These settings reduce noise on the machine that records and runs the checks:

- **Transparency effects:** turn them off (Settings → Personalization → Colors → Transparency effects). Windows 11 apps with Mica or acrylic surfaces blend in the desktop behind them, and anything moving there (a video, an animated wallpaper, another window updating) changes your pixels. Also avoid putting a video or other animation behind the window under test.
- **Animation effects:** turn them off (Settings → Accessibility → Visual effects → Animation effects) so menus and flyouts appear without fading, and settle faster.
- **Text cursor blink:** a blinking caret changes pixels inside an editor. Turn the blink rate to its lowest setting (Control Panel → Keyboard → Cursor blink rate) or scope the baseline to an element that doesn't contain the caret.
- **One display scale, theme and font smoothing (ClearType) for recording and checking.** Text rasterisation differs between them, and a baseline recorded on one machine may need a looser `maxDifferenceRatio` on another.

## Testing Your Own App:

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

`AutoMancer.Testing.XUnit` builds on that for xUnit. It provides an fixture for launch/teardown, plus a base class that exposes `App` and `Expect` to your test classes. When `AutoMancerTestOptions.CaptureScreenshotsOnFailure` is on, a failed validation automatically captures a screenshot and folds the path into the assertion message.

See **[TESTING.md](TESTING.md)** for the full guide, including the fixture pattern, and [`samples/ConsumerNotepadTests`](samples/ConsumerNotepadTests) for a complete working test project built entirely on public API. [`samples/ConsumerVsCodeTests`](samples/ConsumerVsCodeTests) is a second one against a more complex, actively-changing Electron app — a useful second data point for what driving a real app beyond a simple native control actually takes.

### Resource Diagnostics:

Check whether an operation leaks GDI/USER objects, kernel handles, threads or memory in the app under test:

```csharp
// One call: readings before and after the work, as deltas.
var cost = await ResourceMonitor.MeasureAsync(app, async () => await app.TypeAsync(document, "hello"));
Assert.True(cost.HandleDelta <= 5, cost.ToString());   // ToString prints a before/after/delta table

// Or take readings at checkpoints you choose and compare any two.
var start = ResourceMonitor.Capture(app);
await OpenReport();
var open = ResourceMonitor.Capture(app);
await CloseReport();
var closed = ResourceMonitor.Capture(app);
var leaked = start.DiffTo(closed);                       // earlier.DiffTo(later)
```

A few things to know when reading the numbers:
- Each reading covers GDI objects, USER objects, kernel handles, threads, working set and private bytes. Handle counts and memory drift a little on their own, so compare against a small bound rather than zero.
- Run the flow once before you start measuring, so one-time setup (UI Automation, COM, thread pool) isn't counted as growth.

See [`samples/ConsumerNotepadTests/ResourceMeasureDemoTests.cs`](samples/ConsumerNotepadTests/ResourceMeasureDemoTests.cs) for working examples against a live Notepad.

## Logging:

`AppOptions.Logger` takes an `IEngineLogger` that traces element resolution and action execution. By default, every `App` logs out of the box with no setup: it's a logger writing JSON lines to stderr, so it never mixes into a CLI command's stdout. Pass `Logger = null` to turn it off, or your own logger to redirect it:

```csharp
var logger = new EngineLogger(new StreamWriter("automancer.log") { AutoFlush = true }, LogLevel.Debug);
await using var app = await App.LaunchAsync("notepad.exe", new AppOptions { Logger = logger });
```

`IEngineLogger` is a small interface of a `LogLevel`, a message, and optional structured data. `EngineLogger` is just the default implementation. It writes JSON lines to a text writer, and filters output by a minimum level.

If you're embedding AutoMancer in a larger app, it's usually easier to implement `IEngineLogger` directly against your own logging stack (`ILogger`, Serilog, xUnit's `ITestOutputHelper`, whatever you've got) than to adapt the existing text writer.

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

## License:

Apache License 2.0 — see [LICENSE](LICENSE).
