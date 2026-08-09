# AutoMancer — Master Implementation Roadmap

> This is the sequencing document. It organizes work from the three detailed plans into stages and batches that can be picked up and executed independently. Each batch has a concrete deliverable, a time estimate, and a clear "done" signal.
>
> **Numbering:** Stages are `Phase.Stage` (e.g. `2.3` is the 3rd stage of Phase 2) and batches are `Phase.Stage.Batch` (e.g. `2.3.4`). Numbering restarts at 1 within each phase specifically so that adding a stage to an earlier phase never renumbers a later phase — only append.
>
> **NEVER run git commands (add, commit, push) automatically.** All version control is the developer's responsibility.
>
> **Tech stack:** C# latest / .NET 10 (`net10.0-windows10.0.22621.0`). The .NET 10 SDK creates `.slnx` solution files instead of `.sln` — use `AutoMancer.slnx` everywhere.
>
> **Detailed specs:** [Core Engine](./core-engine-spec.md) · [Daemon](./daemon-spec.md) · [SDKs](./sdks-spec.md)

---

## Delivery Strategy

This project ships in three distinct phases. **Phase 1** is a self-contained C# proof of concept that grows into the complete interaction surface and a test adapter layer — engine + CLI + `AutoMancer.Testing`. **Phase 2** deepens that surface — richer locators, wait primitives, native fallbacks, and diagnostics — all additive to what Phase 1 built. **Phase 3** adds the daemon and polyglot SDKs on top of that surface without modifying it.

```
╔══════════════════════════════════════════════════════════╗
║  PHASE 1 — C# Engine  (Stages 1.1–1.9, ~21.5–22 hours)     ║
║                                                          ║
║   Stage 1.1: Core Types                                  ║
║       │                                                  ║
║   Stage 1.2: UIA3 Provider + Resolver ──► first find     ║
║       │                                                  ║
║   Stage 1.3: Actions + DPI ───────────► first click/type ║
║       │                                                  ║
║   Stage 1.4: Full Provider Chain ─────► UIA2, Win32      ║
║       │                                                  ║
║   Stage 1.5: Extended Locators ───────► RuntimeId, XPath ║
║       │                                                  ║
║   Stage 1.6: Window Management + CLI ─► PoC complete ✓   ║
║       │                                                  ║
║   Stage 1.7: Extended Interactions ───► hover, hotkeys,  ║
║       │                                 double-click,drag║
║       │                                                  ║
║   Stage 1.8: Screenshot + Wait + App ─► full C# API ✓    ║
║       │                                 engine surface set║
║       │                                                  ║
║   Stage 1.9: Test Adapter Layer ──────► Expect() +fixtures║
╚══════════════════════════════════════════════════════════╝
                        │
                        │  Core surface done. Deepen coverage.
                        ▼
╔══════════════════════════════════════════════════════════╗
║  PHASE 2 — Extended Coverage  (Stages 2.1–2.7, ~22–29 hrs)║
║                                                          ║
║   Stage 2.1: Locator Power Tools ────► spatial, ByProperty║
║       │                                                  ║
║   Stage 2.2: Wait & Resilience ──────► canned conditions,║
║       │                                 stale re-resolve ║
║       │                                                  ║
║   Stage 2.3: Context Menu Fallback ──► native HMENU click║
║       │                                                  ║
║   Stage 2.4: Diagnostics & Env ──────► handle/mem watch, ║
║       │                                 multi-monitor DPI║
║       │                                                  ║
║   Stage 2.5: Accessibility Audit ────► unnamed-element   ║
║       │                                 report            ║
║       │                                                  ║
║   Stage 2.6: Visual Provider ────────► OCR + template +   ║
║       │                                 visual regression ║
║       │                                                  ║
║   Stage 2.7: Engine Hardening ───────► DPI matrix,        ║
║                                          Win10/11, examples✓║
╚══════════════════════════════════════════════════════════╝
                        │
                        │  Daemon added as a new project —
                        │  no engine changes required.
                        ▼
╔══════════════════════════════════════════════════════════╗
║  PHASE 3 — Polyglot HTTP Layer  (Stages 3.1–3.6)         ║
║                                                          ║
║   Stage 3.1: Daemon Foundation ──────► curl finds elements║
║       │                                                  ║
║   Stage 3.2: Daemon Interactions ────► full HTTP surface ║
║       │                                                  ║
║   ├──► Stage 3.3: Python SDK ────────► pip install works ║
║   ├──► Stage 3.4: TypeScript SDK ────► npm install works ║
║       │                                                  ║
║   Stage 3.5: CI Session ─────────────► headless CI works ║
║       │                                                  ║
║   Stage 3.6: Polish ─────────────────► v0.1 shippable ✓  ║
╚══════════════════════════════════════════════════════════╝
```

### Why this split works

`AutoMancer.Engine` is a pure class library with no HTTP awareness. C# consumers in Phases 1 and 2 call the engine API directly — no port, no process, no wire protocol. When Phase 3 adds the daemon, it wraps whatever the engine surface looks like at that point; the daemon is purely additive.

Phase 1 covers the full arc from first find to a complete interaction surface and a test adapter layer — proof of concept through "a C# consumer has everything they need" — in one phase, so there's no artificial seam between "does it work" and "is it complete." Stage 1.9 (test adapter layer) is sequenced at the end of Phase 1 because it's the natural next thing a C# consumer reaches for once the interaction surface is complete, and it only adds new projects (`AutoMancer.Testing`, `AutoMancer.Testing.XUnit`) on top of the `App`/`Locator` surface.

Phase 2 deepens that surface — richer locators, canned wait conditions, opt-in resilience, a native context-menu fallback, some Windows-native diagnostics with no web/mobile equivalent, and a visual (OCR/template-matching) fallback for apps with no accessibility tree at all. None of it is required for Phase 3's daemon to exist; it's sequenced first because the daemon gets to wrap a deeper surface if it's already there, not because anything here is a hard gate. Every batch in this phase is additive — existing callers and existing tests keep working unchanged whether or not a given batch has landed.

Phase 3 adds the daemon and polyglot SDKs on top of whatever the engine looks like by then. Nothing in Phase 3 modifies engine source files.

The one thing to keep clean throughout Phases 1 and 2: `ElementHandle` must stay opaque (only `Id` and `NativeHandle` exposed). The daemon's element registry depends on this — it stores handles by ID between stateless HTTP requests.

---

## Test Language Summary

All C# code (engine + daemon) is tested in C# with xunit + Moq. SDK client code is tested in the SDK's own language.

| Layer | Test framework | Location |
|---|---|---|
| Engine (core library) | C# / xunit 2.8 / Moq 4.20 | `tests/AutoMancer.Engine.Tests/` |
| Test adapter (`.Testing` / `.Testing.XUnit`) | C# / xunit 2.8 | `tests/AutoMancer.Testing.Tests/` |
| Daemon (HTTP server) | C# / xunit 2.8 / Moq 4.20 | `tests/AutoMancer.Daemon.Tests/` |
| Python SDK | Python / pytest | `sdk/python/tests/` |
| TypeScript SDK | TypeScript / vitest | `sdk/typescript/tests/` |

---

## Phase 1 — C# Engine

### Stage 1.1 — Core Types and Contracts
> **Unlocks:** Everything. This is the type vocabulary the entire codebase shares.
> **Estimated time:** 1–2 hours
> **Done when:** `dotnet build AutoMancer.slnx` passes with 0 errors.

| Batch | Work | Plan ref |
|---|---|---|
| 1.1.1 | Solution scaffold — `AutoMancer.slnx`, three `.csproj` files, NuGet refs, delete stubs | Engine Task 1 |
| 1.1.2 | `LocatorStrategy`, `Locator`, `ElementHandle`, `Rect`, `ElementProviderOptions` | Engine Task 2 |
| 1.1.3 | `IElementProvider` + `ElementSnapshot`; `AppSession` (`LaunchAsync`, `AttachByPid`, `AttachByTitle`) | Engine Task 3 |

**UWP / packaged app launch:** `LaunchAsync` identifies a session by watching the launched PID for a window. UWP and MSIX-packaged apps (Calculator, Windows Terminal, new Paint, etc.) work differently — `calc.exe` is a thin activator that exits immediately; the real window appears under a different PID managed by the Windows app model. `LaunchAsync` will throw `AppLaunchError` for these apps. Workaround until Stage 1.6: use `Process.Start` with `UseShellExecute = true` and then call `AttachByTitleAsync`. Addressed properly in batch 1.6.6.
| 1.1.4 | `EngineLogger` — opt-in structured trace of AutoMancer's own process (off by default, no-op when unset); wired into `ElementResolver`'s retry loops and exposed via `AppOptions.Logger` + error types (`ElementNotFoundError`, `ElementNotInteractableError`, `AppLaunchError`) | Engine Task 4 |
| 1.1.5 | `ClosestMatchFinder` + Levenshtein; `DpiHelper` testable overloads | Engine Tasks 5–6 |

**Commit at end of each batch.** After 1.1.5: run `dotnet test --filter "Category!=Integration"` — all unit tests green.

---

### Stage 1.2 — UIA3 Provider and Element Resolver
> **Unlocks:** Finding elements in any UIA-accessible Windows app.
> **Estimated time:** 2–3 hours
> **Done when:** `automancer find <session> --by control --value Edit` returns an element in a live Notepad window.

| Batch | Work | Plan ref |
|---|---|---|
| 1.2.1 | `NativeMethods.cs` — all P/Invoke declarations | Engine Task 7 |
| 1.2.2 | `Uia3Provider` — Name, AutomationId, ClassName, ControlType strategies; `ControlTypeMap` | Engine Task 8 |
| 1.2.3 | `ElementResolver` — retry loop, fallback chain, `TrySnapshotAsync`, throws `ElementNotFoundError` with closest match | Engine Task 9 |
| 1.2.4 | Integration tests — launch Notepad, find Edit control via UIA3, verify `ResolvedVia == "uia3"`; typo → closest match hint | Engine Task 10 |

**After 1.2.4:** `dotnet test --filter "Category=Integration"` — 3 integration tests green.

---

### Stage 1.3 — Actions and DPI
> **Unlocks:** Actually interacting with elements, not just finding them.
> **Estimated time:** 2 hours
> **Done when:** The integration test types "Hello AutoMancer" into Notepad and opens the File menu.

| Batch | Work | Plan ref |
|---|---|---|
| 1.3.1 | `DpiHelper` production overloads (`GetDpiForWindow`, `GetWindowRect`); integrate into the engine | Engine Task 6 (prod side) |
| 1.3.2 | `ClickAction` — `InvokePattern` first, `SendInput` fallback; `GetCenter` uses `DpiHelper` | Engine Task 11 |
| 1.3.3 | `TypeAction` — `ValuePattern.SetValue` first, Unicode `SendInput` fallback (layout-agnostic via `KEYEVENTF_UNICODE`) | Engine Task 11 |
| 1.3.4 | Integration tests — `TypeInEditor_TextAppears`, `ClickFileMenu_OpensMenu` | Engine Task 11 |

**After 1.3.4:** 5 integration tests green. You can now automate Notepad end-to-end from C#.

---

### Stage 1.4 — Full Provider Fallback Chain
> **Unlocks:** Automating apps with partial or no UIA accessibility trees.
> **Estimated time:** 2–3 hours
> **Done when:** Win32 fallback triggers correctly when UIA finds nothing; all 15 integration tests pass.

| Batch | Work | Plan ref |
|---|---|---|
| 1.4.1 | `IElementOperator` interface (find/operate split); extract `Uia3Operator` from `Uia3Provider`; add `Uia2Provider` + `Uia2Operator`; `UseWPF` in engine csproj | Engine Task 12 |
| 1.4.2 | `Win32Provider` — `EnumChildWindows`, title/class match; no operator (SendInput covers it; `Win32Operator` added in 1.6.1 for window management) | Engine Task 13 |
| 1.4.3 | `ClearAction` — `ValuePattern.SetValue("")` → Ctrl+A Delete; `ScrollAction` — `ScrollItemPattern` | Engine Task 14 |
| 1.4.4 | `NotepadWorkflowTests` — 10-test comprehensive suite covering the full chain, all locator strategies, snapshot, and error hints | Engine Task 13b |

**Architecture note:** Provider and operator responsibilities are separated. `IElementProvider` (find-only) and `IElementOperator` (native interact) are distinct interfaces. Each provider holds a stateless operator singleton in a `private static readonly` field and injects it into `ElementHandle.Operator` at wrap time. `ClickAction` and `TypeAction` check `element.Operator` first; they fall through to `SendInput` when it is `null` (Win32/Visual) or when the pattern returns `false` (pattern not supported).

**WinUI3 test isolation:** Windows 11 Notepad is single-instance. Test classes that each launch Notepad must share a `[Collection("Notepad")]` with `DisableParallelization = true`. Each `DisposeAsync` adds an 800 ms post-kill delay to ensure the process fully exits before the next `LaunchAsync`. A UIA3-only warmup in each `InitializeAsync` ensures the COM element tree is populated before the chain resolver runs.

**After 1.4.4:** 15 integration tests pass; Win32 snapshot and provider-chain order verified.

---

### Stage 1.5 — Extended Locator Strategies
> **Unlocks:** Complex element addressing — positional paths, re-finding by ID, XPath queries.
> **Estimated time:** 2–3 hours
> **Done when:** `RuntimeId` and `AutomancerXPath` find correct elements in Notepad end-to-end. `AutoMancerPath` parser is complete; provider wiring (segment-by-segment tree traversal) is deferred to Stage 1.6.

| Batch | Work | Plan ref |
|---|---|---|
| 1.5.1 | `AutoMancerPathParser` — `"Window > Pane[2] > Button[\"OK\"]"` syntax; unit tests | Engine Task 15 |
| 1.5.2 | `RuntimeId` strategy — add to enum, `Locator.ByRuntimeId`, `UIA_RuntimeIdPropertyId` in both providers | Engine Task 17 |
| 1.5.3 | `XPathEvaluator` — UIA tree snapshot → `XDocument` → XPath → element indices; unit tests | Engine Task 18 |
| 1.5.4 | Wire `AutomancerXPath` into `Uia3Provider.FindElementAsync`; add `CollectElements` helper for index→element mapping | Engine Task 18 |

**After 1.5.4:** `Locator.ByXPath("//MenuItem[@Name='File']")` finds the File menu in Notepad. (File is a `MenuItem` in WinUI3 Notepad, not a `Button`.)

---

### Stage 1.6 — Window Management and CLI Harness
> **Unlocks:** The `automancer` command-line tool is usable for manual testing of any app.
> **Estimated time:** 2 hours
> **Done when:** `automancer launch notepad.exe && automancer tree <id>` produces a readable element tree.
> **Proof-of-concept milestone — Phase 1 continues through Stage 1.9 (interaction depth, wait utilities, and the test adapter layer).**

| Batch | Work | Plan ref |
|---|---|---|
| 1.6.1 | `WindowAction` — `ResizeAsync`, `MoveAsync`, `SetVisualStateAsync` (maximize/minimize/restore), `GetSizeAsync`; `WindowPattern` → `SetWindowPos` fallback | Engine Task 19 |
| 1.6.2 | `SessionStore.cs` — `%TEMP%\automancer-sessions.json` | Engine Task 16 |
| 1.6.3 | CLI commands — `launch`, `find`, `tree` (first three) | Engine Task 16 |
| 1.6.4 | CLI commands — `click`, `type` | Engine Task 16 |
| 1.6.5 | Full unit test pass | Engine Task 20 |
| 1.6.6 | `App.LaunchPackagedAsync(string aumid)` — uses `IApplicationActivationManager.ActivateApplication(aumid)` (COM, no extra package) to activate a UWP/MSIX app by its Application User Model ID; returns the real host PID so the existing window-wait logic works unchanged. Integration test: launch Calculator by AUMID, find the display element. |

**After 1.6.6:** Proof-of-concept milestone reached — the engine library handles basic automation from C# and every unit and integration test is green. Phase 1 continues into extended interactions, wait utilities, and the test adapter layer.

---

### Stage 1.7 — Extended Interactions
> **Unlocks:** The full range of mouse and keyboard interactions a real automation script needs.
> **Estimated time:** 2–2.5 hours
> **Done when:** Integration tests confirm double-click, right-click, hover, hotkey, drag, scroll wheel, Ctrl+click, and set-focus all work against Notepad.

| Batch | Work |
|---|---|
| 1.7.1 | `DoubleClickAction`, `RightClickAction`, `HoverAction` — SendInput mouse event variants; extend `ClickAction` with `modifiers` (Shift/Ctrl/Alt/Win held during click) and `button` (left/middle/right/back/forward) parameters; add `CloseAsync` to `WindowAction` via `WindowPattern` → `WM_CLOSE` fallback |
| 1.7.2 | `KeyboardAction` — `PressKeyAsync(Key)`, `HotkeyAsync(modifiers, key)`, `KeyDownAsync`/`KeyUpAsync`; VK codes for non-printable keys; `KEYEVENTF_KEYUP` for release |
| 1.7.3 | `DragAction` — `DragAsync(from, to)` via SendInput mouse press + move + release |
| 1.7.4 | `ScrollWheelAction` — `ScrollAsync(element, deltaX, deltaY)` via `SendInput` `MOUSEEVENTF_WHEEL` (vertical) / `MOUSEEVENTF_HWHEEL` (horizontal); `SetFocusAction` — `UIAutomationElement.SetFocus` |
| 1.7.5 | Integration tests — double-click selects a word, Ctrl+A selects all text, right-click opens context menu, Ctrl+click, scroll wheel moves caret, SetFocus focuses element |

**After 1.7.5:** All mouse and keyboard interaction types covered.

---

### Stage 1.8 — Screenshot, Wait Utilities, and App Facade
> **Unlocks:** Screenshot capture and reactive wait patterns; the `App` facade exposes everything as a single cohesive C# API.
> **Estimated time:** 2–2.5 hours
> **Done when:** `app.ScreenshotAsync()` returns a valid PNG; `app.WaitUntilGoneAsync(locator)` resolves when an element disappears; `app.WaitForAsync(locator, predicate)` resolves once the predicate is true instead of racing a single find.
> **Planned engine API work finished after this stage.**

| Batch | Work |
|---|---|
| 1.8.1 | `ScreenshotAction` — `CaptureAsync(hwnd)` → `byte[]` PNG via GDI+ `BitBlt`; no external dependencies |
| 1.8.2 | `WaitUntilGoneAsync` in `ElementResolver` — retry until every provider returns null within the implicit wait window |
| 1.8.3 | `App` facade enrichment — add `DoubleClickAsync`, `RightClickAsync`, `HoverAsync`, `HotkeyAsync`, `DragAsync`, `ScrollWheelAsync`, `SetFocusAsync`, `CloseAsync`, `ScreenshotAsync`, `WaitUntilGoneAsync` |
| 1.8.4 | Integration tests — screenshot returns non-empty bytes; wait-until-gone resolves after Notepad dialog is dismissed |
| 1.8.5 | `WaitForAsync(Locator, Func<ElementHandle, bool> condition)` in `ElementResolver`/`App` — generic retry-asserting primitive (poll-until-true, not just poll-until-found); this is the primitive Stage 1.9's `Expect()` API is built on |
| 1.8.6 | `AppOptions.TestDefaults` static — longer implicit wait, shorter action delay, tuned for CI/test-runner use rather than interactive scripting |

**After 1.8.6:** Engine API is production-ready and the planned Phase 1 work is complete. Stage 1.9 adds a new project on top of it — the engine itself can still gain features later; it's just not blocking anything downstream right now.

---

### Stage 1.9 — Test Adapter Layer
> **Unlocks:** Consumers write UI tests against `Expect(locator).ToHaveName(...)` — a retry-asserting API — instead of hand-rolled `FindAsync` + single-shot `Assert.Equal` races. Existing xUnit test projects get fixture/teardown boilerplate for free.
> **Estimated time:** 2–3 hours
> **Done when:** A Notepad-based test written against `AutoMancerTest`/`Expect()` passes and is shorter than the equivalent hand-rolled `IAsyncLifetime` version in `NotepadWorkflowTests.cs`.
> This stage adds two new projects; it does not modify `AutoMancer.Engine`.

| Batch | Work |
|---|---|
| 1.9.1 | `AutoMancer.Testing` project scaffold — references only `AutoMancer.Engine`, no test-framework dependency |
| 1.9.2 | `Expect(ElementHandle)` / `Expect(App, Locator)` assertion API — `ToHaveName`, `ToBeVisible`, `ToHaveText`, wrapping `App.WaitForAsync` from batch 1.8.5 so assertions poll instead of racing |
| 1.9.3 | On-failure diagnostics — a single-pass `FindAllAsync` lookup folds what was actually found into `ExpectFailedError`'s message; screenshot capture (opt-in) saves to `%TEMP%` and folds the path into the same message. Self-contained, no logger object — matches Playwright's model of a rich failure message over a managed logger |
| 1.9.4 | `AutoMancer.Testing.XUnit` project scaffold — `AppFixture` (`IAsyncLifetime`, one `App` per xUnit collection) and `AutoMancerTest` base class exposing `App` and `Expect` to derived test classes |
| 1.9.5 | `AutoMancerTestOptions.CaptureScreenshotsOnFailure` — one process-wide policy switch (set once, e.g. via `[ModuleInitializer]`) instead of a per-test logger; `AutoMancerTest` exposes it as an overridable default |
| 1.9.6 | Unit tests for `AutoMancer.Testing` (assertion pass/fail timing, retry behavior against a fake provider) + an integration test rewriting one `NotepadWorkflowTests` scenario on `AutoMancerTest`/`Expect()` |

**Scope boundary — what this stage does *not* provide**, left to whichever test project consumes it: which test framework to use (xUnit, NUnit, MSTest — only xUnit gets a first-party adapter here), test parallelism decisions (one `App` per collection vs. per class), what to assert about business logic (this stage provides "does this element exist / have this value," not "did the save succeed"), and test data setup (launching the right app in the right initial state).

**After 1.9.6:** Phase 1 complete. Direct engine calls (`App`/`Locator`) and the `Expect()` wrapper both work against the same engine surface — consumers choose per line of test code, not per project.

---

## Phase 2 — Extended Coverage

> **Prerequisite:** Phase 1 complete and `dotnet build AutoMancer.slnx` reporting 0 errors.
> Every batch here is additive to the Phase 1 engine surface — existing callers and existing tests are unaffected whether or not a given batch has landed. Nothing in this phase is a hard gate for Phase 3.

### Stage 2.1 — Locator Power Tools
> **Unlocks:** Finding elements that have no reliable `Name`/`AutomationId`; querying any built-in UIA property by name instead of a magic number; and reaching properties a specific app registered itself, which no fixed enum could ever anticipate.
> **Estimated time:** 4–5 hours
> **Done when:** A spatial locator finds an unlabeled `Edit` control next to a known label element in a live app; `Locator.ByProperty(UiaProperty.HelpText, ...)` finds an element via the named enum; `Locator.ByProperty(customPropertyGuid, ...)` finds one via a custom, app-registered property.

| Batch | Work |
|---|---|
| 2.1.1 | `Locator.Near(Locator anchor, SpatialDirection direction, int maxDistancePx = ...)` — new `LocatorStrategy.Spatial` case; the anchor locator and direction (`Above`/`Below`/`LeftOf`/`RightOf`/`Near`) travel as the locator's value |
| 2.1.2 | `SpatialMatcher` — resolves the anchor via the existing `ElementResolver.FindAsync`, then filters candidate elements by `BoundingRect` proximity/direction; lives once in `ElementResolver`, not duplicated per provider, since it operates on already-resolved rects rather than raw UIA queries |
| 2.1.3 | `Locator.ByProperty(int propertyId, object value)` — generic escape-hatch strategy for the built-in UIA property set; `Uia3Provider.BuildCondition` routes it straight to `IUIAutomation.CreatePropertyCondition(propertyId, value)`, bypassing the fixed strategy→property map |
| 2.1.4 | `UiaProperty` enum — named constants for the built-in properties worth surfacing (`HelpText`, `LocalizedControlType`, `IsOffscreen`, `ItemStatus`, `IsContentElement`, `AriaRole`, `AriaProperties`, ...) mapped to their well-known integer IDs; `Locator.ByProperty(UiaProperty property, object value)` overload so the common case never needs a raw int |
| 2.1.5 | `Locator.ByProperty(Guid customPropertyGuid, object value)` — for app-registered custom properties, which don't have stable integer IDs across processes; resolves the GUID to this session's `PropertyId` via `IUIAutomationRegistrar` before querying, since (unlike the built-in set) a custom property's numeric ID is assigned at registration time and can't be hardcoded |
| 2.1.6 | Unit tests for `SpatialMatcher` against a synthetic rect layout; integration tests — spatial locator finds a label's adjacent input, `UiaProperty` enum lookup finds an element by `HelpText`, and a custom-property lookup against a test app that registers one via `AutomationProperties.RegisterProperty` |

**After 2.1.6:** Locators cover the three cases the fixed strategy set can't reach: elements with no name, built-in properties nobody thought to add a named strategy for, and properties that only exist because a specific app registered them.

---

### Stage 2.2 — Wait and Resilience Primitives
> **Unlocks:** A ready-made vocabulary of wait conditions instead of hand-rolled predicates, and an opt-in way for actions to survive a UIA element going stale mid-test.
> **Estimated time:** 2–3 hours
> **Done when:** A canned `WaitConditions` predicate works as a drop-in `WaitForAsync` condition; an action against a deliberately-staled `ElementHandle` re-resolves once via `RuntimeId` and succeeds, but only when the caller opts in.

| Batch | Work |
|---|---|
| 2.2.1 | `WaitConditions` static class — canned `Func<ElementHandle, bool>` factories (`IsVisible()`, `NameEquals`, `NameContains`, `TextEquals`, `IsEnabled()`), mirroring Selenium's `ExpectedConditions`; each is a small predicate closure, no new engine surface |
| 2.2.2 | Opt-in stale-element re-resolve — a wrapper (e.g. an `AppOptions.ReresolveOnStale` flag, or an explicit `ClickAction.ExecuteWithRetryAsync`) that catches a stale-element COM failure and re-resolves once via `RuntimeId` through `ElementResolver` before retrying; default behavior for every existing call is unchanged — this only activates when a caller explicitly asks for it |
| 2.2.3 | Unit tests for each `WaitConditions` predicate against a fake element; a resilience test simulating a stale COM failure via a mock provider, verifying the opt-in retry re-resolves and succeeds, plus a control test proving non-opted-in calls fail exactly as they do today |

**After 2.2.3:** `WaitForAsync` has a starter vocabulary instead of only raw predicates, and flaky COM staleness has an opt-in escape hatch that never changes default behavior.

---

### Stage 2.3 — Context Menu Fallback Provider
> **Unlocks:** Right-click context menus on apps that don't expose them through UIA at all.
> **Estimated time:** 3–4 hours
> **Done when:** `app.ClickContextMenuItemAsync(target, itemName)` finds and clicks a menu item via the native `HMENU`, on an app whose popup menu UIA can't see.

| Batch | Work |
|---|---|
| 2.3.1 | `ContextMenuAction` — right-clicks the target, then locates the resulting popup window (class `#32768`) via `EnumWindows`/`GetClassName`; tries the existing UIA `Menu`/`MenuItem` path first and only falls back to native when UIA comes back empty, matching the existing provider-chain philosophy |
| 2.3.2 | Native menu item enumeration and invocation — `GetMenu`/`GetSubMenu`/`GetMenuItemInfo` to read item text and state, `GetMenuItemRect` plus a synthetic click (or `TrackPopupMenuEx` command dispatch) to invoke the matched item |
| 2.3.3 | `App.ClickContextMenuItemAsync(Locator target, string itemName)` |
| 2.3.4 | Integration test against an app with a legacy/native context menu, verifying the UIA path is tried first and the native fallback only engages when UIA finds nothing |

**After 2.3.4:** Context menus join the list of things AutoMancer can drive even when an app's accessibility tree doesn't cooperate.

---

### Stage 2.4 — Diagnostics and Environment Coverage
> **Unlocks:** Catching resource leaks and DPI-boundary bugs that only surface when a real Windows app runs a while or moves across monitors — neither has a web/mobile equivalent.
> **Estimated time:** 2.5–3.5 hours
> **Done when:** `app.WatchResourcesAsync()` returns a sampled series of GDI/USER handle counts and working-set memory across a run; `app.MoveToMonitorAsync(index)` relocates the window to a specific monitor.

| Batch | Work |
|---|---|
| 2.4.1 | `ResourceWatch` — samples `GetGuiResources` (`GR_GDIOBJECTS`/`GR_USEROBJECTS`) and `Process.WorkingSet64` on a timer; `App.WatchResourcesAsync(TimeSpan interval)` returns an `IAsyncDisposable` sampler plus the snapshot history |
| 2.4.2 | `MonitorHelper` — enumerates monitors via `EnumDisplayMonitors`, exposing each monitor's bounds and DPI; `App.MoveToMonitorAsync(int monitorIndex)` repositions the root window via the existing `WindowAction` |
| 2.4.3 | Integration tests — resource watch on a live app returns a non-empty, plausible sample series; move-to-monitor test, skipping gracefully (inconclusive, not failed) on single-monitor CI runners, matching the existing pattern already used for Win32 snapshot tests |

**After 2.4.3:** Two diagnostic capabilities with no counterpart in Selenium, Playwright, Appium, or WinAppDriver — both are pure data collection, and nothing else in the roadmap depends on them.

---

### Stage 2.5 — Accessibility Audit Mode
> **Unlocks:** A standalone report of interactive elements with no accessible name — useful for QA and for actual screen-reader compliance, built entirely from data the tree walker already collects.
> **Estimated time:** 2–3 hours
> **Done when:** `automancer audit <session>` (or `app.AuditAccessibilityAsync()`) lists every interactive control with a missing or empty `Name`, alongside its control type and tree path.

| Batch | Work |
|---|---|
| 2.5.1 | `AccessibilityAuditor` — walks an `ElementSnapshot` tree from `SnapshotAsync`, flags nodes whose `ControlType` is interactive (`Button`, `Edit`, `CheckBox`, ...) with a null/empty `Name`; returns findings with a tree-path breadcrumb |
| 2.5.2 | CLI command — `automancer audit <session>` prints findings as a table, matching `tree`'s existing output style |
| 2.5.3 | Unit tests for the auditor against a synthetic tree with mixed named/unnamed nodes; integration test against a live app asserting the report is well-formed (not a fixed finding count, since that drifts across Windows versions) |

**After 2.5.3:** The engine can locate elements no named strategy reaches, wait on more than raw predicates, opt into stale-element resilience, drive native context menus, watch process health, and test across monitors — all additive to the Phase 1 surface. Phase 2 continues into the visual fallback provider.

---

### Stage 2.6 — Visual Provider (OCR + Template Matching)
> **Unlocks:** Automating apps that expose no accessibility tree at all (legacy ERP, custom-rendered UIs), asserting on rendered pixels instead of just finding elements by them, and waiting for animations to actually finish instead of guessing with a fixed delay.
> **Estimated time:** 4–5 hours
> **Done when:** `app.find(text="Submit Order")` finds a button by its visible text in an app with no UIA elements; a visual regression assertion fails when a window's rendered output drifts from a stored baseline; `app.WaitForIdleAsync()` resolves once consecutive screenshots stop changing.

| Batch | Work | Plan ref |
|---|---|---|
| 2.6.1 | `VisualProvider` skeleton — implements `IElementProvider`; screenshots target window via `GDI+` | Engine spec §4.3 |
| 2.6.2 | OCR path — `Windows.Media.Ocr.OcrEngine` (on-device, no external service); `automancer:text` strategy | Engine spec §4.3 |
| 2.6.3 | Template matching — `OpenCvSharp4.Windows`; `automancer:image` strategy (base64 PNG template) | Engine spec §4.3 |
| 2.6.4 | Add `visual` to default `ElementProviderOptions.ProviderChain`; integration test with a no-UIA test app | Engine spec §4.3 |
| 2.6.5 | `App.WaitForIdleAsync()` — diffs consecutive `ScreenshotAsync()` captures at a short interval until two frames match within a pixel-difference threshold, or a timeout is hit; replaces the ad-hoc `Task.Delay(300) // flyout animation` waits already scattered through the integration test suite with a real settledness check | — |
| 2.6.6 | Visual regression snapshot assertion — captures the current window/element region and pixel-diffs it against a stored baseline PNG, failing past a configurable difference threshold; reuses the screenshot/pixel-compare plumbing built for template matching in 2.6.3 | — |

**This stage is entirely engine-side** — like the rest of Phase 2, it only touches `AutoMancer.Engine`. Exposing `VisualProvider` over HTTP is Phase 3's job (see batch 3.2.8), since that requires the daemon project to exist first.

**After 2.6.6:** The full UIA3 → UIA2 → Win32 → Visual fallback chain is complete, plus two capabilities that build on the same screenshot infrastructure: real animation-settle waits and pixel-level regression assertions. Phase 2 continues into engine hardening and examples.

---

### Stage 2.7 — Engine Hardening and Examples
> **Unlocks:** Confidence that the engine behaves identically across DPI settings and Windows versions, a failed find leaves behind an annotated screenshot instead of just a stack trace, and new C# consumers have working example projects to start from.
> **Estimated time:** 4–6 hours
> **Done when:** The Notepad integration suite passes at 100%, 125%, and 150% DPI with identical logical coordinates, and on both Windows 10 and 11; a failed find under a debug flag leaves an annotated PNG in `%TEMP%`; all four example projects build and run.

| Batch | Work |
|---|---|
| 2.7.1 | Annotated error screenshots — on `ElementNotFoundError`, capture a screenshot and draw a red overlay around the search area / closest-match bounding rect via GDI+, save to `%TEMP%`; a new `AppOptions`/`ElementProviderOptions` flag (off by default) turns this on. The engine sibling of Stage 1.9's on-failure diagnostics, for direct `App` consumers rather than `Expect()` |
| 2.7.2 | DPI compat matrix — run the Notepad integration test suite at 100%, 125%, and 150% DPI; assert identical logical coordinates across all three |
| 2.7.3 | Windows 10/11 compat — run the full `AutoMancer.Engine.Tests` integration suite on both OS versions; fix any behavioral differences |
| 2.7.4 | Example projects — `examples/notepad/`, `examples/winforms-calculator/`, `examples/legacy-no-uia/` (Visual Provider showcase), `examples/xunit-test-adapter/` |

**This stage is entirely engine-side**, same as the rest of Phase 2 — none of these four batches touch `AutoMancer.Daemon` or either SDK. Exposing the debug-screenshot flag over HTTP is Phase 3's job (see batch 3.6.1), same split pattern as the Visual Provider's daemon wiring in batch 3.2.8.

**After 2.7.4:** Phase 2 complete. All of Phase 2's engine work — deeper locators, wait/resilience primitives, native context menus, diagnostics, accessibility auditing, visual fallback, and now hardening/examples — is additive to the Phase 1 surface, none of it required for Phase 3 to begin.

---

## Phase 3 — Polyglot HTTP Layer

> **Prerequisite:** Phase 1 complete and `dotnet build AutoMancer.slnx` reporting 0 errors. Phase 2 is sequenced first because the daemon gets to wrap a deeper surface if it's already there — but nothing in Phase 2 is a hard gate; Phase 3 can start against the Phase 1 surface alone if priorities shift.
> The engine and `App` facade APIs are settled at this point. The daemon, SDKs, and test adapter are additive — no engine source files are modified for this phase's work.

### Stage 3.1 — Daemon Foundation
> **Unlocks:** Any HTTP client can find elements in a Windows app. `curl` test is possible.
> **Estimated time:** 3 hours
> **Done when:** `curl -X POST http://127.0.0.1:27272/session/.../element -d '{"using":"control type","value":"Edit"}'` returns an element ID.

| Batch | Work | Plan ref |
|---|---|---|
| 3.1.1 | Daemon project scaffold — `AutoMancer.Daemon.csproj`, test project; add both to solution | Daemon Task 1 |
| 3.1.2 | `W3CErrorWriter`, `HttpContext`, `DaemonConfig` | Daemon Task 2 |
| 3.1.3 | `Router` (regex route table), `DaemonSession` (element registry + lock), `SessionManager` | Daemon Task 3 |
| 3.1.4 | `StatusEndpoint` + `Program.cs` entry point; smoke test `GET /status` | Daemon Task 4 |
| 3.1.5 | `SessionEndpoints` — `POST /session` (parse capabilities, launch/attach, return sessionId), `DELETE /session/:id` | Daemon Task 5 |
| 3.1.6 | `FindEndpoints` — 4 W3C find endpoints; routes all strategies including `id` and `automancer:xpath` | Daemon Task 6 |
| 3.1.7 | Route `AppOptions.Logger` (owned by `App` since batch 1.1.4) into daemon session construction — attach an `EngineLogger` sink (file or stdout `TextWriter`, configurable via `DaemonConfig`) when building each session's `AppOptions`, so the engine's existing structured operational trace (`ElementResolver` + `AppSession` launch/attach) reaches daemon request handling. Daemon-side only, no engine source changes needed. Unrelated to Stage 1.9's `Expect()` diagnostics, which stay self-contained | Engine spec Task 4 |

**After 3.1.7:** Full element-finding stack reachable via HTTP, with structured operational logging wired end-to-end. Selenium client can locate elements.

---

### Stage 3.2 — Daemon Interactions and Properties
> **Unlocks:** Full automation loop over HTTP. SDKs can now be built.
> **Estimated time:** 3.5–4.5 hours
> **Done when:** All C# daemon integration tests pass.

| Batch | Work | Plan ref |
|---|---|---|
| 3.2.1 | `InteractionEndpoints` — click (with `modifiers` and `button` params), double-click, right-click, hover, value (type), clear, drag, scroll wheel (deltaX/deltaY); coordinate-based click and hover without an element ID | Daemon Task 7 |
| 3.2.2 | `PropertyEndpoints` — text, name, enabled, selected, displayed, rect, attribute | Daemon Task 8 |
| 3.2.3 | `ScreenshotEndpoint` (base64 PNG), `TimeoutEndpoints` | Daemon Task 9 (partial) |
| 3.2.4 | `WindowEndpoints` — size GET/POST, maximize, minimize, restore, close | Daemon Task 9 (window) |
| 3.2.5 | `KeyboardEndpoints` — hotkey, key-down/up; `ExtensionEndpoints` — provider, scroll-to, app PID, kill, snapshot | Daemon Tasks 9–10 |
| 3.2.6 | `ClipboardEndpoints` — `GET /session/:id/clipboard` (text or image), `POST /session/:id/clipboard`; `PatternEndpoints` — expand, collapse, toggle, select, addToSelection, removeFromSelection, allSelectedItems, isMultiple, getValue, setFocus as `windows: *` extension commands | — |
| 3.2.7 | C# daemon integration tests — `DaemonIntegrationTests.cs`; tests covering full HTTP surface including clipboard, pattern commands, and coordinate-based interactions | Daemon Task 11 |
| 3.2.8 | Expose the Visual Provider over HTTP — add it to `FindEndpoints`' resolver builder; surface via `automancer:resolverChain` capability. Depends on Stage 2.6 having shipped (the engine-side `VisualProvider`); skip or defer this batch if Phase 2 hasn't reached that stage yet | Daemon spec §3.3 |

**After 3.2.8:** Complete daemon. All W3C + extension endpoints covered by C# tests, including the visual fallback provider if Phase 2 built it first. No Python/TypeScript toolchain needed to verify daemon correctness.

---

### Stage 3.3 — Python SDK
> **Unlocks:** `pip install automancer` and `from automancer import App`.
> **Estimated time:** 2–3 hours
> **Done when:** `pytest tests/integration/ -m integration` passes all 5 Notepad tests.

| Batch | Work | Plan ref |
|---|---|---|
| 3.3.1 | `pyproject.toml`, `errors.py`, package scaffold | SDK Task 1 |
| 3.3.2 | `locator.py` — `build_locator` with all strategies including `runtime_id` and `xpath` | SDK Task 2 |
| 3.3.3 | `session.py` — W3C HTTP client, `_unwrap` error mapper, window methods | SDK Task 2 |
| 3.3.4 | `element.py` (`Rect`, `Element`) + `app.py` (`App.launch`, `attach`, `find`, `wait_*`, `window_size`, `maximize`) | SDK Task 3 |
| 3.3.5 | Control subclasses — `Button`, `TextBox`, `ComboBox`, `DataGrid` | SDK Task 4 |
| 3.3.6 | Integration tests — launch, type, click, attach-by-pid, screenshot, closest-match error | SDK Task 5 |
| 3.3.7 | `mypy --strict` pass | SDK Task 5 |

**After 3.3.7:** Python SDK ships.

---

### Stage 3.4 — TypeScript SDK
> **Unlocks:** `npm install automancer` and `import { App } from 'automancer'`.
> **Estimated time:** 2–3 hours
> **Done when:** `npm test` passes all integration tests; `tsc --noEmit` clean.

| Batch | Work | Plan ref |
|---|---|---|
| 3.4.1 | `package.json`, `tsconfig.json`, `errors.ts`, `index.ts` scaffold | SDK Task 6 |
| 3.4.2 | `types.ts` (all interfaces incl. `runtimeId`, `xpath`), `Locator.ts` (`buildLocator`), unit tests | SDK Task 7 |
| 3.4.3 | `Session.ts` — HTTP client, error mapper, window methods | SDK Task 7 |
| 3.4.4 | `Element.ts` (async getters + actions) + `App.ts` (launch, attach, find, waitUntilGone, window management) | SDK Task 8 |
| 3.4.5 | Control subclasses — `Button`, `TextBox`, `ComboBox`, `DataGrid` | SDK Task 9 |
| 3.4.6 | Integration tests + final `tsc --noEmit` pass | SDK Task 9 |

**After 3.4.6:** Both SDKs ship. The full stack (engine → daemon → SDKs) is complete.

---

### Stage 3.5 — CI Session (`automancer-session`)
> **Unlocks:** Running AutoMancer tests in GitHub Actions and Azure Pipelines without a real desktop.
> **Estimated time:** 2–3 hours
> **Done when:** `automancer-session start && automancer-session run pytest tests/ && automancer-session stop` exits 0 in a CI pipeline.
> **Not the `automancer` CLI from Stage 1.6.** The CLI (`launch`/`find`/`tree`/`click`/`type`) drives one action at a time against a desktop a human is already logged into — a developer tool. `automancer-session` solves a different problem: `SendInput`/UIA generally need an interactive desktop (`WinSta0\Default`), which most CI runners don't have — clicks silently go nowhere and screenshots come back black. `start`/`stop` create and tear down an isolated virtual desktop (`CreateDesktop`/`SetThreadDesktop`); `run <command>` executes a whole test command (`pytest tests/`, `npm test`, ...) inside it, not individual UI actions.

| Batch | Work | Plan ref |
|---|---|---|
| 3.5.1 | `VirtualDesktop.cs` in daemon — `CreateDesktop` / `SetThreadDesktop` Win32 APIs | Engine spec §9 |
| 3.5.2 | `automancer-session` CLI — `start`, `run`, `stop`, `list` sub-commands | Engine spec §9 |
| 3.5.3 | Session isolation — each `automancer-session start` gets a named desktop; parallel sessions don't interfere | Engine spec §9 |
| 3.5.4 | CI example — GitHub Actions `.yml` with start/run/stop steps | Engine spec §9 |

**After 3.5.4:** Headless CI works. The `automancer-session run pytest tests/integration/` pattern is validated.

---

### Stage 3.6 — Polish and v0.1 Release
> **Unlocks:** Something you can actually publish and point people to.
> **Estimated time:** ongoing
> **Done when:** The Definition of Done checklist below is fully satisfied.

| Batch | Work |
|---|---|
| 3.6.1 | Wire the `automancer:debugScreenshots` session capability — a thin daemon pass-through that turns on the `AppOptions`/`ElementProviderOptions` flag from batch 2.7.1. Depends on Stage 2.7 having shipped; skip or defer otherwise, same pattern as batch 3.2.8 |
| 3.6.2 | `protocol/endpoints.md` — generated endpoint reference from the daemon's route table |
| 3.6.3 | `README.md` — 15-minute quickstart; `pip install automancer` + 10-line Notepad example |
| 3.6.4 | Apache-2.0 license header audit — every `.cs`, `.py`, `.ts` file must start with the copyright line |

---

## Quick-Reference: Definition of Done

**Phase 1 complete when:**
- [ ] `dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration"` — all unit tests green
- [ ] `dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"` — all integration tests green on Windows
- [ ] `automancer launch notepad.exe && automancer tree <id>` produces a readable element tree
- [ ] `ElementNotFoundError` includes `closestMatch` when a near-match exists
- [ ] Double-click, right-click, hover, hotkey, drag all verified against a live app
- [ ] `ScreenshotAsync()` returns a valid PNG from a live window
- [ ] `WaitUntilGoneAsync()` resolves correctly when an element disappears
- [ ] `WaitForAsync(locator, condition)` resolves once the condition is true, not merely once the element is found
- [ ] `AutoMancer.Testing` / `AutoMancer.Testing.XUnit` ship; `Expect(locator).ToHaveName(...)` and direct `App`/`Locator` calls both work in the same test method against the same engine surface
- [ ] `dotnet test tests/AutoMancer.Testing.Tests/` — all tests green
- [ ] Apache-2.0 license header present in all `.cs` source files

**Phase 2 complete when:**
- [ ] `dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration"` — all unit tests green including Phase 2 additions
- [ ] A spatial locator finds an unlabeled control relative to a known anchor
- [ ] `Locator.ByProperty(UiaProperty.HelpText, ...)` finds an element via the named enum
- [ ] `Locator.ByProperty(customPropertyGuid, ...)` finds an element via a custom, app-registered property
- [ ] A canned `WaitConditions` predicate works as a drop-in `WaitForAsync` condition
- [ ] Stale-element re-resolve is opt-in and covered by a test — calls that don't opt in behave exactly as before
- [ ] A native context-menu item is found and clicked via the `HMENU` fallback on an app where UIA doesn't expose it
- [ ] `WatchResourcesAsync()` returns a non-empty handle/memory sample series over a live test run
- [ ] `MoveToMonitorAsync(index)` relocates a window to a specific monitor (or the test degrades gracefully on single-monitor CI)
- [ ] `automancer audit <session>` reports at least one finding against a deliberately-unnamed test control
- [ ] `app.find(text="Submit Order")` finds a button by its visible text in an app with no UIA elements
- [ ] `app.WaitForIdleAsync()` resolves once consecutive screenshots stop changing, and a visual regression assertion fails when rendered output drifts from a stored baseline
- [ ] The Notepad integration suite passes at 100%, 125%, and 150% DPI with identical logical coordinates
- [ ] The full `AutoMancer.Engine.Tests` integration suite passes on both Windows 10 and 11
- [ ] A failed find under the debug-screenshot flag leaves an annotated PNG in `%TEMP%`
- [ ] All four example projects (`notepad`, `winforms-calculator`, `legacy-no-uia`, `xunit-test-adapter`) build and run

**Phase 3 complete when (full v0.1):**
- [ ] `automancer-session start && automancer-session run pytest tests/integration/notepad_test.py && automancer-session stop` exits 0 on clean Windows 11
- [ ] Same test passes at 100%, 125%, 150% DPI without modification
- [ ] Raw `curl` to `/session` with Selenium capabilities creates a session and returns a valid `sessionId`
- [ ] All W3C error responses conform to `{"value":{"error":"...","message":"...","stacktrace":"..."}}`
- [ ] Python SDK passes `mypy --strict` with zero errors
- [ ] TypeScript SDK passes `tsc --noEmit` with zero errors
- [ ] Apache-2.0 license header present in all `.cs`, `.py`, `.ts` source files

---

## Time Estimates (rough)

| Stage | Est. hours | Cumulative | Phase |
|---|---|---|---|
| 1.1 — Core Types | 1–2 h | 2 h | 1 |
| 1.2 — UIA3 + Resolver | 2–3 h | 5 h | 1 |
| 1.3 — Actions + DPI | 2 h | 7 h | 1 |
| 1.4 — Full Provider Chain | 2 h | 9 h | 1 |
| 1.5 — Extended Locators | 2–3 h | 12 h | 1 |
| 1.6 — Window + CLI | 2 h | 14 h | 1 |
| 1.7 — Extended Interactions | 2–2.5 h | 16.5 h | 1 |
| 1.8 — Screenshot + Wait + App | 2–2.5 h | 19 h | 1 |
| 1.9 — Test Adapter Layer | 2–3 h | **21.5–22 h ← Phase 1 done** | 1 |
| 2.1 — Locator Power Tools | 4–5 h | 25.5–27 h | 2 |
| 2.2 — Wait & Resilience Primitives | 2–3 h | 27.5–30 h | 2 |
| 2.3 — Context Menu Fallback Provider | 3–4 h | 30.5–34 h | 2 |
| 2.4 — Diagnostics & Environment Coverage | 2.5–3.5 h | 33–37.5 h | 2 |
| 2.5 — Accessibility Audit Mode | 2–3 h | 35–40.5 h | 2 |
| 2.6 — Visual Provider (+ visual regression, WaitForIdleAsync) | 4–5 h | 39–45.5 h | 2 |
| 2.7 — Engine Hardening and Examples | 4–6 h | **43–51.5 h ← Phase 2 done** | 2 |
| 3.1 — Daemon Foundation | 3 h | 46–54.5 h | 3 |
| 3.2 — Daemon Interactions + C# Tests (+ visual provider wiring) | 3.5–4.5 h | 49.5–59 h | 3 |
| 3.3 — Python SDK | 2–3 h | 51.5–62 h | 3 |
| 3.4 — TypeScript SDK | 2–3 h | 53.5–65 h | 3 |
| 3.5 — CI Session | 2–3 h | 55.5–68 h | 3 |
| 3.6 — Polish (debug-screenshot capability, docs, license audit) | ongoing | — | 3 |

---

## Suggested First Session

If you want to start and reach a meaningful milestone in one sitting:

1. Complete Stage 1.1 batches 1.1.1–1.1.4 (types, providers contract, logger, errors) — **~90 min**
2. Complete Stage 1.2 batches 1.2.1–1.2.3 (NativeMethods, Uia3Provider, ElementResolver) — **~90 min**
3. Run Stage 1.2 batch 1.2.4 (integration test) — if Notepad shows a `ResolvedVia == "uia3"` result, the core engine loop is working

At that point you have a real Windows automation engine finding elements in C#. Everything after is building on top of that foundation.
