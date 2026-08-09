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
