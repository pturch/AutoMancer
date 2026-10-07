# Changelog

All notable changes to AutoMancer are documented here, described by what you'll notice rather than by API name; the [README](README.md) has the details. Entries are grouped by roadmap stage (see [docs/roadmap-spec.md](docs/roadmap-spec.md)) rather than by semantic version — Phase 1 shipped as `v0.1.0`, and Phase 2 work lands under `Unreleased` until the **v1** milestone, reached when Phase 2 (stages 2.1–2.8) is complete.

## Unreleased

### Stage 2.2 — Wait and Resilience Primitives

#### Added

- **Ready-made wait conditions** — common things to wait for (an element becoming visible or enabled, its name or text matching, its ID, class, or control type matching, or the opposite of any of these) no longer need a hand-written check.
- **Automatic recovery when you search by description** — when an element you described (by name, ID, position, and so on) turns out to be gone, AutoMancer looks it up again and carries on. This includes the container a scoped search runs in, so a dialog that closed and reopened is searched fresh. To handle this yourself instead, turn off the `ReresolveOnStale` option and you'll get a stale-element error. See [Locators vs. Element Handles](README.md#locators-vs-element-handles).

#### Changed

- **Gone elements are reported, not silently mishandled** — acting on or reading an element that has left the UI now fails with a clear "stale element" error. Previously a click could land wherever the element used to be, a value read could come back empty, and a search inside a container that vanished partway through looked the same as "nothing matched." This covers elements from a closed dialog that Windows itself doesn't report as gone (e.g. Notepad's Go to line), and the brief moment after an app exits.
- **An element you already found and kept** now fails with the stale-element error if it's gone, instead of acting on its old position or doing nothing. Nothing can look it up again for you, since it points at one specific element.
- **The scrolling search for long lists** now takes a description of the list rather than a list you already found, so it recovers if the list is rebuilt mid-scroll. You can still pin one specific list by its runtime ID.
- **With automatic recovery turned off**, a scoped search whose container disappears now fails right away with the stale-element error, instead of waiting out the full timeout and reporting "not found." A search for all matches never waited, so it previously came back as an empty list instead.

#### Fixed

- Searching for all matches inside a container that had disappeared could crash with an internal error under the `uia3` provider.

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
