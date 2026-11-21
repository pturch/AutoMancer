# AutoMancer CLI

AutoMancer's CLI offers the same behaviors that the engine does. Each step is its own command that prints plain text (or a JSON object) and an exit code, so it composes into shell scripts, CI steps, and AI agents the same way it does for a person at a prompt. It's a thin wrapper over the engine, so every command maps onto one or two `App` methods and behaves the way the library does.

**For the engine's API, see the [main README](../../README.md); for writing tests in C#, see [TESTING.md](../../TESTING.md).** 

## Run it

```bash
dotnet build AutoMancer.slnx
src/AutoMancer.Cli/bin/Debug/<net-version>/automancer.exe <command> [options]
```

The CLI sends mouse and keyboard inputs to the desktop and brings the target window to the front, so you may see some issues if you're trying to model an uninterrupted workflow while... interrupting it by entering commands in a terminal. Use powershell or batch or another script type if you want commands to run in a... batch.

Here's an example workflow:

```bash
automancer launch notepad.exe                                         # starts it and makes it the current target
automancer tree --interactive-only --depth 1                          # what can I act on?
automancer type "hello world" --by control --value Document           # types "hello world" into the notepad
automancer assert --by control --value Document --check "text~hello"  # asserts that the notepad contains the text "hello"
automancer screenshot --out after.png                                 # takes a screenshot of the final state of the app and saves it as a file
```

## Launching and targeting an app

Every command acts on the application identified by it's process ID. You can pass `--pid <n>` to name one, or use the *current target* by excluding it, which `launch` and `configure --attach` set.

Working with two apps at once is just a matter of naming each one:

```bash
automancer --pid 18276 assert --by control --value Document --check enabled
automancer --pid 31608 tree --depth 1
```

### Attaching to something already running

`configure --attach` points the current target at an app that's already open. A number is a PID; otherwise the text is matched against process names first (case-insensitive, with or without `.exe`), and then against window titles:

```bash
automancer configure --attach notepad          # a process name
automancer configure --attach 18276            # a PID
automancer configure --attach "Untitled"       # part of a window title
```

If the text matches several apps, you'll get an error that lists them so you can pick a PID. 

**Packaged apps like Calculator and Settings need this route.** `launch calc.exe` can't follow the launcher stub to the real window. After launching, attach to it by title (`configure --attach Calculator`). The target is then the app's window host, `ApplicationFrameHost`, which is what actually owns the window.

## Global options

These work on every command:

| Option | What it does |
|---|---|
| `--json` | Prints one JSON object on stdout instead of text; see [Output and exit codes](#output-and-exit-codes) |
| `--pid <n>` | Names the app to act on, instead of the current target |
| `--verbose` | Shows the engine's trace log on stderr; without it only the engine's warnings appear |
| `--help`, `-h`, `-?` | Help for a command, or for the whole CLI at the root |
| `--version` | Prints the version |

## Commands

Every command is listed here under the group it belongs to, with a link to its section. Each section starts with the command line you'd type, then explains its options; the [global options](#global-options) work on all of them. Commands that act on an element share the same [element options](#element-options).

**[Starting and managing an app](#starting-and-managing-an-app)**

- [`launch`](#launch) — starts an app and makes it the current target
- [`configure`](#configure) — shows or changes the current target and the saved engine settings
- [`window`](#window) — shows, moves, resizes, minimizes, maximizes, closes or kills the window

**[Looking at the UI](#looking-at-the-ui)**

- [`find`](#find) — resolves one element and prints its properties
- [`tree`](#tree) — prints the element tree
- [`screenshot`](#screenshot) — saves the window, or one element, as a PNG

**[Acting on the app](#acting-on-the-app)**

- [`click`](#click) — clicks an element
- [`type`](#type) — types text into an element
- [`key`](#key) — presses one key
- [`hotkey`](#hotkey) — presses a combination of keys
- [`hover`](#hover) — moves the pointer over an element without clicking
- [`focus`](#focus) — gives an element keyboard focus

**[Checking things](#checking-things)**

- [`assert`](#assert) — checks things about elements, the window, or two images, and doubles as the CLI's wait

---

## Starting and managing an app

These commands cover the life of the app: start it, point the CLI at it, and move, minimize or close its window.

### launch

`launch` starts an app, makes it the current target, and prints its PID. It's the one command that doesn't need a target, since it creates one. To work with an app that's already open, use [`configure --attach`](#attaching-to-something-already-running) instead.

```
automancer launch <executable> [--args <text>]
```

- `<executable>` — the path or name of the program to start
- `--args <text>` — command-line arguments, passed straight through to the process

For example:

```bash
automancer launch "C:\Windows\System32\notepad.exe"
automancer launch "C:\...\Code.exe" --args "--new-window C:\my-project"
```

---

### configure

`configure` shows or changes what the CLI remembers: the current target, and a handful of engine settings. With no options (or with `--show`) it shows them, along with where each value came from.

```
automancer configure [--show] [--attach <pid, name or title>] [--clear] [--reset]
                     [--implicit-wait <ms>] [--poll-interval <ms>] [--action-delay <ms>]
                     [--foreground-timeout <ms>] [--reresolve-on-stale <true|false>]
                     [--provider-chain <list>]
```

- `--show` — shows the current target and the settings 
- `--attach <pid, name or title>` — attaches to a running app and makes it the current target
- `--clear` — forgets the current target
- `--reset` — puts every saved setting back to the engine default
- `--implicit-wait <ms>` — how long a find waits for an element to appear (default 5000)
- `--poll-interval <ms>` — how often the engine re-checks while it waits (default 500)
- `--action-delay <ms>` — a pause after each action so the UI can settle (default 150)
- `--foreground-timeout <ms>` — how long to keep trying to bring a window to the front (default 3000)
- `--reresolve-on-stale <true|false>` — whether to re-find an element that went stale mid-action (default true)
- `--provider-chain <list>` — which providers to try, in order (default `uia3`, `uia2` and `win32`)

The settings view looks like this:

```
> automancer configure
Target: PID 40120 (Notepad), running
Settings:
  implicit-wait        5000               (default)
  poll-interval        500                (default)
  ...
```

A setting resolves in this order: a per-call flag such as `assert --timeout`, then the saved setting, then the engine default. Saved settings stay until you `--reset` them.

This data persists in the `%LOCALAPPDATA%\AutoMancer\state.json` file, which is written automatically. All CLI commands share it, so to run instances in parallel, give each its own `--pid` to keep them apart.

---

### window

`window` shows or changes the app's window. With no option it prints the window's position and size, or `minimized`. Otherwise it does one of the things below, and only one per call. `--move`, `--resize` and `--state` print the window's position and size afterward, so a script can confirm the change.

```
automancer window [--move <x,y> | --resize <w,h> | --state <normal|max|min> | --close | --kill]
```

- `--move <x,y>` — moves the top-left corner to that screen position (`--move -1200,40`)
- `--resize <w,h>` — resizes the window to that width and height in pixels
- `--state <normal|max|min>` — restores, maximizes, or minimizes it
- `--close` — closes the window, like clicking its X, and waits up to three seconds. The output reports if the app is still running (it may be showing a save dialog)
- `--kill` — terminates the app's process immediately and waits for it to exit

---

## Looking at the UI

These read the app and change nothing: one element's properties, the whole tree, or a picture.

### find

`find` resolves one element and prints its properties, including a `runtimeLocator` you can reuse for as long as the element lives.

```
automancer find --by <strategy> --value <text>
```

- `--by <strategy>` — how to look for the element: `name`, `id`, `class`, `control`, `runtime`, `path` or `xpath`; [Naming an element](#naming-an-element) explains what each one matches
- `--value <text>` — what to match, using that strategy

It also takes `--within-by` and `--within-value`; see the [element options](#element-options) for more information on scoping.

For example:

```
> automancer find --by control --value Document
Name:         Text editor
AutomationId:
ControlType:  Document
ClassName:    RichEditD2DPT
ResolvedVia:  uia3
Id:           42.54798418
Rect:         88, 173, 717x522
```

`ResolvedVia` says which provider found it, and `Id` is the runtime ID that `--by runtime` accepts.

---

### tree

`tree` prints the element tree as an indented list, each node showing its control type, its name, and its automation ID in brackets. An app's tree can be massive, so narrow it with scope, interactive filter, and depth.

```
automancer tree [--within-by <strategy> --within-value <text>] [--interactive-only] [--depth <n>]
```

- `--within-by <strategy>`, `--within-value <text>` — start the tree at one element instead of the window; see the [element options](#element-options)
- `--interactive-only` — keeps just the controls a user could act on (buttons, edits, list items, menu items, tabs, documents and so on)
- `--depth <n>` — stops n levels below each starting node; 0 means the starting nodes only

For example:

```
> automancer tree --interactive-only --depth 1
Document  "Text editor"
TabItem  "Untitled. Unmodified."
  Button  "Close Tab"  [CloseButton]
Button  "Add New Tab"  [AddButton]
MenuItem  "File"  [File]
...
```

---

### screenshot

`screenshot` saves the window as a PNG, or just one element if you name it, cropped out of the window (an element that's outside the window can't be captured). The folders in the path are created if they don't exist, and the result prints the absolute path. Two screenshots and an [`assert --expected --actual`](#assert) are how you compare screens.

```
automancer screenshot --out <file> [--by <strategy> --value <text>]
```

- `--out <file>` — where to write the PNG
- `--by <strategy>`, `--value <text>` — capture just this element; give both or neither

Also takes `--within-by` and `--within-value`; see the [element options](#element-options).

---

## Acting on the app

These send real mouse and keyboard input, either to a named element or to whichever control has focus.

### click

`click` clicks an element. When the control offers UI Automation's Invoke action it uses that, and otherwise it sends a real mouse click at the element's center.

```
automancer click --by <strategy> --value <text> [--double] [--right]
```

- `--double` — sends a double-click instead of a single click
- `--right` — sends a right-click instead of a left click

Also takes `--within-by` and `--within-value`; see the [element options](#element-options).

For example:

```bash
automancer click --by name --value "Add New Tab"
automancer click --by control --value Document --right
```

---

### type

`type` types text into an element. Where the control supports it, the text *replaces* the element's whole value instead of being typed at the caret, so typing into a field that already has something in it doesn't append. 

Any text works, including accents, CJK characters and emoji. A text that starts with `-` is fine (`type "-5" ...`), but a literal `--help` is read as the option.

```
automancer type <text> --by <strategy> --value <text>
```

- `<text>` — the text to type

Also takes `--within-by` and `--within-value`; see the [element options](#element-options).

---

### key

`key` presses one key and sends it to the window, so it goes to whichever control has focus; [`focus`](#focus) an element first to aim it. A key is a name like `Enter`, `Tab`, `Escape`, `F5`, `A`, `Left` or `PageDown` (any case), a digit `0` to `9`, or an alias: `esc`, `return`, `del`, `ins`, `pgup`, `pgdn`. The output echoes the key the way you could type it back, so `key 7` prints `Pressed 7.`

```
automancer key <key>
```

- `<key>` — the key to press; there are no other options

---

### hotkey

`hotkey` presses a combination: keys joined with `+`, with the modifiers (`ctrl`, `shift`, `alt`, `win`) held while the rest are pressed together. `ctrl+a`, `ctrl+shift+s` and a chord with several keys such as `ctrl+a+k` all work. Like `key`, it goes to the window. A chord needs at least one key that isn't a modifier.

```
automancer hotkey <chord>
```

- `<chord>` — the combination to press; there are no other options

---

### hover

`hover` moves the pointer to the center of an element without clicking, which is how you trigger a tooltip or a hover state. Check for the result with [`assert`](#assert), for example `assert --by control --value ToolTip`.

```
automancer hover --by <strategy> --value <text>
```

Also takes `--within-by` and `--within-value`; see the [element options](#element-options).

---

### focus

`focus` gives an element keyboard focus, which is what you want before a `key` or `hotkey` that should land on it.

```
automancer focus --by <strategy> --value <text>
```

Also takes `--within-by` and `--within-value`; see the [element options](#element-options).

---

## Checking things

This is the command that decides pass or fail, and it's also how a script waits.

### assert

`assert` checks that something is true and exits 1 with a report of what it expected and what it found when it isn't. Give it a locator and any number of `--check` expressions; every one has to hold. With a locator and no `--check`, it asserts that the element exists.

```
automancer assert [--by <strategy> --value <text>] [--check <expression>]... [--timeout <ms>]
                  [--expected <file> --actual <file> [--tolerance <fraction>]]
```

- `--by <strategy>`, `--value <text>` — which element to check; give both or neither. Without them, only `stable` and the image comparison are allowed
- `--check <expression>` — one thing to assert; repeat it for several, and all of them must hold
- `--timeout <ms>` — the total time for the whole call (default: the saved implicit wait, 5000 ms)
- `--expected <file>`, `--actual <file>` — two PNG files to compare; give both or neither, and no running app is needed
- `--tolerance <fraction>` — how much of the two images may differ, from 0 to 1 (default 0.01)

Also takes `--within-by` and `--within-value`; see the [element options](#element-options).

For example:

```bash
automancer assert --by name --value Save --check enabled --check visible --check "name=Save"
automancer assert --by control --value ListItem --check count=3
automancer assert --by name --value "Error" --check "!exists" --timeout 3000
automancer assert --check stable
automancer assert --expected expected.png --actual actual.png --tolerance 0.02
```

A check is written as a name (it must be true), `!name` or `name=false` (it must not be), `name=value` (an exact match), `name~value` (contains), or `name=~regex`. Comparisons are case-sensitive, and in bash anything with a `!` needs quoting.

| Check | What it asserts |
|---|---|
| `exists`, `!exists` | The element is found, or is not present |
| `enabled`, `visible` | The engine's own rules: enabled means not reported disabled, and visible means not offscreen and having some area |
| `unobscured` | One thing only: no other window covers the element's center. It says nothing about whether the element is enabled, so a greyed-out button in plain view is `unobscured` |
| `clickable` | All three at once: `enabled` and `visible` and `unobscured`. Use it to ask "would a click land and do something?"; a failure prints the three results (`enabled=True visible=True unobscured=False`) so you can see which one failed |
| `name`, `text`, `automation-id`, `class-name`, `control-type` | A text property compared with `=`, `~` or `=~`; `text` reads the element's value |
| `count=N` | How many elements match, inside the scope if you gave one; `0` is fine |
| `stable` | The window, or the element, has stopped visibly changing |

`!exists` can't be combined with other checks, and without a locator only `stable` and the image comparison are allowed.

**`--timeout` is the total time for the whole call**, and it defaults to the saved implicit wait (5000 ms). It covers waiting for the element to appear, and the checks re-run every 100 ms until they pass or the time runs out. `--timeout 0` makes a single pass with no waiting. That's what makes `assert` the CLI's wait: `assert --by name --value Dialog --timeout 10000` waits for a dialog to appear, `assert ... --check "!exists"` waits for it to go away, and `assert --check stable` waits for the window to settle.

**Comparing screens takes two captures and one assert.** `--expected` and `--actual` aren't checks; they name two PNG files that may differ by at most `--tolerance`, a fraction that defaults to 0.01. The comparison needs no running app, and images of different sizes fail with the dimensions spelled out. It runs once with no retry, so settle the UI first:

```bash
automancer screenshot --out expected.png      # earlier
automancer screenshot --out actual.png        # later
automancer assert --expected expected.png --actual actual.png
```

---

## Element options

Every command that acts on an element finds it the same way, with `--by` and `--value`, and can narrow where it looks with `--within-by` and `--within-value`.

### Naming an element

Commands that act on an element take `--by <strategy> --value <text>`. The strategies are the same ones the library's `Locator` has:

| `--by` | Matches |
|---|---|
| `name` | The visible or accessible name |
| `id` | The developer-assigned automation ID, which is stable across relaunches |
| `class` | The Win32 class name |
| `control` | The UIA control type (`Button`, `Edit`, `Document`, …) |
| `runtime` | The `id` an earlier `find` or `tree` printed. It's only good while that element lives |
| `path` | A position in the tree, like `Window > Pane[2] > MenuBar > MenuItem["File"]` |
| `xpath` | XPath over the tree, like `//MenuItem[@Name='File']` |

An empty `--value` is rejected, because an unset shell variable would otherwise quietly match some arbitrary element. The [Locator Reference](../../README.md#locator-reference) in the main README covers the strategies in depth. Names match exactly and case-sensitively, so Notepad's editor is `Text editor`, not `Text Editor`.

### Looking inside one element

A name or control type often repeats across an app, so `--within-by` and `--within-value` (both or neither) limit the search to another element's subtree. It works on every command that takes `--by`/`--value`, and on `tree` it sets where the printout starts:

```bash
# In Notepad: four MenuItems in the window, three of them in the menu bar (File, Edit, View)
automancer assert --by control --value MenuItem --check count=3 --within-by id --within-value MenuBar
automancer find --by control --value MenuItem --within-by id --within-value MenuBar
automancer tree --within-by id --within-value MenuBar
```

If the scope matches several elements the first one is used, and a scope that matches nothing is an element-not-found error.

`--by xpath` and `--by path` can't be combined with a scopesince the engine always searches those two from the window root and would ignore it, so the CLI refuses the combination instead of searching outside the scope you asked for. The window itself can't be a scope, because a search never includes the window it starts from.

## Output and exit codes

Without `--json`, a result is plain text on stdout and an error is one message on stderr. With `--json`, **every** command writes exactly one JSON object to stdout and nothing else, including when the command line itself is wrong. The engine's trace log, if you turned it on, goes to stderr:

```json
{"ok":true,"data":{"passed":true,"checks":[{"check":"exists","expected":true,"actual":true,"passed":true,"detail":null}]}}
{"ok":false,"error":{"code":"assertion_failed","message":"Assertion failed: name: expected \"Nope\", actual \"Text editor\"","details":{"checks":[ ... ]}}}
```

`ok` is `true` exactly when the exit code is 0. `error.details` is `null` except for assertion failures, where it lists every check that ran.

| Exit | Meaning | JSON `error.code` |
|---|---|---|
| 0 | Success | |
| 1 | The app ran fine, but an expectation wasn't met | `assertion_failed`, `element_not_found`, `still_present`, `condition_timeout`, `not_interactable`, `window_minimized`, `timeout` |
| 2 | The command line was wrong: a syntax error, an unknown option or value, or an invalid combination | `usage` |
| 3 | There was no app to act on: no target, the target has exited, or the PID doesn't exist or has no window | `target_not_found` |
| 4 | The tool or the environment broke: a failed launch, a missing file, anything unexpected | `error` |

The split between 1 and 4 is the one scripts and agents lean on: **1 means "the thing you checked isn't the case"**, and **4 means "something went wrong while checking"**.

What each command puts in `data`:

| Command | `data` |
|---|---|
| `launch` | `pid`, `path` |
| `configure` | `target` (`pid`, `name`, `alive`, or `null`) and `settings` (each with a `value` and a `source`) |
| `find` | `name`, `automationId`, `controlType`, `className`, `resolvedVia`, `id`, `rect`, `locator`, `scope`, `runtimeLocator` |
| `tree` | `roots`, nested through `children`, with `id`, `name`, `automationId`, `className`, `controlType` and `boundingRect` on each node |
| `click`, `hover`, `focus` | `clicked`, `hovered`, `focused` |
| `type` | `typed`, the number of characters |
| `key`, `hotkey` | `key`; or `chord`, `modifiers` and `keys` |
| `screenshot` | `path`, made absolute |
| `window` | `rect` and `minimized`; for `--close`, `closeRequested`, `exited`, `pid` and `targetCleared`; for `--kill`, `killed`, `pid` and `targetCleared` |
| `assert` | `passed` and `checks` |

## Scripting tips

**Check the exit code.** A failed `assert` is how a script stops, and `assert --timeout` is how a script waits before it moves on.

**Quote what your shell would eat.** That's `'!exists'` in bash, and regexes containing `^` or `$` in PowerShell. A negative number needs no quoting (`window --move -1200,40`).

**Text is UTF-8.** Names, typed text and paths with accents, CJK characters or emoji round-trip, and `--json` escapes them.

**Paths can be relative.** `--out`, `--expected` and `--actual` resolve against the current folder, and `--out` creates any folders it needs.

**Find a menu item's exact name with `tree`.** Open the menu with `click --right`, run `tree` to see the item names, then `click` the item by name. The menu stays open between calls, so there's no separate context-menu command.

**Don't run two commands against the same app at once** unless you mean to. They share one real mouse and keyboard.

## Known limits

- `launch` can attach to a window that's already open, for an app that only allows one instance (Windows 11 Notepad does this). The CLI only mentions it with `--verbose`.
- `window --kill` ends one process and not its children, so an Electron app like VS Code can leave helper processes behind. If the target is a shared window host, which is what a packaged app's window belongs to, killing it closes every packaged app's window.
- The window itself can't be found as a scope or by runtime ID, so a `count` never includes it.
- A literal `--help` can't be typed with `type`; it's read as the option.
