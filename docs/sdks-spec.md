# AutoMancer SDKs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking..
>
> **NEVER run git commands (add, commit, push) automatically.** All version control is the developer's responsibility. Bash blocks in this document are implementation reference — execute the build/test lines only, never the git lines.

**Phase:** 2 of 2 — client SDKs built on top of the daemon. No engine or daemon source files are modified.

**Goal:** Build the Python and TypeScript client SDKs that talk to `automancerd` over HTTP using the W3C WebDriver wire protocol.

**Architecture:** Both SDKs are pure HTTP clients — no Windows code, no native dependencies. Each has a low-level `Session` class that handles the W3C wire protocol and error mapping, an `App` class for session lifecycle, an `Element` class for interactions, and control subclasses for type-specific operations.

**WinAppDriver lessons incorporated:** WinAppDriver required users to use the generic Selenium client library, which has browser-centric ergonomics (`driver.find_element(By.XPATH, "...")`, no `find(name=...)` shorthand). AutoMancer's custom SDKs provide a cleaner API and can expose Windows-specific capabilities that Selenium clients cannot. New additions: `runtime_id`/`runtimeId` locator kwarg (maps to the `id` strategy), and `window_size`/`set_window_size`/`maximize` on `App` (filling the gap WinAppDriver left).

**Tech Stack:** Python 3.10+, `httpx`, `pyproject.toml`, `mypy --strict`, `pytest` | TypeScript / Node 18+, native `fetch`, `tsconfig`, `tsc --noEmit`, `vitest`

**Test language boundary:** SDK tests are written in the SDK's own language (pytest for Python, vitest for TypeScript). C# end-to-end coverage of the daemon HTTP surface is handled in `AutoMancer.Daemon.Tests` (see Daemon plan Task 11). The SDK integration tests assume `automancerd` is already running.

**Prerequisite:** Phase 2 daemon complete and `automancerd` reachable at `http://127.0.0.1:27272` before integration tests can pass. Phase 1 (engine + CLI) must be done first.

---

## File Map

```
sdk/
├── python/
│   ├── pyproject.toml
│   ├── automancer/
│   │   ├── __init__.py               exports App, Element, all errors
│   │   ├── session.py                low-level W3C HTTP client
│   │   ├── app.py                    App class — launch/attach/find/wait
│   │   ├── element.py                Element class + Rect dataclass
│   │   ├── locator.py                kwargs → {"using":...,"value":...}
│   │   ├── errors.py                 AutoMancerError hierarchy
│   │   └── controls/
│   │       ├── __init__.py
│   │       ├── button.py
│   │       ├── combobox.py
│   │       ├── datagrid.py
│   │       └── textbox.py
│   └── tests/
│       ├── test_locator.py
│       ├── test_errors.py
│       └── integration/
│           └── test_notepad.py
│
└── typescript/
    ├── package.json
    ├── tsconfig.json
    ├── src/
    │   ├── index.ts                  re-exports App, Element, errors, types
    │   ├── Session.ts                W3C HTTP client
    │   ├── App.ts
    │   ├── Element.ts
    │   ├── Locator.ts                Locator → W3CLocator
    │   ├── errors.ts
    │   ├── types.ts                  Locator, LaunchOptions, AttachOptions, Rect interfaces
    │   └── controls/
    │       ├── Button.ts
    │       ├── ComboBox.ts
    │       ├── DataGrid.ts
    │       └── TextBox.ts
    └── tests/
        ├── locator.test.ts
        └── integration/
            └── notepad.test.ts
```

---

### Task 1: Python SDK scaffold

**What:** Creates the Python package with `pyproject.toml`, installs it in editable mode, and defines the error hierarchy. Errors carry machine-readable fields (`locator`, `elapsed_ms`, `closest_match`) so tests can assert on them.

**Creates:**
- `sdk/python/pyproject.toml` — `hatchling` build, `httpx` dependency, `pytest`+`mypy` dev deps
- `sdk/python/automancer/errors.py` — `AutoMancerError`, `DaemonNotRunningError`, `SessionError`, `ElementNotFoundError` (with structured fields), `ElementNotInteractableError`
- `sdk/python/tests/test_errors.py` — verifies `ElementNotFoundError` stores `closest_match` and `elapsed_ms`


- [ ] **Scaffold, install, and run test**

```bash
cd sdk/python
pip install -e ".[dev]"
pytest tests/test_errors.py -v
```

**Done when:** `test_errors.py` passes after `pip install -e ".[dev]"`.

---

### Task 2: Python locator builder and session client

**What:** `build_locator` maps keyword arguments to the W3C `{"using":"...","value":"..."}` format — the single translation point between the Python API and the wire protocol. Supports all standard strategies plus `runtime_id` (maps to `id` strategy → RuntimeId lookup) and `xpath` (maps to `automancer:xpath` → XPath on the UIA tree). `Session` is the low-level HTTP client: it wraps every HTTP response, extracts the `value` field, and raises the correct Python exception when the W3C error shape appears.

**Creates:**
- `sdk/python/automancer/locator.py` — `build_locator(*, name, automation_id, control, path, text, class_name)` → `dict`; raises `ValueError` if no strategy given
- `sdk/python/automancer/session.py` — `Session.create(capabilities)`, find, click, type, clear, get_text, get_enabled, get_rect, screenshot, `_unwrap` maps W3C errors to Python exceptions
- `sdk/python/tests/test_locator.py` — all six strategy kwargs, no-kwarg raises


- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
cd sdk/python && pytest tests/test_locator.py -v
```

**Done when:** 6 locator tests pass.

---

### Task 3: Python App and Element classes

**What:** `App` is the user-facing entry point: `App.launch("notepad.exe")` creates a session, `app.find(name="Submit")` finds an element. `Element` exposes properties (`text`, `enabled`, `visible`, `rect`) and actions (`click`, `type`, `clear`) by delegating to `Session`. `App` supports the context manager protocol for automatic cleanup. Also adds window management methods missing from WinAppDriver: `window_size()` → `(width, height)`, `set_window_size(w, h)`, `maximize()`.

**Creates:**
- `sdk/python/automancer/element.py` — `Element` class; `Rect` frozen dataclass; `find`/`find_all` for scoped search
- `sdk/python/automancer/app.py` — `App.launch`, `App.attach`; `find`, `find_all`, `wait_until_gone`, `wait_until`, `screenshot`, `close`; `__enter__`/`__exit__`


- [ ] **Implement and type-check**

```bash
cd sdk/python && mypy automancer/ --strict
```

**Done when:** `mypy --strict` reports no issues.

---

### Task 4: Python control subclasses

**What:** Thin subclasses of `Element` that add control-type-specific methods: `TextBox.set_value`, `ComboBox.select`/`options`, `DataGrid.rows`/`cell`. They're returned by `find()` when the daemon reports the matching control type.

**Creates:**
- `sdk/python/automancer/controls/__init__.py`
- `sdk/python/automancer/controls/button.py` — `Button(Element)` (click already on `Element`)
- `sdk/python/automancer/controls/textbox.py` — `value()`, `set_value(text)`
- `sdk/python/automancer/controls/combobox.py` — `select(item)`, `options()`, `selected_item()`
- `sdk/python/automancer/controls/datagrid.py` — `rows()`, `row(index)`, `cell(row, col)`, `row_count()`


- [ ] **Implement and type-check**

```bash
cd sdk/python && mypy automancer/ --strict
```

**Done when:** `mypy --strict` reports no issues.

---

### Task 5: Python integration tests

**What:** End-to-end tests against a live daemon. Covers launch+find+type, file menu click, closest-match error, attach by PID, and screenshot PNG verification.

**Creates:**
- `sdk/python/tests/integration/test_notepad.py` — `@pytest.mark.integration`; 5 tests

Extends `pyproject.toml` with the `integration` marker definition.


- [ ] **Run integration tests (daemon must be running)**

```bash
cd sdk/python && pytest tests/integration/ -v -m integration
mypy automancer/ --strict
```

**Done when:** 5 integration tests pass; mypy clean.

---

### Task 6: TypeScript SDK scaffold

**What:** Creates the Node 18+ package with strict TypeScript config (`ES2020`, `Node16` module resolution), installs `vitest` for testing, and defines the error class hierarchy matching the Python SDK.

**Creates:**
- `sdk/typescript/package.json` — `typescript`, `vitest`, `@types/node` dev deps; `build`/`typecheck`/`test` scripts
- `sdk/typescript/tsconfig.json` — `strict: true`, `ES2020`, `Node16`, `declaration: true`
- `sdk/typescript/src/errors.ts` — `AutoMancerError`, `DaemonNotRunningError`, `SessionError`, `ElementNotFoundError` (with `locator`, `elapsedMs`, `closestMatch`), `ElementNotInteractableError`, `ClosestMatch` interface
- `sdk/typescript/src/index.ts` — re-exports `App`, `Element`, types, errors


- [ ] **Scaffold and install**

```bash
cd sdk/typescript && npm install
npm run typecheck
```

**Done when:** `npm run typecheck` exits 0 (or only fails due to not-yet-created files — acceptable at this stage).

---

### Task 7: TypeScript types, Locator, and Session

**What:** `types.ts` defines the public interfaces (`Locator`, `LaunchOptions`, `AttachOptions`, `Rect`). `buildLocator` is the single translation point from the typed `Locator` to the W3C wire format — adds `runtimeId` (maps to `id` strategy) and `xpath` (maps to `automancer:xpath`). `Session` is the HTTP client: creates sessions, finds elements, interacts, reads properties, and maps W3C error responses to TypeScript exceptions. Also adds `getWindowSize()`, `setWindowSize(w, h)`, `maximize()` session methods.

**Creates:**
- `sdk/typescript/src/types.ts` — all public interfaces + `W3CLocator` internal type
- `sdk/typescript/src/Locator.ts` — `buildLocator(loc: Locator): W3CLocator`
- `sdk/typescript/src/Session.ts` — `Session.create`, all find/interact/property methods, `_unwrap` error mapper
- `sdk/typescript/tests/locator.test.ts` — all five strategy mappings; no-strategy throws


- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
cd sdk/typescript && npm test -- locator.test.ts
```

**Done when:** 5 locator tests pass.

---

### Task 8: TypeScript App and Element

**What:** `Element` exposes async getter properties (`text`, `enabled`, `visible`, `rect`) and async action methods (`click`, `type`, `clear`, `find`, `findAll`). `App` mirrors the Python SDK API: `App.launch`, `App.attach`, `find`, `findAll`, `waitUntilGone`, `screenshot`, `close`. Adds window management filling the WinAppDriver gap: `windowSize()`, `setWindowSize(w, h)`, `maximize()`.

**Creates:**
- `sdk/typescript/src/Element.ts` — async properties + actions; `find`/`findAll` for scoped search
- `sdk/typescript/src/App.ts` — `static async launch/attach`; find, findAll, waitUntilGone, screenshot, close


- [ ] **Implement and type-check**

```bash
cd sdk/typescript && npm run typecheck
```

**Done when:** `tsc --noEmit` reports `Found 0 errors`.

---

### Task 9: TypeScript controls and integration tests

**What:** Control subclasses mirror the Python SDK. Integration tests verify the full stack: launch Notepad, type text, get closest-match error on typo, verify screenshot is a PNG buffer.

**Creates:**
- `sdk/typescript/src/controls/Button.ts`
- `sdk/typescript/src/controls/TextBox.ts` — `value()`, `setValue(text)`
- `sdk/typescript/src/controls/ComboBox.ts` — `select(item)`, `options()`, `selectedItem()`
- `sdk/typescript/src/controls/DataGrid.ts` — `rows()`, `row(index)`, `cell(row, col)`, `rowCount()`
- `sdk/typescript/tests/integration/notepad.test.ts` — 3 integration tests; requires daemon


- [ ] **Implement, run integration tests, final typecheck**

```bash
cd sdk/typescript && npm test
npm run typecheck
```

**Done when:** 3 integration tests pass; `tsc --noEmit` reports `Found 0 errors`.

---

## Constraints

- Apache-2.0 license header in every source file
- Python minimum: 3.10 — use `str | None` union syntax, not `Optional`
- TypeScript target: ES2020, Node 18+; `strict: true`
- Python SDK passes `mypy --strict` with zero errors
- TypeScript SDK passes `tsc --noEmit` with zero errors
- No global state in either SDK — multiple `App` instances can coexist in the same process
- Both SDKs use the W3C wire protocol only — no custom protocol endpoints (except `automancer/element/:id/provider`)
- `find(xpath="//Button[@Name='OK']")` maps to `automancer:xpath`, NOT to the W3C `xpath` strategy — document this distinction in docstrings so users who migrate from WinAppDriver/Selenium understand the difference
- `find(runtime_id="42.333896.3.1")` maps to the `id` strategy — this is a UIA RuntimeId, not a DOM id or element-6066 reference
- Window management methods (`window_size`, `set_window_size`, `maximize`) operate on the session's root window; they are not element-scoped
