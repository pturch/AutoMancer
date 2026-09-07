# Automating Real Windows Apps

Practical lessons for driving a real, arbitrary Windows app with AutoMancer — as opposed to *how to use AutoMancer's testing API*, which is [TESTING.md](TESTING.md)'s job.

## Picking a locator strategy

Try them in this order:

1. **`ByAutomationId` / `ByName`** — works when the app actually sets them. Native Win32 common dialogs (Open, Save, folder pickers) almost always do, with numeric `AutomationId`s assigned from the dialog's resource IDs — e.g. the filename combo box is `1148` and the confirm button is `1` on every English-locale Open dialog. Those numeric IDs are the most stable locator you'll find, and don't depend on the display text on the button.
2. **`FindAllAsync(ByControlType(...))` + a stable ordering key** — for apps (or specific controls) that expose neither. Anonymous buttons and labels can be resolved this way, ordering by `BoundingRect.X` (or `Y`). It doesn't need to know the tree's nesting depth, so it tends to survive an app's UI being reshuffled between versions better than a path does.
3. **`ByPath` / `ByXPath`** — also reach anonymous elements (they don't require a `Name`), and are more precise when position alone is ambiguous. But read the next section before reaching for these against an unfamiliar app.

## The `ByPath`/`ByXPath` indexing gotcha

Both take a bracketed index per step, e.g. `"Window > Custom[0] > Custom[1] > Button[0]"`. That index is the element's position **among same-`ControlType` siblings**, not its position among all of that parent's children. If a parent's children are `Pane, Custom, Custom`, the second `Custom` is `Custom[1]`, not `Custom[2]`.

Get this wrong and you get `ElementNotFoundError` with no other clue — it looks exactly like the locator strategy can't reach the element at all. Before assuming a locator "doesn't work" against an app, double check the index is counted per-`ControlType`, not per-position.

## Native dialogs

`App.FindDialogAsync(ownerPid, titleContains)` finds a dialog by a substring of its title — it exists because a dialog is a separate top-level window with its own `HWND`, not part of the parent window's tree, and `Process.MainWindowTitle` doesn't update for it. Two things to know:

- The title substring must match the dialog that actually opens for the action you triggered — some apps expose more than one dialog for what looks like the same action (e.g. a native file picker vs. the app's own custom "Open" dialog). Match on the substring that's common to every dialog you're willing to accept, and narrow it only if you need to distinguish between them.
- `FindDialogAsync` can return `null` even when the dialog did open, if the triggering keystroke landed before the target window had taken OS focus. Wrap the keystroke in a short retry loop rather than a single long wait — it's cheaper and more robust.

## Single-instance apps vs. everything else

`IClassFixture<TFixture>` gives each test *class* its own fixture instance — including its own `App.LaunchAsync` call. For a single-instance app, launching a "second" instance just focuses the existing window, so this pattern is free.

For an app that allows multiple instances, each class independently declaring `IClassFixture<TFixture>` would launch a **separate process per class**, and — since xUnit class fixture setup for classes sharing a `[Collection(...)]` isn't guaranteed to serialize the way test *execution* is — those launches can end up racing for OS keyboard focus against each other, corrupting each other's `SendInput` calls in ways that look like random, unreproducible locator failures. The fix is `ICollectionFixture<TFixture>` on the `[CollectionDefinition]` instead: one shared instance, injected into every class's constructor in that collection. Default to `ICollectionFixture` unless you've confirmed the app is genuinely single-instance.

There's a second, unrelated single-instance gotcha at the `LaunchAsync` level itself, worth knowing even for a fully isolated launch (its own profile/user-data directory, say). `AppSession.LaunchAsync` actively scans for an already-running instance of the same executable and, if it finds one with a window, attaches to *that* instead of waiting for the one it just spawned — and kills the freshly-spawned process as redundant, on the assumption it's no longer needed. This exists to handle apps that legitimately hand off to an existing window (Notepad included), but it means an "isolated" launch option alone doesn't guarantee a genuinely new window if another instance of the same app happens to already be running elsewhere on the desktop — including, easy to overlook, the very app you're driving this automation *from*. Pass `WindowMatchOptions.RequireNewWindow = true` to opt out of that handoff and wait for your own spawned process's window specifically.

## Inferring state that has no UIA property

Not every piece of app state is exposed as a UIA property. Some apps have no boolean or enum property for state you'd expect to be exposed. The pattern that works: poll an observable proxy for the state (a label or value that changes over time) at least twice with a real delay between reads, and check whether it changed. A single read proves nothing, since you can't tell "static" from "just hasn't changed yet."

## Treat third-party COM property reads as fallible

Any live property or pattern read against a third-party app's UIA element can throw or hang, not just pattern invocations — don't assume a getter is cheap or safe just because it looks like one. Catch `COMException` around individual property reads and treat it as "try the next thing" rather than fatal, and guard against a getter that hangs rather than throws.

## Prefer keyboard shortcuts when the GUI is unreliable

When an app's controls are anonymous, fragile, or the GUI path is simply flaky (see above), a documented global hotkey is often the more robust way in — it doesn't depend on locating any element at all. Keep the GUI-driven equivalents around too for comparison, not because they're the better default.

That said, a "global" shortcut isn't automatically safe just because it doesn't need a locator — its meaning can depend on which panel currently has focus, in an app with more than one focusable region each claiming the same chord for a different command (a code editor binding `Ctrl+N` to "new file" globally, but to "new chat" while an AI/chat panel has focus, say). If an app has multiple such regions, click a known element in the one you actually want before sending the shortcut, rather than assuming a hotkey is inherently unambiguous.

## Electron/Chromium apps expose their UI Automation tree lazily

A freshly-launched Electron app (VS Code, Slack, Discord, Teams, ...) gets a real window handle — so `LaunchAsync`'s wait-for-window loop succeeds — well before Chromium has actually built out its accessibility tree. The very first `FindAsync`/`SnapshotAsync` against one can return almost nothing (just the native window chrome: minimize/maximize/close), then a full tree a few seconds later, once the query itself is what triggers Chromium to activate accessibility in the first place. Don't paper over this with a fixed startup delay; do a throwaway `FindAsync` for something you know will exist (a menu item, a toolbar button) with a generous `ImplicitWaitMs` instead, and let the retry loop ride out the activation lag naturally.

## A bare command "not recognized" despite visibly existing

If a terminal panel (or any embedded shell) reports a bare filename as "not recognized as an internal or external command" even though it's sitting right there in the working directory (confirm with `dir`), the machine likely has `NoDefaultCurrentDirectoryInExePath` set — a common security-hardening measure that disables cmd.exe's implicit current-directory search for unqualified commands. Invoke it as `.\name.exe` instead of a bare `name.exe`; the explicit relative path bypasses that search entirely, and works the same way under PowerShell.
