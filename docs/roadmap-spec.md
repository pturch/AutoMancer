# AutoMancer — Master Implementation Roadmap

> This is the sequencing document. It organizes work from the three detailed plans into stages and batches that can be picked up and executed independently. Each batch has a concrete deliverable, a time estimate, and a clear "done" signal.
>
> **NEVER run git commands (add, commit, push) automatically.** All version control is the developer's responsibility.
>
> **Tech stack:** C# latest / .NET 10 (`net10.0-windows10.0.22621.0`). The .NET 10 SDK creates `.slnx` solution files instead of `.sln` — use `AutoMancer.slnx` everywhere.
>
> **Detailed specs:** [Core Engine](./core-engine-spec.md) · [Daemon](./daemon-spec.md) · [SDKs](./sdks-spec.md)

---

## Delivery Strategy

This project ships in two distinct phases. **Phase 1** is a self-contained C# proof of concept — engine + CLI, no HTTP, no daemon, no SDK toolchain required. **Phase 2** adds the daemon and polyglot SDKs on top of the engine without modifying it.

```
╔══════════════════════════════════════════════════════════╗
║  PHASE 1 — C# Proof of Concept  (Stages 1–6, ~14 hours) ║
║                                                          ║
║   Stage 1: Core Types                                    ║
║       │                                                  ║
║   Stage 2: UIA3 Provider + Resolver ──► first find       ║
║       │                                                  ║
║   Stage 3: Actions + DPI ───────────► first click/type   ║
║       │                                                  ║
║   Stage 4: Full Provider Chain ─────► UIA2, Win32        ║
║       │                                                  ║
║   Stage 5: Extended Locators ───────► RuntimeId, XPath   ║
║       │                                                  ║
║   Stage 6: Window Management + CLI ─► PoC complete ✓     ║
╚══════════════════════════════════════════════════════════╝
                        │
                        │  Engine API frozen. Daemon added
                        │  as a new project — no engine changes.
                        ▼
╔══════════════════════════════════════════════════════════╗
║  PHASE 2 — Polyglot HTTP Layer  (Stages 7–13)           ║
║                                                          ║
║   Stage 7:  Daemon Foundation ──────► curl finds elements ║
║       │                                                  ║
║   Stage 8:  Daemon Interactions ────► full HTTP surface   ║
║       │                                                  ║
║   ├──► Stage 9:  Python SDK ────────► pip install works  ║
║   ├──► Stage 10: TypeScript SDK ────► npm install works  ║
║       │                                                  ║
║   Stage 11: CI Session ─────────────► headless CI works  ║
║       │                                                  ║
║   Stage 12: Visual Provider ────────► OCR + template     ║
║       │                                                  ║
║   Stage 13: Polish ─────────────────► v0.1 shippable ✓  ║
╚══════════════════════════════════════════════════════════╝
```

### Why this split works

`AutoMancer.Engine` is a pure class library with no HTTP awareness. The daemon is a separate project that consumes the engine — it doesn't modify it. A C# consumer in Phase 1 calls the engine API directly (no port, no process, no wire protocol). When Phase 2 adds the daemon, the engine API is unchanged and the daemon is purely additive.

The one thing to keep clean during Phase 1: `ElementHandle` must stay opaque (only `Id` and `NativeHandle` exposed). The daemon's element registry depends on this — it stores handles by ID between stateless HTTP requests. The existing design already does this correctly.

---

## Test Language Summary

All C# code (engine + daemon) is tested in C# with xunit + Moq. SDK client code is tested in the SDK's own language.

| Layer | Test framework | Location |
|---|---|---|
| Engine (core library) | C# / xunit 2.8 / Moq 4.20 | `tests/AutoMancer.Engine.Tests/` |
| Daemon (HTTP server) | C# / xunit 2.8 / Moq 4.20 | `tests/AutoMancer.Daemon.Tests/` |
| Python SDK | Python / pytest | `sdk/python/tests/` |
| TypeScript SDK | TypeScript / vitest | `sdk/typescript/tests/` |

---

## Phase 1 — C# Proof of Concept

### Stage 1 — Core Types and Contracts
> **Unlocks:** Everything. This is the type vocabulary the entire codebase shares.
> **Estimated time:** 1–2 hours
> **Done when:** `dotnet build AutoMancer.slnx` passes with 0 errors.

| Batch | Work | Plan ref |
|---|---|---|
| 1.1 | Solution scaffold — `AutoMancer.slnx`, three `.csproj` files, NuGet refs, delete stubs | Engine Task 1 |
| 1.2 | `LocatorStrategy`, `Locator`, `ElementHandle`, `Rect`, `ElementProviderOptions` | Engine Task 2 |
| 1.3 | `IElementProvider` + `ElementSnapshot`; `AppSession` (`LaunchAsync`, `AttachByPid`, `AttachByTitle`) | Engine Task 3 |
| 1.4 | `EngineLogger` + error types (`ElementNotFoundError`, `ElementNotInteractableError`, `AppLaunchError`) | Engine Task 4 |
| 1.5 | `ClosestMatchFinder` + Levenshtein; `DpiHelper` testable overloads | Engine Tasks 5–6 |

**Commit at end of each batch.** After 1.5: run `dotnet test --filter "Category!=Integration"` — all unit tests green.

---

### Stage 2 — UIA3 Provider and Element Resolver
> **Unlocks:** Finding elements in any UIA-accessible Windows app.
> **Estimated time:** 2–3 hours
> **Done when:** `automancer find <session> --by control --value Edit` returns an element in a live Notepad window.

| Batch | Work | Plan ref |
|---|---|---|
| 2.1 | `NativeMethods.cs` — all P/Invoke declarations | Engine Task 7 |
| 2.2 | `Uia3Provider` — Name, AutomationId, ClassName, ControlType strategies; `ControlTypeMap` | Engine Task 8 |
| 2.3 | `ElementResolver` — retry loop, fallback chain, `TrySnapshotAsync`, throws `ElementNotFoundError` with closest match | Engine Task 9 |
| 2.4 | Integration tests — launch Notepad, find Edit control via UIA3, verify `ResolvedVia == "uia3"`; typo → closest match hint | Engine Task 10 |

**After 2.4:** `dotnet test --filter "Category=Integration"` — 3 integration tests green.

---

### Stage 3 — Actions and DPI
> **Unlocks:** Actually interacting with elements, not just finding them.
> **Estimated time:** 2 hours
> **Done when:** The integration test types "Hello AutoMancer" into Notepad and opens the File menu.

| Batch | Work | Plan ref |
|---|---|---|
| 3.1 | `DpiHelper` production overloads (`GetDpiForWindow`, `GetWindowRect`); integrate into the engine | Engine Task 6 (prod side) |
| 3.2 | `ClickAction` — `InvokePattern` first, `SendInput` fallback; `GetCenter` uses `DpiHelper` | Engine Task 11 |
| 3.3 | `TypeAction` — `ValuePattern.SetValue` first, Unicode `SendInput` fallback (layout-agnostic via `KEYEVENTF_UNICODE`) | Engine Task 11 |
| 3.4 | Integration tests — `TypeInEditor_TextAppears`, `ClickFileMenu_OpensMenu` | Engine Task 11 |

**After 3.4:** 5 integration tests green. You can now automate Notepad end-to-end from C#.

---

### Stage 4 — Full Provider Fallback Chain
> **Unlocks:** Automating apps with partial or no UIA accessibility trees.
> **Estimated time:** 2–3 hours
> **Done when:** Win32 fallback triggers correctly when UIA finds nothing; all 15 integration tests pass.

| Batch | Work | Plan ref |
|---|---|---|
| 4.1 | `IElementOperator` interface (find/operate split); extract `Uia3Operator` from `Uia3Provider`; add `Uia2Provider` + `Uia2Operator`; `UseWPF` in engine csproj | Engine Task 12 |
| 4.2 | `Win32Provider` — `EnumChildWindows`, title/class match; no operator (SendInput covers it; `Win32Operator` added in 6.1 for window management) | Engine Task 13 |
| 4.3 | `ClearAction` — `ValuePattern.SetValue("")` → Ctrl+A Delete; `ScrollAction` — `ScrollItemPattern` | Engine Task 14 |
| 4.4 | `NotepadWorkflowTests` — 10-test comprehensive suite covering the full chain, all locator strategies, snapshot, and error hints | Engine Task 13b |

**Architecture note:** Provider and operator responsibilities are separated. `IElementProvider` (find-only) and `IElementOperator` (native interact) are distinct interfaces. Each provider holds a stateless operator singleton in a `private static readonly` field and injects it into `ElementHandle.Operator` at wrap time. `ClickAction` and `TypeAction` check `element.Operator` first; they fall through to `SendInput` when it is `null` (Win32/Visual) or when the pattern returns `false` (pattern not supported).

**WinUI3 test isolation:** Windows 11 Notepad is single-instance. Test classes that each launch Notepad must share a `[Collection("Notepad")]` with `DisableParallelization = true`. Each `DisposeAsync` adds an 800 ms post-kill delay to ensure the process fully exits before the next `LaunchAsync`. A UIA3-only warmup in each `InitializeAsync` ensures the COM element tree is populated before the chain resolver runs.

**After 4.4:** 15 integration tests pass; Win32 snapshot and provider-chain order verified.

---

### Stage 5 — Extended Locator Strategies
> **Unlocks:** Complex element addressing — positional paths, re-finding by ID, XPath queries.
> **Estimated time:** 2–3 hours
> **Done when:** All three new strategies find correct elements in Notepad.

| Batch | Work | Plan ref |
|---|---|---|
| 5.1 | `AutoMancerPathParser` — `"Window > Pane[2] > Button[\"OK\"]"` syntax; unit tests | Engine Task 15 |
| 5.2 | `RuntimeId` strategy — add to enum, `Locator.ByRuntimeId`, `UIA_RuntimeIdPropertyId` in both providers | Engine Task 17 |
| 5.3 | `XPathEvaluator` — UIA tree snapshot → `XDocument` → XPath → element indices; unit tests | Engine Task 18 |
| 5.4 | Wire `AutomancerXPath` into `Uia3Provider.FindElementAsync`; add `CollectElements` helper for index→element mapping | Engine Task 18 |

**After 5.4:** `Locator.ByXPath("//Button[@Name='File']")` finds the File menu in Notepad.

---

### Stage 6 — Window Management and CLI Harness
> **Unlocks:** The `automancer` command-line tool is usable for manual testing of any app.
> **Estimated time:** 2 hours
> **Done when:** `automancer launch notepad.exe && automancer tree <id>` produces a readable element tree.
> **Phase 1 complete after this stage.**

| Batch | Work | Plan ref |
|---|---|---|
| 6.1 | `WindowAction` — `ResizeAsync`, `MoveAsync`, `SetVisualStateAsync` (maximize/minimize/restore), `GetSizeAsync`; `WindowPattern` → `SetWindowPos` fallback | Engine Task 19 |
| 6.2 | `SessionStore.cs` — `%TEMP%\automancer-sessions.json` | Engine Task 16 |
| 6.3 | CLI commands — `launch`, `find`, `tree` (first three) | Engine Task 16 |
| 6.4 | CLI commands — `click`, `type` | Engine Task 16 |
| 6.5 | Full unit test pass | Engine Task 20 |

**After 6.5:** Phase 1 complete. The engine library is production-ready. Every unit and integration test is green. C# consumers can use `AutoMancer.Engine` directly — no daemon required.

---

## Phase 2 — Polyglot HTTP Layer

> **Prerequisite:** Phase 1 complete and `dotnet build AutoMancer.slnx` reporting 0 errors.
> The engine API is treated as stable from this point. The daemon and SDKs are additive — no engine source files are modified.

### Stage 7 — Daemon Foundation
> **Unlocks:** Any HTTP client can find elements in a Windows app. `curl` test is possible.
> **Estimated time:** 3 hours
> **Done when:** `curl -X POST http://127.0.0.1:27272/session/.../element -d '{"using":"control type","value":"Edit"}'` returns an element ID.

| Batch | Work | Plan ref |
|---|---|---|
| 7.1 | Daemon project scaffold — `AutoMancer.Daemon.csproj`, test project; add both to solution | Daemon Task 1 |
| 7.2 | `W3CErrorWriter`, `HttpContext`, `DaemonConfig` | Daemon Task 2 |
| 7.3 | `Router` (regex route table), `DaemonSession` (element registry + lock), `SessionManager` | Daemon Task 3 |
| 7.4 | `StatusEndpoint` + `Program.cs` entry point; smoke test `GET /status` | Daemon Task 4 |
| 7.5 | `SessionEndpoints` — `POST /session` (parse capabilities, launch/attach, return sessionId), `DELETE /session/:id` | Daemon Task 5 |
| 7.6 | `FindEndpoints` — 4 W3C find endpoints; routes all 8 strategies including `id` and `automancer:xpath` | Daemon Task 6 |

**After 7.6:** Full element-finding stack reachable via HTTP. Selenium client can locate elements.

---

### Stage 8 — Daemon Interactions and Properties
> **Unlocks:** Full automation loop over HTTP. SDKs can now be built.
> **Estimated time:** 3–4 hours
> **Done when:** All C# daemon integration tests pass.

| Batch | Work | Plan ref |
|---|---|---|
| 8.1 | `InteractionEndpoints` — click, value (type), clear | Daemon Task 7 |
| 8.2 | `PropertyEndpoints` — text, name, enabled, selected, displayed, rect, attribute | Daemon Task 8 |
| 8.3 | `ScreenshotEndpoint` (base64 PNG), `TimeoutEndpoints` | Daemon Task 9 (partial) |
| 8.4 | `WindowEndpoints` — size GET/POST, maximize, minimize | Daemon Task 9 (window) |
| 8.5 | `ExtensionEndpoints` — `/automancer/element/:id/provider`, scroll-to, app PID, kill, snapshot | Daemon Task 10 |
| 8.6 | C# daemon integration tests — `DaemonIntegrationTests.cs`; 7 tests covering full HTTP surface | Daemon Task 11 |

**After 8.6:** Complete daemon. All W3C + extension endpoints covered by C# tests. No Python/TypeScript toolchain needed to verify daemon correctness.

---

### Stage 9 — Python SDK
> **Unlocks:** `pip install automancer` and `from automancer import App`.
> **Estimated time:** 2–3 hours
> **Done when:** `pytest tests/integration/ -m integration` passes all 5 Notepad tests.

| Batch | Work | Plan ref |
|---|---|---|
| 9.1 | `pyproject.toml`, `errors.py`, package scaffold | SDK Task 1 |
| 9.2 | `locator.py` — `build_locator` with all 8 strategies including `runtime_id` and `xpath` | SDK Task 2 |
| 9.3 | `session.py` — W3C HTTP client, `_unwrap` error mapper, window methods | SDK Task 2 |
| 9.4 | `element.py` (`Rect`, `Element`) + `app.py` (`App.launch`, `attach`, `find`, `wait_*`, `window_size`, `maximize`) | SDK Task 3 |
| 9.5 | Control subclasses — `Button`, `TextBox`, `ComboBox`, `DataGrid` | SDK Task 4 |
| 9.6 | Integration tests — launch, type, click, attach-by-pid, screenshot, closest-match error | SDK Task 5 |
| 9.7 | `mypy --strict` pass | SDK Task 5 |

**After 9.7:** Python SDK ships.

---

### Stage 10 — TypeScript SDK
> **Unlocks:** `npm install automancer` and `import { App } from 'automancer'`.
> **Estimated time:** 2–3 hours
> **Done when:** `npm test` passes all integration tests; `tsc --noEmit` clean.

| Batch | Work | Plan ref |
|---|---|---|
| 10.1 | `package.json`, `tsconfig.json`, `errors.ts`, `index.ts` scaffold | SDK Task 6 |
| 10.2 | `types.ts` (all interfaces incl. `runtimeId`, `xpath`), `Locator.ts` (`buildLocator`), unit tests | SDK Task 7 |
| 10.3 | `Session.ts` — HTTP client, error mapper, window methods | SDK Task 7 |
| 10.4 | `Element.ts` (async getters + actions) + `App.ts` (launch, attach, find, waitUntilGone, window management) | SDK Task 8 |
| 10.5 | Control subclasses — `Button`, `TextBox`, `ComboBox`, `DataGrid` | SDK Task 9 |
| 10.6 | Integration tests + final `tsc --noEmit` pass | SDK Task 9 |

**After 10.6:** Both SDKs ship. The full stack (engine → daemon → SDKs) is complete.

---

### Stage 11 — CI Session (`automancer-session`)
> **Unlocks:** Running AutoMancer tests in GitHub Actions and Azure Pipelines without a real desktop.
> **Estimated time:** 2–3 hours
> **Done when:** `automancer-session start && automancer-session run pytest tests/ && automancer-session stop` exits 0 in a CI pipeline.

| Batch | Work | Plan ref |
|---|---|---|
| 11.1 | `VirtualDesktop.cs` in daemon — `CreateDesktop` / `SetThreadDesktop` Win32 APIs | Engine spec §9 |
| 11.2 | `automancer-session` CLI — `start`, `run`, `stop`, `list` sub-commands | Engine spec §9 |
| 11.3 | Session isolation — each `automancer-session start` gets a named desktop; parallel sessions don't interfere | Engine spec §9 |
| 11.4 | CI example — GitHub Actions `.yml` with start/run/stop steps | Engine spec §9 |

**After 11.4:** Headless CI works. The `automancer-session run pytest tests/integration/` pattern is validated.

---

### Stage 12 — Visual Provider (OCR + Template Matching)
> **Unlocks:** Automating apps that expose no accessibility tree at all (legacy ERP, custom-rendered UIs).
> **Estimated time:** 3–4 hours
> **Done when:** `app.find(text="Submit Order")` finds a button by its visible text in an app with no UIA elements.

| Batch | Work | Plan ref |
|---|---|---|
| 12.1 | `VisualProvider` skeleton — implements `IElementProvider`; screenshots target window via `GDI+` | Engine spec §4.3 |
| 12.2 | OCR path — `Windows.Media.Ocr.OcrEngine` (on-device, no external service); `automancer:text` strategy | Engine spec §4.3 |
| 12.3 | Template matching — `OpenCvSharp4.Windows`; `automancer:image` strategy (base64 PNG template) | Engine spec §4.3 |
| 12.4 | Add `visual` to default `ElementProviderOptions.ProviderChain`; integration test with a no-UIA test app | Engine spec §4.3 |
| 12.5 | Add `VisualProvider` to `FindEndpoints` resolver builder; expose via `automancer:resolverChain` capability | Daemon spec §3.3 |

**After 12.5:** The full UIA3 → UIA2 → Win32 → Visual fallback chain is complete.

---

### Stage 13 — Polish and v0.1 Release
> **Unlocks:** Something you can actually publish and point people to.
> **Estimated time:** ongoing
> **Done when:** The Definition of Done checklist below is fully satisfied.

| Batch | Work |
|---|---|
| 13.1 | Annotated error screenshots — when `automancer:debugScreenshots` is true, save PNG with red overlay on search area to `%TEMP%` |
| 13.2 | Example projects — `examples/notepad/`, `examples/winforms-calculator/`, `examples/legacy-no-uia/` |
| 13.3 | DPI compat matrix — run the Notepad integration test at 100%, 125%, 150% DPI; assert identical logical coordinates |
| 13.4 | Windows 10/11 compat — run full integration suite on both; fix any behavioral differences |
| 13.5 | `protocol/endpoints.md` — generated endpoint reference from the daemon's route table |
| 13.6 | `README.md` — 15-minute quickstart; `pip install automancer` + 10-line Notepad example |
| 13.7 | MIT license header audit — every `.cs`, `.py`, `.ts` file must start with the copyright line |

---

## Quick-Reference: Definition of Done

**Phase 1 complete when:**
- [ ] `dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration"` — all unit tests green
- [ ] `dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"` — all integration tests green on Windows
- [ ] `automancer launch notepad.exe && automancer tree <id>` produces a readable element tree
- [ ] `ElementNotFoundError` includes `closestMatch` when a near-match exists
- [ ] MIT license header present in all `.cs` source files

**Phase 2 complete when (full v0.1):**
- [ ] `automancer-session start && automancer-session run pytest tests/integration/notepad_test.py && automancer-session stop` exits 0 on clean Windows 11
- [ ] Same test passes at 100%, 125%, 150% DPI without modification
- [ ] Raw `curl` to `/session` with Selenium capabilities creates a session and returns a valid `sessionId`
- [ ] All W3C error responses conform to `{"value":{"error":"...","message":"...","stacktrace":"..."}}`
- [ ] Python SDK passes `mypy --strict` with zero errors
- [ ] TypeScript SDK passes `tsc --noEmit` with zero errors
- [ ] MIT license header present in all `.cs`, `.py`, `.ts` source files

---

## Time Estimates (rough)

| Stage | Est. hours | Cumulative | Phase |
|---|---|---|---|
| 1 — Core Types | 1–2 h | 2 h | 1 |
| 2 — UIA3 + Resolver | 2–3 h | 5 h | 1 |
| 3 — Actions + DPI | 2 h | 7 h | 1 |
| 4 — Full Provider Chain | 2 h | 9 h | 1 |
| 5 — Extended Locators | 2–3 h | 12 h | 1 |
| 6 — Window + CLI | 2 h | **14 h ← Phase 1 done** | 1 |
| 7 — Daemon Foundation | 3 h | 17 h | 2 |
| 8 — Daemon Interactions + C# Tests | 3–4 h | 21 h | 2 |
| 9 — Python SDK | 2–3 h | 24 h | 2 |
| 10 — TypeScript SDK | 2–3 h | 27 h | 2 |
| 11 — CI Session | 2–3 h | 30 h | 2 |
| 12 — Visual Provider | 3–4 h | 34 h | 2 |
| 13 — Polish | ongoing | — | 2 |

---

## Suggested First Session

If you want to start and reach a meaningful milestone in one sitting:

1. Complete Stage 1 batches 1.1–1.4 (types, providers contract, logger, errors) — **~90 min**
2. Complete Stage 2 batches 2.1–2.3 (NativeMethods, Uia3Provider, ElementResolver) — **~90 min**
3. Run Stage 2 batch 2.4 (integration test) — if Notepad shows a `ResolvedVia == "uia3"` result, the core engine loop is working

At that point you have a real Windows automation engine finding elements in C#. Everything after is building on top of that foundation.
