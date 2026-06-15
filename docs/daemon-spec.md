# AutoMancer Daemon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking..
>
> **NEVER run git commands (add, commit, push) automatically.** All version control is the developer's responsibility. Bash blocks in this document are implementation reference — execute the build/test lines only, never the git lines.

**Phase:** 2 of 2 — this plan adds an HTTP translation layer on top of the completed Phase 1 engine. No engine source files are modified.

**Goal:** Build `automancerd` — the W3C WebDriver 2 HTTP server that wraps `AutoMancer.Engine` and exposes a JSON-over-HTTP API for session management, element finding, and interaction.

**Architecture:** `AutoMancer.Daemon` is a .NET 10 Windows console app using raw `HttpListener`. A `Router` matches method+path patterns to handler lambdas. `SessionManager` holds in-memory sessions keyed by UUID. Each endpoint handler resolves the session, calls into the Engine, and writes W3C-shaped JSON responses. The daemon is a consumer of `AutoMancer.Engine` — it references it as a project dependency and calls its public API. Nothing in the engine changes.

**WinAppDriver lessons incorporated:** WinAppDriver defaulted to port 4723 (Appium standard) making it usable by stock Selenium/Appium clients. AutoMancer uses 27272 by default but should accept `--port 4723` for Appium compat mode. WinAppDriver also omitted window management endpoints entirely; AutoMancer adds `GET/POST .../window/size` and `POST .../window/:handle/maximize`. New locator strategies `id` (RuntimeId) and `automancer:xpath` are routed here.

**Tech Stack:** C# latest / .NET 10 Windows (`net10.0-windows10.0.22621.0`), `System.Net.HttpListener`, `System.Text.Json`, `AutoMancer.Engine` project reference, `System.Drawing.Common` (screenshots)

**Test Stack:** xunit 2.8, Moq 4.20, `Microsoft.NET.Test.Sdk` 17.9.0 — all tests are C# in `tests/AutoMancer.Daemon.Tests/`; run with `dotnet test`

**Prerequisite:** Phase 1 complete — Core Engine plan done, all engine tests passing, `dotnet build AutoMancer.slnx` exits 0. The engine API is treated as stable before this plan begins.

---

## File Map

```
src/
└── AutoMancer.Daemon/
    ├── AutoMancer.Daemon.csproj
    ├── Program.cs                      entry point: parse args, start HttpListener loop
    ├── DaemonConfig.cs                 --port, --host, --log-level
    ├── Http/
    │   ├── Router.cs                   regex route table; dispatches to handlers
    │   └── HttpContext.cs              wraps HttpListenerContext; JSON read/write helpers
    ├── Session/
    │   ├── SessionManager.cs           thread-safe ConcurrentDictionary of sessions
    │   └── DaemonSession.cs            session state: HWND, options, element registry, lock
    ├── Endpoints/
    │   ├── StatusEndpoint.cs           GET /status
    │   ├── SessionEndpoints.cs         POST /session, DELETE /session/:id
    │   ├── FindEndpoints.cs            POST .../element, .../elements; routes id + automancer:xpath
    │   ├── InteractionEndpoints.cs     POST .../click, .../value, .../clear
    │   ├── PropertyEndpoints.cs        GET .../text, name, enabled, selected, displayed, rect, attribute
    │   ├── TimeoutEndpoints.cs         GET/POST .../timeouts
    │   ├── ScreenshotEndpoint.cs       GET .../screenshot (base64 PNG)
    │   ├── WindowEndpoints.cs          GET/POST .../window/size, POST .../window/:handle/maximize
    │   └── ExtensionEndpoints.cs       /automancer/* non-standard endpoints
    └── Errors/
        └── W3CErrorWriter.cs           builds {"value":{"error":...}} responses

tests/
└── AutoMancer.Daemon.Tests/
    ├── AutoMancer.Daemon.Tests.csproj           xunit 2.8 + Moq 4.20
    ├── Session/
    │   └── SessionManagerTests.cs               unit — add/get/remove sessions
    ├── Endpoints/
    │   ├── W3CErrorWriterTests.cs               unit — error response JSON shape
    │   └── SessionEndpointsTests.cs             unit — POST /session capability parsing
    └── Integration/
        └── DaemonIntegrationTests.cs            [Trait("Category","Integration")] — live HttpListener; see Task 11
```

---

### Task 1: Daemon project scaffold

**What:** Creates the `AutoMancer.Daemon` console project and its test project, adds them to the solution. The daemon outputs as `automancerd.exe` and references the Engine library.

**Creates:**
- `src/AutoMancer.Daemon/AutoMancer.Daemon.csproj` — `OutputType=Exe`, `AssemblyName=automancerd`, Engine project reference
- `tests/AutoMancer.Daemon.Tests/AutoMancer.Daemon.Tests.csproj` — xunit + Moq + Daemon project reference


- [ ] **Scaffold and configure**

```bash
dotnet new console -n AutoMancer.Daemon -o src/AutoMancer.Daemon
dotnet new xunit -n AutoMancer.Daemon.Tests -o tests/AutoMancer.Daemon.Tests
dotnet sln add src/AutoMancer.Daemon/AutoMancer.Daemon.csproj
dotnet sln add tests/AutoMancer.Daemon.Tests/AutoMancer.Daemon.Tests.csproj
del src\AutoMancer.Daemon\Program.cs
del tests\AutoMancer.Daemon.Tests\UnitTest1.cs
# Edit each .csproj to set TargetFramework to net10.0-windows10.0.22621.0 and LangVersion to preview
dotnet build AutoMancer.slnx
```

**Done when:** `dotnet build AutoMancer.slnx` exits 0.

---

### Task 2: W3C error writer, HTTP context, and daemon config

**What:** The `W3CErrorWriter` produces the W3C-required `{"value":{"error":"...","message":"..."}}` response shape from any exception. `HttpContext` is a thin wrapper around `HttpListenerContext` that provides `ReadBodyAsync` and `WriteJsonAsync`. `DaemonConfig` parses `--port`, `--host`, and `--log-level` from args.

**Creates:**
- `src/AutoMancer.Daemon/Errors/W3CErrorWriter.cs` — `BuildErrorBody`, `ForException` (maps Engine exceptions to W3C error codes and HTTP status codes)
- `src/AutoMancer.Daemon/Http/HttpContext.cs` — `ReadBodyAsync` → `JsonNode?`; `WriteJsonAsync`; `WriteErrorAsync`
- `src/AutoMancer.Daemon/DaemonConfig.cs` — `ListenPrefix` property; `FromArgs` factory
- `tests/AutoMancer.Daemon.Tests/Endpoints/W3CErrorWriterTests.cs` — verifies `{"value":{"error":...}}` JSON shape


- [ ] **Write test, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Daemon.Tests/ --filter "W3CErrorWriterTests"
```

**Done when:** 1 test passes.

---

### Task 3: Router and session manager

**What:** `Router` compiles URL templates like `/session/:sessionId/element/:elementId` into named-group regexes and dispatches incoming requests to the matching handler, catching exceptions and writing W3C error responses. `SessionManager` is a thread-safe dictionary. `DaemonSession` holds the HWND, options, element registry, and a per-session serialization lock.

**Creates:**
- `src/AutoMancer.Daemon/Http/Router.cs` — `Add(method, template, handler)`, `DispatchAsync`
- `src/AutoMancer.Daemon/Session/SessionManager.cs` — `Add`, `Get`, `Remove`, `All`
- `src/AutoMancer.Daemon/Session/DaemonSession.cs` — `RegisterElement`, `GetElement`, `ToAppSession`, `WithLockAsync`, `KillApp`
- `tests/AutoMancer.Daemon.Tests/Session/SessionManagerTests.cs` — add+get, unknown ID returns null, remove


- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Daemon.Tests/ --filter "SessionManagerTests"
```

**Done when:** 3 tests pass.

---

### Task 4: GET /status and Program.cs entry point

**What:** The daemon's health endpoint returns `{"value":{"ready":true,...}}`. `Program.cs` wires together config, sessions, logger, router, and the `HttpListener` accept loop. Ctrl+C stops the listener cleanly.

**Creates:**
- `src/AutoMancer.Daemon/Endpoints/StatusEndpoint.cs` — `Register(router, sessions)`
- `src/AutoMancer.Daemon/Program.cs` — parses args, registers all endpoints, starts `HttpListener`, runs accept loop

Note: temporarily stub missing endpoint classes with empty `Register` methods so the build succeeds before they're implemented.


- [ ] **Implement, build, and smoke test**

```bash
dotnet build src/AutoMancer.Daemon/AutoMancer.Daemon.csproj
# Terminal 1:
dotnet run --project src/AutoMancer.Daemon
# Terminal 2:
curl http://127.0.0.1:27272/status
```

**Done when:** `curl /status` returns `{"value":{"ready":true,...}}`.

---

### Task 5: Session lifecycle — POST /session and DELETE /session/:id

**What:** `POST /session` parses W3C capabilities (the `automancer:*` namespaced fields), creates an `AppSession` (launch or attach), wraps it in a `DaemonSession`, and returns the `sessionId`. `DELETE /session/:id` disposes the session.

**Creates:**
- `src/AutoMancer.Daemon/Endpoints/SessionEndpoints.cs` — `Capabilities` record; `ParseCapabilities(JsonElement)`; `Register`
- `tests/AutoMancer.Daemon.Tests/Endpoints/SessionEndpointsTests.cs` — `ParseCapabilities` correctly extracts `attachPid`, `implicitWaitMs`, `appPath`


- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Daemon.Tests/ --filter "SessionEndpointsTests"
# Smoke test with curl (daemon running):
curl -X POST http://127.0.0.1:27272/session \
  -H "Content-Type: application/json" \
  -d '{"capabilities":{"alwaysMatch":{"platformName":"windows","automancer:appPath":"C:\\Windows\\notepad.exe"}}}'
```

**Done when:** 2 tests pass; curl returns a `sessionId`.

---

### Task 6: Element finding endpoints

**What:** Implements the four W3C find endpoints: find one/all from a session, find one/all from an element (scoped search). Parses the `{"using":"...","value":"..."}` body and maps strategies to `Locator`. Rejects browser-only strategies (`css selector`, `xpath`, `link text`) with `invalid argument`. Routes two new strategies: `id` → `Locator.ByRuntimeId(value)` and `automancer:xpath` → `Locator.ByXPath(value)` (XPath evaluated against the UIA tree, not a browser DOM).

**Creates:**
- `src/AutoMancer.Daemon/Endpoints/FindEndpoints.cs` — `Register`; `FindElement`, `FindElements`, `ParseRequest`, `BuildResolver`


- [ ] **Implement and smoke test**

```bash
dotnet build src/AutoMancer.Daemon/AutoMancer.Daemon.csproj
# With daemon running and a Notepad session open as SESSION_ID:
curl -X POST http://127.0.0.1:27272/session/$SESSION_ID/element \
  -H "Content-Type: application/json" \
  -d '{"using":"control type","value":"Edit"}'
```

**Done when:** Curl returns `{"value":{"element-6066-11e4-a52e-4f735466cecf":"<id>"}}`.

---

### Task 7: Interaction endpoints — click, value, clear

**What:** Delegates `POST .../click` to `ClickAction`, `POST .../value` to `TypeAction`, `POST .../clear` to `ClearAction`. Each endpoint resolves the session and element from the URL, reconstructs an `AppSession` via `ToAppSession()`, and executes the action.

**Creates:**
- `src/AutoMancer.Daemon/Endpoints/InteractionEndpoints.cs` — `Register`; `GetSessionAndElement` helper


- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Daemon/AutoMancer.Daemon.csproj
```

**Done when:** Build succeeds with 0 errors.

---

### Task 8: Property endpoints

**What:** Exposes element state over HTTP: `text`, `name` (control type), `enabled`, `selected`, `displayed`, `rect`, `attribute/:name`. Each reads from the cached `ElementHandle` or falls through to the live UIA element when the handle doesn't have the data.

**Creates:**
- `src/AutoMancer.Daemon/Endpoints/PropertyEndpoints.cs` — `Register`; `GetElement` helper; `TryGet`/`TryGetBool` COM-safe accessors


- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Daemon/AutoMancer.Daemon.csproj
```

**Done when:** Build succeeds with 0 errors.

---

### Task 9: Window management endpoints

**What:** WinAppDriver lacks these entirely. Implements W3C-compatible window management: get/set window size and maximize. Maps to `WindowAction` in the Engine (which uses `WindowPattern` falling back to `SetWindowPos`). The `:windowHandle` segment accepts `"current"` to target the session's root window.

**Creates:**
- `src/AutoMancer.Daemon/Endpoints/WindowEndpoints.cs` — `Register`; `GET/POST /session/:id/window/size`; `POST /session/:id/window/:handle/maximize`; `POST /session/:id/window/:handle/restore`


- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Daemon/AutoMancer.Daemon.csproj
# With daemon running and Notepad session open:
curl http://127.0.0.1:27272/session/$SESSION_ID/window/size
curl -X POST http://127.0.0.1:27272/session/$SESSION_ID/window/current/maximize
```

**Done when:** `GET .../window/size` returns `{"value":{"width":...,"height":...}}`; maximize visibly changes the window.

---

### Task 10: Screenshot, timeout, and extension endpoints

**What:** Completes the daemon's surface area. Screenshot captures the app window as a base64 PNG. Timeout endpoints read/acknowledge (timeouts are baked into `ResolverOptions` at session creation). Extension endpoints expose AutoMancer-specific capabilities: provider info, scroll-to, PID, kill app, and annotated snapshot.

**Creates:**
- `src/AutoMancer.Daemon/Endpoints/ScreenshotEndpoint.cs` — `GDI+ CopyFromScreen` → base64 PNG; `CaptureBase64Internal` for extension endpoint reuse
- `src/AutoMancer.Daemon/Endpoints/TimeoutEndpoints.cs` — GET returns `ImplicitWaitMs`; POST is a no-op
- `src/AutoMancer.Daemon/Endpoints/ExtensionEndpoints.cs` — `/automancer/element/:id/provider`, `/automancer/element/:id/scroll-to`, `/automancer/app/pid`, `/automancer/app` (DELETE = kill), `/automancer/snapshot`

Extends `AutoMancer.Daemon.csproj` with `System.Drawing.Common`.


- [ ] **Implement and end-to-end test**

```bash
dotnet build src/AutoMancer.Daemon/AutoMancer.Daemon.csproj
# With daemon running:
curl http://127.0.0.1:27272/session/$SESSION_ID/screenshot | \
  python -c "import sys,json,base64; d=json.load(sys.stdin); open('screen.png','wb').write(base64.b64decode(d['value']))"
curl http://127.0.0.1:27272/session/$SESSION_ID/automancer/element/$ELEMENT_ID/provider
dotnet test tests/AutoMancer.Daemon.Tests/ -v
```

**Done when:** All daemon tests pass; screenshot saves a valid PNG.

---

### Task 11: C# daemon integration tests

**What:** End-to-end C# tests that spin up a live `HttpListener`-backed daemon on a random port, create a real session against Notepad, and exercise the full HTTP surface. These run in `AutoMancer.Daemon.Tests` under `[Trait("Category","Integration")]` — no Python or TypeScript toolchain required. They are the single C# proof that the daemon speaks correct W3C wire protocol before the SDK integration tests are written.

**Creates:**
- `tests/AutoMancer.Daemon.Tests/Integration/DaemonIntegrationTests.cs`

**Test coverage:**

| Test | Verifies |
|---|---|
| `GetStatus_ReturnsReady` | `GET /status` → `{"value":{"ready":true,...}}` |
| `PostSession_LaunchNotepad_ReturnsSessionId` | `POST /session` → sessionId non-null |
| `FindElement_ByControlType_ReturnsElementRef` | `POST .../element` with `control type` → W3C element reference |
| `FindElement_Typo_ReturnsClosestMatch` | `no such element` response includes `data.closestMatch` |
| `Interact_TypeInEditor_TextAppears` | `POST .../value` → subsequent `GET .../text` matches |
| `Screenshot_ReturnsPngBase64` | `GET .../screenshot` → base64 decodes to PNG header `89504e47` |
| `DeleteSession_DisposesCleanly` | `DELETE /session/:id` → `GET /session/:id/status` returns 404 |

**Implementation pattern:** Each test class creates a `DaemonTestFixture` that starts `automancerd` on a free port via `HttpListener`, runs the test via `HttpClient`, then disposes the daemon. Use `IAsyncLifetime` for setup/teardown.

- [ ] **Write tests, run (expect FAIL for missing endpoints), implement remaining stubs, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Daemon.Tests/ --filter "Category=Integration" -v
```

**Done when:** All 7 integration tests pass with a real Notepad process.

---

## Constraints

- All error responses use `{"value":{"error":"...","message":"...","stacktrace":""}}` — no naked exceptions
- `no such element` errors include `data.closestMatch` when confidence > 0.70
- Element references use the W3C constant key `element-6066-11e4-a52e-4f735466cecf`
- `css selector`, `xpath`, `link text` return `{"error":"invalid argument"}` — these are browser concepts with no meaning here
- `automancer:xpath` is NOT the same as the rejected `xpath` strategy — it targets the UIA tree and must be explicitly routed to `Locator.ByXPath`
- `id` strategy routes to `Locator.ByRuntimeId`, not element-6066 reference lookup
- Sessions are in-memory only — no persistence between daemon restarts
- `DELETE /session/:id` disposes the session and its element registry cleanly
- Default port is 27272; `--port 4723` enables Appium-compatible mode for teams already using Appium client libraries
