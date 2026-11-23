# Changelog

All notable changes to AutoMancer are documented here, described by what you'll notice rather than by API name; the [README](README.md) has the details. Entries are grouped by roadmap stage (see [docs/roadmap-spec.md](docs/roadmap-spec.md)) rather than by semantic version — Phase 1 shipped as `v0.1.0`, and Phase 2 work lands under `Unreleased` until the **v1** milestone, reached when Phase 2 (stages 2.1–2.8) is complete.

## Unreleased

### CLI Parity — The Command Line Catches Up with the Engine

#### Added

- **A much bigger command line** — besides `launch`, `find`, `tree`, `click` and `type`, `automancer` can now press keys and combinations (`key`, `hotkey`), hover over or focus an element, save a screenshot, and move, resize, minimize, maximize, close or kill the window (`window`). See the [CLI README](src/AutoMancer.Cli/README.md).
- **Checks you can script** — `assert` confirms that an element exists, is enabled, visible, clickable or unobscured, has a given name, text, ID, class or control type (exactly, by a phrase it contains, or by pattern), that a number of elements match, that the window has stopped changing, or that two screenshots match within a tolerance. Every check you give has to hold, and a failure exits with code 1 and shows what was expected next to what was found. It retries until its `--timeout`, so it's also how a script waits for something to appear, go away, or settle.
- **Output a program can read** — add `--json` to any command and you get exactly one JSON object back, including when the command line itself is wrong. Exit codes now say what kind of problem it was: 1 when the app ran but your expectation wasn't met, 2 for a bad command, 3 when there's no app to act on, and 4 when something broke.
- **Search inside an element** — `--within-by` and `--within-value` limit a lookup to one element's subtree, so a name that repeats across the app (a dozen buttons, an `OK` in every dialog) no longer needs a fragile path. `tree` uses the same option to print just one subtree. Path and XPath lookups can't be scoped, since they always search from the window root, so combining them with a scope is refused.
- **A shorter `tree`** — `--depth` and `--interactive-only` keep what comes back to the controls you can act on, instead of the whole UI Automation tree.
- **`configure`** — shows the current target and the saved settings, attaches to a running app by PID, process name or window title, and saves engine settings such as the implicit wait and the pause after each action.
- **A quiet CLI** — the engine's trace log no longer prints before every result; `--verbose` brings it back.

#### Changed

- **Apps are identified by process ID, not session ID** — `launch` prints the PID and makes that app the current target, so later commands don't need an ID at all, and `--pid` points a command at a different app. The session IDs, the `sessions` command and the session file in the temp folder are gone; what the CLI remembers now lives in one file under `%LOCALAPPDATA%\AutoMancer`.
- **`tree`'s `--root-by` and `--root-value` became `--within-by` and `--within-value`**, matching the scope option on every other command.
- **Acting on a disabled element exits with 1** — the element was there and the app was fine, it just couldn't take the action — where it used to exit with 4, which now means the tool itself broke.
- **A mistake in the command line prints one message** — and a hint at `--help` — to stderr, where it used to print the whole help text to stdout. With `--json` it's a JSON error instead. `--help`, `--version` and a bare `automancer` now succeed.

#### Fixed

- **A command that attached to an app killed it when the command finished.** Every command except `launch` did this.
- **Text with accents, CJK characters or emoji came out as question marks** in element names, typed text and file paths. Output is now UTF-8.
- **Running several commands at once could fail, or wipe the saved settings and target.** The file the CLI keeps between commands is now safe to use from overlapping commands.
- **A few inputs were mishandled instead of rejected.** An empty `--value` matched some arbitrary element, a `--tolerance` of NaN passed validation, trailing spaces in a check value were silently trimmed so the check could never match, and an empty `launch` or `configure --attach` argument was acted on. All of them are now handled.

### Stage 2.8 — Pattern Actions, Clipboard and Window Assertions

#### Added

- **Drive checkboxes, menus, lists, grids and sliders directly** — `app` can now flip a checkbox or switch, expand and collapse a menu, tree node or combo box, select and deselect list items and read what's selected, read a grid's size and fetch one cell, and set or read a slider's value, all through UI Automation instead of pixel-aimed clicks. Each does nothing when the control doesn't support it, and a control that has gone away raises `StaleElementError`, as other actions do.
- **Read and write the clipboard** — `GetClipboardTextAsync` and `SetClipboardTextAsync` handle text, Unicode included. Only the length of the text is ever logged.
- **Assertions about the whole window** — `Expect(app)` can now check the window's title, whether it is minimized, maximized or in front, its bounds, whether the process is running, responding or has exited, how many windows and dialogs are open, that the window has stopped changing, and that memory and handle use stay under a ceiling. They retry like every other assertion.
- **Examples** — `ExtendedPatternDemoTests` and `AppExpectDemoTests` in `samples/ConsumerNotepadTests`, and a small WPF test app in `fixtures/GridHost` that the selection, grid and range-value tests run against.

#### Fixed

- **A minimized-window test that failed about half the time** after a maximize/minimize test ran before it. `SetWindowStateAsync` returns before Windows has applied the state, so the test now waits for it.

### Stage 2.7 — Failure Screenshots

#### Added

- **A screenshot of what the engine was looking at when a find fails** — set `AnnotateFailureScreenshots` in `AppOptions` and a find that can't succeed saves a picture of the window to your temp folder, with a red box around the closest match (or around the whole window when nothing is close). The error message ends with the file's path, and `ElementNotFoundError.ScreenshotPath` holds it. Off by default. It covers finds through the normal lookup chain, not spatial finds or lookups inside a popup or second window.

### Stage 2.3 — Context Menus, Popups and Windows

#### Added

- **Right-click menus** — open an element's context menu by right-click, Shift+F10 or a click on a "⋯" button, see what it offers and which items are enabled, then click one; or open and click in one call. A failed click closes the menu, so it can't swallow your next input.
- **Native Windows menus** — classic Win32 menus are read straight from Windows when the accessibility tree can't see their items, with names matching what the accessibility tree reports, so one item name works either way.
- **Work inside any popup** — open a dropdown, tooltip, flyout or menu through the click, right-click, key or hover that opens it, and get back something that searches and acts only inside that popup. You say what the popup is or contains, so the right one is picked. Every action, wait and assertion works there as it does on the app, and the popup can be closed, checked for being open, or waited on to close by itself. Once it closes, using it again says so instead of acting on the app behind it. See [Popups, Context Menus and Windows](README.md#popups-context-menus-and-windows).
- **Work inside a dialog or second window** — wait for a window of the app by its title, then search and act inside it alone. Closing your handle on it never closes the app.
- **Popups drawn inside the app's own window** are found too, not just ones with a window of their own: VS Code's menus and hovers, and Paint's in-window dialogs.

#### Changed

- **Simulated clicks wait for the element to stop moving** — a click, hover, double-click or drag now checks that the element isn't still sliding into place (like a dropdown that's opening), so it can't miss and land beside it. Forced actions skip this.
- **Removed: the old way to find a dialog** — it returned a separate app object for the dialog, which killed the app when disposed. Wait for the dialog's window instead (see above).

#### Fixed

- Disposing an app object made from another one, for different options or a dialog, killed the app and closed the session the original was still using.

### Stage 2.2 — Wait and Resilience Primitives

#### Added

- **Ready-made wait conditions** — common things to wait for (an element becoming visible or enabled, its name or text matching, its ID, class, or control type matching, or the opposite of any of these) no longer need a hand-written check. "Visible" and "enabled" use the same rules actions check before acting, so a wait and the action that follows it agree on what counts as usable.
- **Force an action past the usability check** — `app.Force` performs the same actions on an element even when it's disabled or hidden, without waiting, for when acting on something no user could reach is intentional (e.g. invoking a menu command without opening its menu). It also skips the keyboard focus check, so simulated typing goes ahead even when the element doesn't take focus. It still reports gone elements and still needs a real on-screen point for simulated clicks. See [Forcing an action](README.md#forcing-an-action).
- **More test assertions** — check that an element is enabled, that its text contains a phrase, or that exactly a given number of elements match. The count check waits for a list that's still filling in, instead of counting whatever is there at that moment. See [TESTING.md](TESTING.md#assertions-reference).
- **Check whether something is in the way** — `IsUnobscuredAsync` reports whether a click on an element would actually reach it right now, or would land on another window or an in-app overlay such as a dialog's backdrop. It's a check you call yourself; actions don't run it. In tests, `Expect(locator).ToBeObscuredAsync()` and `ToBeUnobscuredAsync()` wait for the answer you expect, since overlays fading in or out can briefly report the old state. See [Checking for something in the way](README.md#checking-for-something-in-the-way).
- **Automatic recovery when you search by description** — when an element you described (by name, ID, position, and so on) turns out to be gone, AutoMancer looks it up again and carries on. This includes the container a scoped search runs in, so a dialog that closed and reopened is searched fresh. To handle this yourself instead, turn off the `ReresolveOnStale` option and you'll get a stale-element error. See [Locators vs. Element Handles](README.md#locators-vs-element-handles).

#### Changed

- **Actions wait for elements to be usable, checked at the moment of acting** — clicks, typing, clearing, hovering, and wheel scrolling now check that the element is enabled and visible right then, rather than trusting what was true when it was found, and wait up to the usual timeout for it to become usable. Previously, clicking, typing, or clearing could succeed on a hidden or disabled element through the app's accessibility interface — doing something no user could — because only the fallback to simulated mouse and keyboard input was checked, and an element kept from earlier (e.g. an item in a menu that has since closed) was judged by its old state. Clicks also aim at where the element is now, not where it was when found.
- **Simulated typing goes to the element you named** — when typing or clearing falls back to simulated keystrokes, the element now gets keyboard focus first, and AutoMancer checks it took focus before sending anything. Previously the keystrokes went to whichever control already had focus, so text could land in the wrong box. An element that won't take focus is now waited on and then reported, instead of the text going elsewhere.

- **The visibility assertion uses the same rule as actions** — an element the app reports as offscreen now fails it, even if it has an on-screen size. Previously only its size was checked, so it could pass for an element an action would refuse. Failed assertions now also say whether the element was enabled and offscreen.
- **Gone elements are reported, not silently mishandled** — acting on or reading an element that has left the UI now fails with a clear "stale element" error. Previously a click could land wherever the element used to be, a value read could come back empty, and a search inside a container that vanished partway through looked the same as "nothing matched." This covers elements from a closed dialog that Windows itself doesn't report as gone (e.g. Notepad's Go to line), and the brief moment after an app exits.
- **An element you already found and kept** now fails with the stale-element error if it's gone, instead of acting on its old position or doing nothing. Nothing can look it up again for you, since it points at one specific element.
- **The scrolling search for long lists** now takes a description of the list rather than a list you already found, so it recovers if the list is rebuilt mid-scroll. You can still pin one specific list by its runtime ID.
- **With automatic recovery turned off**, a scoped search whose container disappears now fails right away with the stale-element error, instead of waiting out the full timeout and reporting "not found." A search for all matches never waited, so it previously came back as an empty list instead.

#### Fixed

- Searching for all matches inside a container that had disappeared could crash with an internal error under the `uia3` provider.
- Setting focus and scrolling into view silently did nothing for elements found by the `uia2` provider; they now work. Setting focus also works for elements found by the `win32` provider.
- Actions on elements found by the `win32` provider never brought their window to the front, so simulated clicks and keystrokes could go to whatever app was in front instead.
- Scrolling into view no longer logs "Scrolled into view" when the element doesn't support it and nothing scrolled; the log now says which happened.

### Stage 2.1 — Locator Power Tools

#### Added

Five ways to find elements that couldn't be reached before — no existing behavior changed.

- **Find by position** — locate an element by where it sits relative to one you can already find (above, below, left of, right of, or near), for controls with no reliable name or ID.
- **Find by any property** — match on any built-in UI Automation property, not just the usual name and ID, with named shortcuts for common ones like help text and item status.
- **Find by an app's custom properties** — use properties that an app registers for itself, alongside the built-in ones.
- **Search within part of a window** — restrict a search to one dialog or panel, so identically named elements elsewhere (e.g. two "Cancel" buttons in different dialogs) don't get in the way.
- **Find items in long, scrolling lists** — locate items that don't exist in the UI until they're scrolled into view; AutoMancer scrolls and checks until it finds them, and stops early once the list stops moving instead of scrolling indefinitely.

## v0.1.0 - 2026-09-07

Phase 1 complete: the core engine, CLI, and test adapter layer (`AutoMancer.Testing`, `AutoMancer.Testing.XUnit`) — the proof-of-concept-through-"a C# consumer has everything they need" arc. See [docs/roadmap-spec.md](docs/roadmap-spec.md) for the full stage-by-stage history.
