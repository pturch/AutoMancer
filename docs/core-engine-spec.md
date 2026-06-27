# AutoMancer Core Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking..
>
> **NEVER run git commands (add, commit, push) automatically.** All version control is the developer's responsibility. Bash blocks in this document are implementation reference — execute the build/test lines only, never the git lines.

**Phase:** 1 of 2 — this plan is the complete C# proof of concept. The daemon (Phase 2) adds an HTTP layer on top of this library without modifying it.

**Goal:** Build the `AutoMancer.Engine` class library and `AutoMancer.Cli` console harness — the Windows automation core with UIA3/UIA2/Win32 providers, element resolver, retry loop, actions, and DPI normalization.

**Architecture:** `AutoMancer.Engine` is a pure class library with no HTTP/RPC. Providers implement `IElementProvider` (find-only). Separately, `IElementOperator` is an optional contract for providers that can interact with elements via native UIA patterns (InvokePattern, ValuePattern). UIA3 and UIA2 providers each ship a paired operator class; Win32 has none (SendInput covers it universally). `ElementResolver` runs the fallback chain with retry until implicit wait expires. Action classes (Click, Type, Clear, Scroll, Window) check `ElementHandle.Operator` first for native dispatch before falling back to SendInput. `AutoMancer.Cli` wraps the engine for interactive developer testing.

**API stability note:** `ElementHandle` must remain opaque — only `Id` and `NativeHandle` exposed publicly. The Phase 2 daemon stores handles in an element registry keyed by `Id` between stateless HTTP requests; leaking implementation details here creates cleanup work later.

**WinAppDriver lessons incorporated:** WinAppDriver (Microsoft, abandoned 2020) uses a single UIAutomation provider with no fallback chain and ships source-closed. Three concrete gaps it left open that AutoMancer fills: (1) `RuntimeId` locator strategy — re-finds a specific element by its UIA RuntimeId, the UIA equivalent of a CSS `:id` selector; (2) `automancer:xpath` — XPath evaluated against the UIA element tree, not rejected like browser XPath; (3) window management actions (resize, move, maximize) missing from WinAppDriver entirely. Our `KEYEVENTF_UNICODE` typing approach also fixes WinAppDriver's known keyboard layout bug (QWERTY-only regardless of system layout).

**Tech Stack:** C# latest / .NET 10 Windows (`net10.0-windows10.0.22621.0`), `Interop.UIAutomationClient` (UIA3 COM), `System.Windows.Automation` (UIA2 managed), `System.CommandLine` 2.0.0-beta4

**Test Stack:** xunit 2.8, Moq 4.20, `Microsoft.NET.Test.Sdk` 17.9.0 — all tests are C# in `tests/AutoMancer.Engine.Tests/`; run with `dotnet test`

---

## File Map

```
AutoMancer/
├── AutoMancer.slnx
├── src/
│   ├── AutoMancer.Engine/
│   │   ├── AutoMancer.Engine.csproj
│   │   ├── Core/
│   │   │   ├── LocatorStrategy.cs        enum: Name, AutomationId, ClassName, ControlType, RuntimeId, AutoMancerPath, AutomancerXPath
│   │   │   ├── Locator.cs                value object + factory methods (incl. ByRuntimeId, ByXPath)
│   │   │   ├── ElementHandle.cs          resolved element + Rect struct; holds Provider and Operator backrefs (internal)
│   │   │   ├── ElementProviderOptions.cs timeouts, chain, DPI flag
│   │   │   ├── AppSession.cs             connection to a running app
│   │   │   ├── IElementProvider.cs       find-only provider contract + ElementSnapshot
│   │   │   ├── IElementOperator.cs       optional native-interact contract (TryClickAsync, TrySetValueAsync)
│   │   │   ├── ElementResolver.cs        fallback chain orchestrator
│   │   │   ├── AutoMancerPathParser.cs   tree-path syntax parser
│   │   │   └── XPathEvaluator.cs         UIA tree snapshot → XML → XPath → element IDs
│   │   ├── Providers/
│   │   │   ├── NativeMethods.cs          all P/Invoke declarations
│   │   │   ├── Uia3Provider.cs           UIAutomation3 via COM (find only; injects Uia3Operator)
│   │   │   ├── Uia2Provider.cs           System.Windows.Automation fallback (find only; injects Uia2Operator)
│   │   │   └── Win32Provider.cs          EnumChildWindows last resort (find only; no operator — SendInput covers it)
│   │   ├── Operators/
│   │   │   ├── Uia3Operator.cs           IElementOperator via COM InvokePattern / ValuePattern
│   │   │   └── Uia2Operator.cs           IElementOperator via managed InvokePattern / ValuePattern
│   │   ├── Actions/
│   │   │   ├── ClickAction.cs            InvokePattern → SendInput
│   │   │   ├── TypeAction.cs             ValuePattern → SendInput (Unicode, layout-agnostic)
│   │   │   ├── ClearAction.cs            ValuePattern → Ctrl+A Delete
│   │   │   ├── ScrollAction.cs           ScrollItemPattern
│   │   │   └── WindowAction.cs           resize, move, maximize via WindowPattern / SetWindowPos
│   │   ├── Dpi/
│   │   │   └── DpiHelper.cs              logical ↔ physical coordinate conversion
│   │   ├── Diagnostics/
│   │   │   ├── ClosestMatchFinder.cs     Levenshtein-based error hints
│   │   │   └── EngineLogger.cs           JSON-line structured logging
│   │   └── Errors/
│   │       ├── ElementNotFoundError.cs
│   │       ├── ElementNotInteractableError.cs
│   │       └── AppLaunchError.cs
│   └── AutoMancer.Cli/
│       ├── AutoMancer.Cli.csproj
│       ├── Program.cs
│       ├── SessionStore.cs               %TEMP%\automancer-sessions.json
│       └── Commands/
│           ├── LaunchCommand.cs
│           ├── FindCommand.cs
│           ├── ClickCommand.cs
│           ├── TypeCommand.cs
│           └── TreeCommand.cs
└── tests/
    └── AutoMancer.Engine.Tests/
        ├── AutoMancer.Engine.Tests.csproj          xunit 2.8 + Moq 4.20
        ├── Resolver/ElementResolverTests.cs         unit — mocked provider chain
        ├── Diagnostics/ClosestMatchFinderTests.cs   unit — Levenshtein scoring
        ├── Diagnostics/EngineLoggerTests.cs         unit — JSON log shape
        ├── Dpi/DpiHelperTests.cs                    unit — DPI conversion theory
        ├── Core/AutoMancerPathParserTests.cs        unit — path segment parsing
        ├── Core/XPathEvaluatorTests.cs              unit — XPath against snapshot tree
        └── Integration/
│               ├── NotepadTestCollection.cs         [CollectionDefinition("Notepad")] — disables parallelism (WinUI3 is single-instance)
│               ├── NotepadIntegrationTests.cs       [Collection("Notepad")] — launch, find, type, click
│               └── NotepadWorkflowTests.cs          [Collection("Notepad")] — full chain, locators, snapshot, error handling
```

---

### Task 1: Solution and project scaffold

**What:** Creates the three .NET projects and wires them into a solution. Sets `net10.0-windows10.0.22621.0`, nullable reference types enabled, and adds the required NuGet references.

**Creates:**
- `AutoMancer.slnx`
- `src/AutoMancer.Engine/AutoMancer.Engine.csproj` — `Interop.UIAutomationClient` (Note: do NOT add `Microsoft.Windows.SDK.Contracts` — it is incompatible with .NET 5+; WinRT APIs are provided by the `windows10.x` TFM)
- `src/AutoMancer.Cli/AutoMancer.Cli.csproj` — `System.CommandLine` + Engine project reference
- `tests/AutoMancer.Engine.Tests/AutoMancer.Engine.Tests.csproj` — xunit + Moq + Engine project reference

- [ ] **Scaffold and configure**

```bash
dotnet new sln -n AutoMancer
dotnet new classlib -n AutoMancer.Engine -o src/AutoMancer.Engine
dotnet new console -n AutoMancer.Cli -o src/AutoMancer.Cli
dotnet new xunit -n AutoMancer.Engine.Tests -o tests/AutoMancer.Engine.Tests
dotnet sln add src/AutoMancer.Engine/AutoMancer.Engine.csproj
dotnet sln add src/AutoMancer.Cli/AutoMancer.Cli.csproj
dotnet sln add tests/AutoMancer.Engine.Tests/AutoMancer.Engine.Tests.csproj
del src\AutoMancer.Engine\Class1.cs
del tests\AutoMancer.Engine.Tests\UnitTest1.cs
# Edit each .csproj to set TargetFramework to net10.0-windows10.0.22621.0 and LangVersion to preview
dotnet build AutoMancer.slnx
```

**Done when:** `dotnet build AutoMancer.slnx` exits 0.

---

### Task 2: Core types

**What:** The fundamental value objects the entire engine is built around. Locators describe what to find; handles represent what was found; options configure resolver behavior.

**Creates:**
- `src/AutoMancer.Engine/Core/LocatorStrategy.cs` — enum: Name, AutomationId, ClassName, ControlType, AutoMancerPath
- `src/AutoMancer.Engine/Core/Locator.cs` — sealed record with `ByName`, `ByAutomationId`, `ByControlType`, `ByClassName`, `ByPath` factory methods
- `src/AutoMancer.Engine/Core/ElementHandle.cs` — opaque `Id`, `ResolvedVia`, `NativeHandle`, metadata properties, and the `Rect` struct
- `src/AutoMancer.Engine/Core/ElementProviderOptions.cs` — `ProviderChain`, `ImplicitWaitMs`, `PollIntervalMs`, `DpiNormalize`; static `Default`


- [ ] **Implement and verify**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 3: IElementProvider and AppSession

**What:** `IElementProvider` is the contract every automation backend must implement — find one, find all, snapshot tree. `AppSession` wraps a running Windows process; it handles the launch-and-wait-for-window loop and attach-by-PID/title variants.

**Creates:**
- `src/AutoMancer.Engine/Core/IElementProvider.cs` — interface + `ElementSnapshot` record
- `src/AutoMancer.Engine/Core/AppSession.cs` — `LaunchAsync`, `AttachByPidAsync`, `AttachByTitleAsync`, `KillApp`, `IAsyncDisposable`


- [ ] **Implement and verify**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 4: EngineLogger and error types

**What:** `EngineLogger` writes structured JSON lines to any `TextWriter`. Error types carry machine-readable context (`ElementNotFoundError` attaches the locator, attempted providers, elapsed ms, and closest match) so the daemon and SDKs can format actionable messages.

**Creates:**
- `src/AutoMancer.Engine/Diagnostics/EngineLogger.cs` — `Info/Warn/Error/Debug` → JSON line via `System.Text.Json`; injectable `TextWriter` for testing
- `src/AutoMancer.Engine/Errors/AppLaunchError.cs`
- `src/AutoMancer.Engine/Errors/ElementNotInteractableError.cs`
- `src/AutoMancer.Engine/Errors/ElementNotFoundError.cs` — `ClosestMatch?`, `BuildMessage`
- `tests/AutoMancer.Engine.Tests/Diagnostics/EngineLoggerTests.cs` — verifies JSON shape and level filtering


- [ ] **Write test, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "EngineLoggerTests"
```

**Done when:** 2 `EngineLoggerTests` pass.

---

### Task 5: ClosestMatchFinder

**What:** When an element isn't found, the resolver snapshots visible elements and finds the closest name match using Levenshtein distance — minimum confidence 0.70. This powers the "Did you mean?" hint in error messages.

**Creates:**
- `src/AutoMancer.Engine/Diagnostics/ClosestMatchFinder.cs` — `Find(locator, tree)` → `ClosestMatch?`; full `LevenshteinDistance` DP; `ClosestMatch` record
- `tests/AutoMancer.Engine.Tests/Diagnostics/ClosestMatchFinderTests.cs` — exact match, typo, threshold rejection, empty tree, case insensitivity


- [ ] **Write test, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "ClosestMatchFinderTests"
```

**Done when:** 5 tests pass.

---

### Task 6: DpiHelper

**What:** Converts logical pixel coordinates (relative to the app window at 96 DPI) to physical screen coordinates for `SendInput`. Has testable overloads that accept pre-fetched values (no Win32 calls) and production overloads that read from `GetDpiForWindow`/`GetWindowRect`.

**Creates:**
- `src/AutoMancer.Engine/Dpi/DpiHelper.cs` — `LogicalToPhysical` and `PhysicalToLogical`, both testable and production variants
- `tests/AutoMancer.Engine.Tests/Dpi/DpiHelperTests.cs` — theory at 96/120/144 DPI with window offset; round-trip inversion test


- [ ] **Write test, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "DpiHelperTests"
```

**Done when:** 4 tests pass.

---

### Task 7: NativeMethods P/Invoke declarations

**What:** Centralizes every Win32 API call used across providers and actions. `EnumChildWindows`, `GetWindowText`, `GetClassName`, `SendInput`, `SetForegroundWindow`, and all associated structs and constants.

**Creates:**
- `src/AutoMancer.Engine/Providers/NativeMethods.cs` — all P/Invoke signatures, `INPUT`/`MOUSEINPUT`/`KEYBDINPUT` structs, mouse/keyboard constants


- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 8: Uia3Provider and Uia3Operator

**What:** First and most capable provider. Uses the UIAutomation3 COM interface (`CUIAutomation8` via `Interop.UIAutomationClient`) to build property conditions and find elements. Returns `null` on not-found — never throws. The matching operator handles InvokePattern and ValuePattern dispatch; it is a stateless singleton stored as a `private static readonly` field in the provider and injected into every `ElementHandle` at wrap time.

**Creates:**
- `src/AutoMancer.Engine/Providers/Uia3Provider.cs` — `FindElementAsync`, `FindElementsAsync`, `SnapshotTreeAsync`; `ControlTypeMap`; `BuildCondition`; `WalkTree` up to 20 levels; injects `Uia3Operator` singleton into each `ElementHandle` via the `Operator` backref
- `src/AutoMancer.Engine/Operators/Uia3Operator.cs` — `IElementOperator` implementation; `TryClickAsync` via `InvokePattern`; `TrySetValueAsync` via `ValuePattern`; returns `false` when the pattern is not exposed


- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 9: ElementResolver

**What:** The engine's core retry loop. Iterates the provider chain every `PollIntervalMs` until a match is found or `ImplicitWaitMs` elapses. On timeout, snapshots the tree and throws `ElementNotFoundError` with a closest-match hint.

**Creates:**
- `src/AutoMancer.Engine/Core/ElementResolver.cs` — `FindAsync` (retry loop), `FindAllAsync` (single pass), `TrySnapshotAsync`
- `tests/AutoMancer.Engine.Tests/Resolver/ElementResolverTests.cs` — first provider succeeds; all fail → exception; chain falls through to second provider


- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "ElementResolverTests"
```

**Done when:** 3 tests pass.

---

### Task 10: Integration tests — Notepad launch and find

**What:** First end-to-end validation. Proves that `AppSession` + `Uia3Provider` + `ElementResolver` work together against a real Windows app. Also verifies that a typo in the element name produces an error that includes the closest match.

**WinUI3 single-instance constraint:** Windows 11 Notepad (WinUI3) is single-instance — a second `notepad.exe` launch opens a new tab in the existing window rather than starting a fresh process. This means test classes that each launch Notepad must run **sequentially**, not in parallel. Enforce this with an xUnit `[CollectionDefinition]` on a marker class and `[Collection("Notepad")]` on every integration test class. Each test's `DisposeAsync` must also `await Task.Delay(800)` after `KillApp()` so the process fully exits before the next test's `LaunchAsync` runs.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Integration/NotepadTestCollection.cs` — `[CollectionDefinition("Notepad", DisableParallelization = true)]` marker
- `tests/AutoMancer.Engine.Tests/Integration/NotepadIntegrationTests.cs` — `[Collection("Notepad")]`; launch+find Document control via UIA3, attach-by-title, typo → closest match, type text, click File menu


- [ ] **Run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"
```

**Done when:** 5 tests pass on a Windows machine with Notepad.

---

### Task 11: ClickAction and TypeAction

**What:** The two primary interaction actions. Each action checks `element.Operator` first — if non-null, it delegates to the operator's native pattern (InvokePattern for click, ValuePattern for type). If the operator returns `false` (pattern not supported) or is `null` (Win32/Visual elements), the action falls back to synthesized `SendInput`. Both use `DpiHelper` when `DpiNormalize` is enabled.

**Creates:**
- `src/AutoMancer.Engine/Actions/ClickAction.cs` — `ClickType` enum; `element.Operator?.TryClickAsync` → SendInput mouse fallback; `GetCenter` via `BoundingRect`
- `src/AutoMancer.Engine/Actions/TypeAction.cs` — `element.Operator?.TrySetValueAsync` → Unicode `SendInput` fallback (`KEYEVENTF_UNICODE`, never VK codes)

Extends `NativeMethods.cs` with `GetSystemMetrics`.
Extends `NotepadIntegrationTests.cs` with `TypeInEditor_TextAppears` and `ClickFileMenu_OpensMenu`.


- [ ] **Implement and run integration tests**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"
```

**Done when:** 5 integration tests pass.

---

### Task 12: Uia2Provider and Uia2Operator

**What:** Fallback using the older managed `System.Windows.Automation` API. Same strategy support as Uia3Provider, all calls wrapped in `Task.Run` because the managed UIA2 API is synchronous. Requires `<UseWPF>true</UseWPF>` in the engine `.csproj` to bring in the Windows Desktop Runtime that ships `UIAutomationClient.dll`. The matching operator handles InvokePattern and ValuePattern the same way as the UIA3 operator, wrapping `InvalidOperationException` (UIA2 throws when a pattern isn't supported, unlike UIA3 which returns null).

**Creates:**
- `src/AutoMancer.Engine/Providers/Uia2Provider.cs` — same `IElementProvider` interface; uses `PropertyCondition`, `TreeWalker.ControlViewWalker`; `ControlTypeNames` reverse map; injects `Uia2Operator` singleton into each `ElementHandle` via the `Operator` backref
- `src/AutoMancer.Engine/Operators/Uia2Operator.cs` — `IElementOperator` implementation; same `TryClickAsync`/`TrySetValueAsync` contract; catches `InvalidOperationException` instead of checking for null


- [ ] **Implement and verify**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"
```

**Done when:** Build succeeds; integration tests still pass.

---

### Task 13: Win32Provider

**What:** Last-resort provider using only `EnumChildWindows` P/Invoke. Works on apps with no accessibility tree at all. Matches by window title text (Name strategy, substring match) or class name (exact match). Returns the HWND wrapped in `ElementHandle`. No operator class — Win32 has no native automation patterns equivalent to InvokePattern/ValuePattern, and `SendInput` is more universal for modern apps. A `Win32Operator` will be added in Stage 6.1 for window management actions (`SetWindowPos`, `ShowWindow`).

**Creates:**
- `src/AutoMancer.Engine/Providers/Win32Provider.cs` — `EnumChildWindows` callback; `Matches`; `GetTitle`/`GetClass` helpers; `Operator = null` in wrapped handles (actions fall through to SendInput)


- [ ] **Implement and run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"
```

**Done when:** All integration tests pass; Win32 snapshot returns child HWNDs with class names.

---

### Task 13b: NotepadWorkflowTests — comprehensive integration coverage

**What:** A second integration test class that exercises the full three-provider chain, multiple locator strategies, snapshot behavior, and error handling in a single suite. Demonstrates what a real automation script looks like.

**Key patterns established here:**
- UIA3 warmup in `InitializeAsync`: use a UIA3-only resolver (with retry) to wait for the element tree to be ready before running tests. Without this, UIA3 sometimes returns null on its first COM query during WinUI3 initialization, causing UIA2 to intercept in the chain and making provider-order assertions fail.
- Snapshot-discovered Win32 tests: rather than hardcoding WinUI3 internal class/title names (which vary across Windows builds), the tests call `SnapshotTreeAsync` first and use whatever child names/classes are actually present.
- Inconclusive-safe tests: when a snapshot returns no named/classed children (valid on some Notepad builds), the test returns early rather than asserting on nothing.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Integration/NotepadWorkflowTests.cs` — `[Collection("Notepad")]`; full workflow (type + menu + re-attach), provider chain assertions, Win32 locators, UIA3/Win32 snapshots, error hint tests (15 tests total)


- [ ] **Run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"
```

**Done when:** All 15 integration tests pass (5 from `NotepadIntegrationTests` + 10 from `NotepadWorkflowTests`).

---

### Task 14: ClearAction and ScrollAction

**What:** Supporting interaction actions. `ClearAction` uses `ValuePattern.SetValue("")`, falling back to Ctrl+A + Delete. `ScrollAction` uses `ScrollItemPattern.ScrollIntoView()`.

**Creates:**
- `src/AutoMancer.Engine/Actions/ClearAction.cs`
- `src/AutoMancer.Engine/Actions/ScrollAction.cs`


- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 15: AutoMancerPathParser

**What:** Parses tree-traversal path strings like `"Window > Pane[2] > Button[\"Submit\"]"` into typed `PathSegment` records. Supports control type, quoted name, 0-based index, and wildcard `*`. Provider wiring (segment-by-segment tree traversal) is a separate concern handled in Stage 6; this task produces only the parser.

**Creates:**
- `src/AutoMancer.Engine/Core/AutoMancerPathParser.cs` — `Parse(string)` → `IReadOnlyList<PathSegment>`; regex-based
- `tests/AutoMancer.Engine.Tests/Core/AutoMancerPathParserTests.cs` — simple type, type+name, type+index, multi-segment, wildcard


- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "AutoMancerPathParserTests"
```

**Done when:** 5 tests pass.

---

### Task 16: CLI harness

**What:** Developer-facing command-line tool (`automancer.exe`). Sessions are persisted to `%TEMP%\automancer-sessions.json` so you can run `launch` in one shell and `find` or `tree` referencing the same session in the next.

**Creates:**
- `src/AutoMancer.Cli/Program.cs` — root command, registers sub-commands
- `src/AutoMancer.Cli/SessionStore.cs` — JSON-backed session registry in temp dir
- `src/AutoMancer.Cli/Commands/LaunchCommand.cs` — prints session ID and PID
- `src/AutoMancer.Cli/Commands/FindCommand.cs` — prints element properties; `ParseLocator` helper shared by other commands
- `src/AutoMancer.Cli/Commands/ClickCommand.cs` — supports `--double` and `--right`
- `src/AutoMancer.Cli/Commands/TypeCommand.cs`
- `src/AutoMancer.Cli/Commands/TreeCommand.cs` — snapshots and prints flat element list


- [ ] **Implement, build, and smoke test**

```bash
dotnet build src/AutoMancer.Cli/AutoMancer.Cli.csproj
dotnet run --project src/AutoMancer.Cli -- launch notepad.exe
# use the printed session ID in subsequent commands:
dotnet run --project src/AutoMancer.Cli -- find <session-id> --by control --value Edit
dotnet run --project src/AutoMancer.Cli -- tree <session-id>
```

**Done when:** All CLI commands work against a live Notepad session.

---

### Task 17: RuntimeId locator strategy

**What:** `RuntimeId` is UIA's opaque per-session element identifier (e.g. `"42.333896.3.1"`). WinAppDriver supports finding by this value as the `id` strategy. It lets automation code re-find a specific element it has already seen — useful for asserting that the same element is still present after an action. Maps to `UIA_RuntimeIdPropertyId` in the UIA3 COM API.

**Modifies:**
- `src/AutoMancer.Engine/Core/LocatorStrategy.cs` — add `RuntimeId` to enum
- `src/AutoMancer.Engine/Core/Locator.cs` — add `ByRuntimeId(string id)` factory
- `src/AutoMancer.Engine/Providers/Uia3Provider.cs` — add `RuntimeId` case in `BuildCondition` mapping to `UIA_RuntimeIdPropertyId`
- `src/AutoMancer.Engine/Providers/Uia2Provider.cs` — add `RuntimeId` case mapping to `AutomationElement.RuntimeIdProperty`


- [ ] **Implement and run integration test**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
# Integration smoke: launch Notepad, find an element, store its runtime ID, re-find by runtime ID
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"
```

**Done when:** Engine builds; a previously-found element can be re-found by RuntimeId.

---

### Task 18: automancer:xpath locator strategy

**What:** WinAppDriver supports XPath evaluated against the UIA accessibility tree. This is not browser XPath — it's XPath run against an XML representation of the UIA element tree, where element tags are control type names and attributes are UIA properties. It's slower than property-condition strategies (requires a full tree snapshot) but more expressive for complex positional queries like `//Pane[2]/Button[0]` or `//Button[@Name='OK' and @AutomationId='btn_ok']`.

**Implementation approach:** `XPathEvaluator` takes an `IReadOnlyList<ElementSnapshot>` tree, builds an in-memory `XDocument`, runs the XPath expression via LINQ to XML, and returns the matching element indices. The provider maps indices back to live `IUIAutomationElement`s via a parallel walk.

**Creates:**
- `src/AutoMancer.Engine/Core/XPathEvaluator.cs` — `Evaluate(xpath, snapshots)` → `IReadOnlyList<int>` (indices into the snapshot list); uses `System.Xml.Linq`
- `src/AutoMancer.Engine/Core/LocatorStrategy.cs` — add `AutomancerXPath`
- `src/AutoMancer.Engine/Core/Locator.cs` — add `ByXPath(string xpath)` factory

**Modifies:**
- `src/AutoMancer.Engine/Providers/Uia3Provider.cs` — add `AutomancerXPath` path: snapshot tree, evaluate XPath, return element at matched index
- `tests/AutoMancer.Engine.Tests/Core/XPathEvaluatorTests.cs` — XPath on a known snapshot tree: `//Button`, `//Button[0]`, `//Pane/Button[@Name='OK']`


- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "XPathEvaluatorTests"
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"
```

**Done when:** `Locator.ByXPath("//MenuItem[@Name='File']")` finds the File menu in Notepad. (File is a `MenuItem` in WinUI3 Notepad, not a `Button`.)

---

### Task 19: WindowAction — resize, move, maximize

**What:** WinAppDriver supports window size/position/maximize endpoints that AutoMancer is missing. `WindowAction` wraps `WindowPattern.SetTransformProperties` (move + resize) and `WindowPattern.SetWindowVisualState` (maximize/minimize/normal). Falls back to `SetWindowPos` P/Invoke for apps that don't expose `WindowPattern`.

**Creates:**
- `src/AutoMancer.Engine/Actions/WindowAction.cs` — `MoveAsync`, `ResizeAsync`, `MaximizeAsync`, `RestoreAsync`; `WindowPattern` → `SetWindowPos` P/Invoke fallback

**Modifies:**
- `src/AutoMancer.Engine/Providers/NativeMethods.cs` — add `SetWindowPos` P/Invoke and `SWP_*` constants


- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors; manual CLI test can resize and maximize Notepad.

---

### Task 20: Full test pass

**What:** Final verification. Unit tests run without a real app. Integration tests run against Notepad. Full solution builds clean.

- [ ] **Run all tests and build**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration" -v
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration" -v
dotnet build AutoMancer.slnx
```

**Done when:** All tests green, `dotnet build AutoMancer.slnx` reports `0 Error(s)`.

---

## Constraints

- Every `.cs` file starts with `// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.`
- `net10.0-windows10.0.22621.0` only — no cross-platform guards
- `System.Text.Json` throughout — no Newtonsoft.Json
- No DI container — providers wired manually at call sites
- Providers return `null`/empty on not-found; only `ElementResolver` throws `ElementNotFoundError`
- All public API is `async Task<T>`; synchronous Win32/UIA2 calls wrapped in `Task.Run`
- `TypeAction` must use `KEYEVENTF_UNICODE` with `wScan` set to the character codepoint — never use VK codes for printable characters; this is what fixes WinAppDriver's known QWERTY-only keyboard layout bug
- `automancer:xpath` is an AutoMancer extension strategy, not standard WebDriver XPath; it targets the UIA element tree, not a browser DOM — document this distinction clearly
- `ElementHandle` must stay opaque — only `Id` (string) and `NativeHandle` (object) are public. No other internal fields exposed. This is a Phase 1 → Phase 2 contract: the daemon's element registry depends on being able to store and retrieve handles purely by `Id` without knowing their internals
