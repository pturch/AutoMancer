# AutoMancer SDKs Implementation Plan

> **For agentic workers:** Steps use checkbox (`- [ ]`) syntax for tracking progress through this plan.
>
> **NEVER run git commands (add, commit, push) automatically.** All version control is the developer's responsibility. Bash blocks in this document are implementation reference — execute the build/test lines only, never the git lines.

**Phase:** 3 of 3 — client SDKs built on top of the daemon. No engine or daemon source files are modified. See [roadmap-spec.md](./roadmap-spec.md) for the current phase breakdown.

**Goal:** Build the Python and TypeScript client SDKs that talk to `automancerd` over HTTP using the W3C WebDriver wire protocol.

**Architecture:** Both SDKs are pure HTTP clients — no Windows code, no native dependencies. Each has a low-level `Session` class that handles the W3C wire protocol and error mapping, an `App` class for session lifecycle, an `Element` class for interactions, and control subclasses for type-specific operations.

**WinAppDriver lessons incorporated:** WinAppDriver required users to use the generic Selenium client library, which has browser-centric ergonomics (`driver.find_element(By.XPATH, "...")`, no `find(name=...)` shorthand). AutoMancer's custom SDKs provide a cleaner API and can expose Windows-specific capabilities that Selenium clients cannot. New additions: `runtime_id`/`runtimeId` locator kwarg (maps to the `id` strategy), and `window_size`/`set_window_size`/`maximize` on `App` (filling the gap WinAppDriver left).

**Tech Stack:** Python 3.10+, `httpx`, `pyproject.toml`, `mypy --strict`, `pytest` | TypeScript / Node 18+, native `fetch`, `tsconfig`, `tsc --noEmit`, `vitest`

**Test language boundary:** SDK tests are written in the SDK's own language (pytest for Python, vitest for TypeScript). C# end-to-end coverage of the daemon HTTP surface is handled in `AutoMancer.Daemon.Tests` (see [daemon-spec.md](./daemon-spec.md) Task 14). The SDK integration tests assume `automancerd` is already running.

**Prerequisite:** Phase 3 daemon complete (see [daemon-spec.md](./daemon-spec.md)) and `automancerd` reachable at `http://127.0.0.1:27272` before integration tests can pass. Phase 1 (engine + CLI) must be done first.

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

**What:** `build_locator` maps keyword arguments to the W3C `{"using":"...","value":"..."}` format — the single translation point between the Python API and the wire protocol. Supports all standard strategies plus `runtime_id` (maps to `id` strategy → RuntimeId lookup), `xpath` (maps to `automancer:xpath` → XPath on the UIA tree), `text` (maps to `automancer:text` → OCR), and `image` (maps to `automancer:image` → template match against a base64 PNG). `Session` is the low-level HTTP client: it wraps every HTTP response, extracts the `value` field, and raises the correct Python exception when the W3C error shape appears.

**`text`/`image` depend on Phase 2's Visual Provider (roadmap-spec.md Stage 2.6) and the daemon's `automancer:resolverChain` wiring (daemon-spec.md Task 15) — both must have shipped, and the session must opt into the visual chain, before `find(text=...)`/`find(image=...)` resolve anything. Implement the kwargs here regardless (they're pure request-shape mappings); their integration tests are gated the same way daemon-spec.md's Task 15 is.**

**Creates:**
- `sdk/python/automancer/locator.py` — `build_locator(*, name, automation_id, control, path, runtime_id, xpath, text, image, class_name)` → `dict`; raises `ValueError` if no strategy given
- `sdk/python/automancer/session.py` — `Session.create(capabilities)`, find, click, double_click, right_click, hover, type, clear, drag, scroll_wheel, hotkey, key_down, key_up, get_text, get_enabled, get_rect, screenshot, get_clipboard/set_clipboard, `_unwrap` maps W3C errors to Python exceptions
- `sdk/python/tests/test_locator.py` — all nine strategy kwargs, no-kwarg raises


- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
cd sdk/python && pytest tests/test_locator.py -v
```

**Done when:** 9 locator tests pass.

---

### Task 3: Python App and Element classes

**What:** `App` is the user-facing entry point: `App.launch("notepad.exe")` creates a session, `app.find(name="Submit")` finds an element. `Element` exposes properties (`text`, `enabled`, `visible`, `rect`) and actions (`click`, `double_click`, `right_click`, `hover`, `type`, `clear`, `drag`, `scroll_wheel`) by delegating to `Session`. `App` supports the context manager protocol for automatic cleanup, plus keyboard methods that aren't element-scoped (`hotkey(modifiers, keys)`, `key_down(key)`, `key_up(key)`). Also adds window management missing from WinAppDriver: `window_size()` → `(width, height)`, `set_window_size(w, h)`, `maximize()`, `minimize()`, `restore()`, `close()`.

**Creates:**
- `sdk/python/automancer/element.py` — `Element` class; `Rect` frozen dataclass; `find`/`find_all` for scoped search; `click`/`double_click`/`right_click`/`hover`/`type`/`clear`/`drag`/`scroll_wheel`
- `sdk/python/automancer/app.py` — `App.launch`, `App.attach`; `find`, `find_all`, `wait_until_gone`, `wait_until`, `screenshot`, `close`; `hotkey`, `key_down`, `key_up`; `window_size`, `set_window_size`, `maximize`, `minimize`, `restore`; `__enter__`/`__exit__`


- [ ] **Implement and type-check**

```bash
cd sdk/python && mypy automancer/ --strict
```

**Done when:** `mypy --strict` reports no issues.

---

### Task 4: Python clipboard, pattern, and grid methods

**What:** Thin wrappers over the daemon's `windows:*` extension endpoints (daemon-spec.md Task 13) — not W3C-standard, so kept separate from the core `Element`/`App` surface rather than implied to be portable to another WebDriver-compatible server. `App.get_clipboard()`/`set_clipboard(text)` read/write the system clipboard. `Element` gains `expand()`, `collapse()`, `toggle()`, `select()`, `add_to_selection()`, `remove_from_selection()`, `all_selected_items()`, and grid access — `row_count()`, `column_count()`, `cell(row, col)` → `Element`. **Depends on Phase 2 Stage 2.8 having shipped `ClipboardAction`/`ToggleAction`/`ExpandCollapseAction`/`SelectionAction`/`GridAction` and daemon-spec.md Task 13 — skip or defer this task entirely if Phase 2 hasn't reached that stage yet.** This is what `ComboBox.select`/`options` and `DataGrid.rows`/`cell` (Task 5) actually delegate to.

**Creates:**
- `sdk/python/automancer/app.py` — extend with `get_clipboard()`, `set_clipboard(text)`
- `sdk/python/automancer/element.py` — extend with `expand()`, `collapse()`, `toggle()`, `select()`, `add_to_selection()`, `remove_from_selection()`, `all_selected_items()`, `row_count()`, `column_count()`, `cell(row, col)`


- [ ] **Implement and type-check**

```bash
cd sdk/python && mypy automancer/ --strict
```

**Done when:** `mypy --strict` reports no issues.

---

### Task 5: Python control subclasses

**What:** Thin subclasses of `Element` that add control-type-specific methods: `TextBox.set_value`, `ComboBox.select`/`options` (delegating to Task 4's `select()`/`toggle()`/`all_selected_items()`), `DataGrid.rows`/`cell` (delegating to Task 4's `row_count()`/`column_count()`/`cell()`). They're returned by `find()` when the daemon reports the matching control type.

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

### Task 6: Python integration tests

**What:** End-to-end tests against a live daemon. Covers launch+find+type, file menu click, closest-match error, attach by PID, screenshot PNG verification, a double-click/drag round trip, and a clipboard round trip.

**Creates:**
- `sdk/python/tests/integration/test_notepad.py` — `@pytest.mark.integration`; 7 tests

Extends `pyproject.toml` with the `integration` marker definition.


- [ ] **Run integration tests (daemon must be running)**

```bash
cd sdk/python && pytest tests/integration/ -v -m integration
mypy automancer/ --strict
```

**Done when:** 7 integration tests pass; mypy clean.

---

### Task 7: TypeScript SDK scaffold

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

### Task 8: TypeScript types, Locator, and Session

**What:** `types.ts` defines the public interfaces (`Locator`, `LaunchOptions`, `AttachOptions`, `Rect`). `buildLocator` is the single translation point from the typed `Locator` to the W3C wire format — adds `runtimeId` (maps to `id` strategy), `xpath` (maps to `automancer:xpath`), `text` (maps to `automancer:text`), and `image` (maps to `automancer:image`; `text`/`image` depend on Phase 2's Visual Provider and daemon-spec.md Task 15 — see the note on the Python SDK's Task 2). `Session` is the HTTP client: creates sessions, finds elements, interacts (click, doubleClick, rightClick, hover, type, clear, drag, scrollWheel, hotkey, keyDown, keyUp), reads properties, and maps W3C error responses to TypeScript exceptions. Also adds `getWindowSize()`, `setWindowSize(w, h)`, `maximize()`, `minimize()`, `restore()`, `close()`, `getClipboard()`/`setClipboard(text)` session methods.

**Creates:**
- `sdk/typescript/src/types.ts` — all public interfaces + `W3CLocator` internal type
- `sdk/typescript/src/Locator.ts` — `buildLocator(loc: Locator): W3CLocator`
- `sdk/typescript/src/Session.ts` — `Session.create`, all find/interact/property/window/clipboard methods, `_unwrap` error mapper
- `sdk/typescript/tests/locator.test.ts` — all nine strategy mappings; no-strategy throws


- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
cd sdk/typescript && npm test -- locator.test.ts
```

**Done when:** 9 locator tests pass.

---

### Task 9: TypeScript App and Element

**What:** `Element` exposes async getter properties (`text`, `enabled`, `visible`, `rect`) and async action methods (`click`, `doubleClick`, `rightClick`, `hover`, `type`, `clear`, `drag`, `scrollWheel`, `find`, `findAll`). `App` mirrors the Python SDK API: `App.launch`, `App.attach`, `find`, `findAll`, `waitUntilGone`, `screenshot`, `close`, plus non-element-scoped keyboard methods (`hotkey`, `keyDown`, `keyUp`). Adds window management filling the WinAppDriver gap: `windowSize()`, `setWindowSize(w, h)`, `maximize()`, `minimize()`, `restore()`.

**Creates:**
- `sdk/typescript/src/Element.ts` — async properties + actions; `find`/`findAll` for scoped search
- `sdk/typescript/src/App.ts` — `static async launch/attach`; find, findAll, waitUntilGone, screenshot, close; hotkey, keyDown, keyUp; windowSize, setWindowSize, maximize, minimize, restore


- [ ] **Implement and type-check**

```bash
cd sdk/typescript && npm run typecheck
```

**Done when:** `tsc --noEmit` reports `Found 0 errors`.

---

### Task 10: TypeScript clipboard, pattern, and grid methods

**What:** Mirrors the Python SDK's Task 4 — thin wrappers over the daemon's `windows:*` extension endpoints (daemon-spec.md Task 13), kept off the core `Element`/`App` surface since they aren't W3C-standard. `App.getClipboard()`/`setClipboard(text)`; `Element` gains `expand()`, `collapse()`, `toggle()`, `select()`, `addToSelection()`, `removeFromSelection()`, `allSelectedItems()`, and grid access — `rowCount()`, `columnCount()`, `cell(row, col)` → `Element`. **Depends on Phase 2 Stage 2.8 having shipped `ClipboardAction`/`ToggleAction`/`ExpandCollapseAction`/`SelectionAction`/`GridAction` and daemon-spec.md Task 13 — skip or defer this task entirely if Phase 2 hasn't reached that stage yet.** This is what `ComboBox.select`/`options` and `DataGrid.rows`/`cell` (Task 11) actually delegate to.

**Creates:**
- `sdk/typescript/src/App.ts` — extend with `getClipboard()`, `setClipboard(text)`
- `sdk/typescript/src/Element.ts` — extend with `expand()`, `collapse()`, `toggle()`, `select()`, `addToSelection()`, `removeFromSelection()`, `allSelectedItems()`, `rowCount()`, `columnCount()`, `cell(row, col)`


- [ ] **Implement and type-check**

```bash
cd sdk/typescript && npm run typecheck
```

**Done when:** `tsc --noEmit` reports `Found 0 errors`.

---

### Task 11: TypeScript controls and integration tests

**What:** Control subclasses mirror the Python SDK — `ComboBox.select`/`options` and `DataGrid.rows`/`cell` delegate to Task 10's pattern/grid methods. Integration tests verify the full stack: launch Notepad, type text, get closest-match error on typo, verify screenshot is a PNG buffer, a double-click/drag round trip, and a clipboard round trip.

**Creates:**
- `sdk/typescript/src/controls/Button.ts`
- `sdk/typescript/src/controls/TextBox.ts` — `value()`, `setValue(text)`
- `sdk/typescript/src/controls/ComboBox.ts` — `select(item)`, `options()`, `selectedItem()`
- `sdk/typescript/src/controls/DataGrid.ts` — `rows()`, `row(index)`, `cell(row, col)`, `rowCount()`
- `sdk/typescript/tests/integration/notepad.test.ts` — 5 integration tests; requires daemon


- [ ] **Implement, run integration tests, final typecheck**

```bash
cd sdk/typescript && npm test
npm run typecheck
```

**Done when:** 5 integration tests pass; `tsc --noEmit` reports `Found 0 errors`.

---

## Constraints

- Apache-2.0 license header in every source file
- Python minimum: 3.10 — use `str | None` union syntax, not `Optional`
- TypeScript target: ES2020, Node 18+; `strict: true`
- Python SDK passes `mypy --strict` with zero errors
- TypeScript SDK passes `tsc --noEmit` with zero errors
- No global state in either SDK — multiple `App` instances can coexist in the same process
- Both SDKs use the W3C wire protocol for the core surface, plus the daemon's namespaced extensions: `automancer/*` (provider info, scroll-to, PID, kill, snapshot) and `windows:*` (keyboard hold, clipboard, UIA patterns) — document in each method's docstring that it's an AutoMancer/Windows-specific extension, not portable to another WebDriver-compatible server
- `find(xpath="//Button[@Name='OK']")` maps to `automancer:xpath`, NOT to the W3C `xpath` strategy — document this distinction in docstrings so users who migrate from WinAppDriver/Selenium understand the difference
- `find(text="Submit Order")`/`find(image=<base64 PNG>)` map to `automancer:text`/`automancer:image` and only resolve once the session opts into the visual resolver chain — depends on Phase 2's Visual Provider and daemon-spec.md Task 15 having shipped; implement the kwargs/params regardless, but their integration tests are gated the same way
- `find(runtime_id="42.333896.3.1")` maps to the `id` strategy — this is a UIA RuntimeId, not a DOM id or element-6066 reference
- Window management methods (`window_size`, `set_window_size`, `maximize`) operate on the session's root window; they are not element-scoped
- Clipboard/pattern/grid methods (`get_clipboard`, `toggle`, `select`, `row_count`, `cell`, …) depend on Phase 2 Stage 2.8 and daemon-spec.md Task 13 having shipped — same skip-or-defer gating as the visual-locator kwargs above
