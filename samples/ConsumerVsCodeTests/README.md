# ConsumerVsCodeTests

A complete, working test project built with `AutoMancer.Testing`/`AutoMancer.Testing.XUnit`, using only public API — nothing here reaches into AutoMancer's own internals.

**For a guide to the API this project demonstrates, see [TESTING.md](../../TESTING.md).** This file is just an index of what's in this folder.

## Run it

```bash
dotnet test samples/ConsumerVsCodeTests/
```

Requires Windows + Visual Studio Code installed (see `VsCodeLauncher.FindCodeExe` for the install locations it checks). It launches a fully isolated VS Code instance (its own `--user-data-dir`/`--extensions-dir`) rather than attaching to whatever the developer already has open, so it's exempt from the Notepad sample's single-instance window-handoff problem specifically — but it still drives real keyboard/mouse input against a real window, so avoid running it alongside anything else that does too, for the same foreground-window-contention reason.

## What's demonstrated where

| File | What it shows |
|---|---|
| [`VsCodeLauncher.cs`](VsCodeLauncher.cs) | `AppOptions.Arguments` for a launch that needs command-line flags (`--user-data-dir`, `--extensions-dir`, ...); `WindowMatchOptions.RequireNewWindow` to avoid attaching to a VS Code window the developer already has open elsewhere; `AppOptions.KillEntireProcessTree` so teardown reaches VS Code's helper processes too — still entirely public API, no `App.AttachByPidAsync` needed |
| [`VsCodeCollection.cs`](VsCodeCollection.cs) | `[CollectionDefinition(DisableParallelization = true)]` for a suite where each test class launches its own instance rather than sharing one via `ICollectionFixture` |
| [`VsCodeFixture.cs`](VsCodeFixture.cs) | An `AppFixture` over the same isolated launch, so a test class shares one VS Code (and gets `Expect()` and failure screenshots through `AutoMancerTest`); deletes the sandbox in `OnKilledAsync` |
| [`WorkbenchStateDemoTests.cs`](WorkbenchStateDemoTests.cs) | How element state reads on Electron. A disabled "Go Back" makes actions throw `ElementNotInteractableError` until `ToBeEnabledAsync` sees it enable. A held side-bar element raises `StaleElementError` once Ctrl+B hides it, because Chromium removes hidden views from the tree instead of marking them offscreen. `IsUnobscuredAsync` reports the editor area covered while the Command Palette is open, checked with `Expect(locator).ToBeObscuredAsync()`/`ToBeUnobscuredAsync()`, which poll, because Chromium's first hit-test after a change can be stale |
| [`TestSetup.cs`](TestSetup.cs) | `[ModuleInitializer]` turning on `AutoMancerTestOptions.CaptureScreenshotsOnFailure` once for the whole assembly |
| [`HelloWorldScriptDemoTests.cs`](HelloWorldScriptDemoTests.cs) | A full real-world workflow, not just isolated API calls: create a file, type multi-line content, `Ctrl+S` through a native Save dialog via `App.FindDialogAsync`, drive the integrated terminal, and verify a background process actually ran by polling for a file it writes with `Expect(() => …).ToSatisfyAsync`. Keystrokes go to whatever has focus, so instead of fixed delays each step first waits for the element that should receive them (the new editor, the Command Palette's input, the terminal) to appear; see the comments in that file for the focus workarounds a genuinely complex Electron app needed |

## Why this one looks different from ConsumerNotepadTests

Notepad is a single native Win32/WinUI3 control — most of that sample is one clean API call per test. VS Code is a full Electron app with a first-run wizard, an AI chat panel that steals keyboard focus and shortcuts, a lazily-activated UI Automation tree, and a terminal panel that needs an explicit mouse click to reliably take focus. None of that reflects a weakness in AutoMancer's API — every workaround here is real VS Code/Electron/Windows behavior, discovered by driving the actual app repeatedly until the workflow stopped flaking. It's a useful second data point: what automating a modern, actively-changing desktop app actually takes, beyond the happy path a simple native control gives you for free.
