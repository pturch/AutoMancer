# ConsumerNotepadTests

A complete, working test project built with `AutoMancer.Testing`/`AutoMancer.Testing.XUnit`, using only public API — nothing here reaches into AutoMancer's own internals.

**For a guide to the API this project demonstrates, see [TESTING.md](../../TESTING.md).** This file is just an index of what's in this folder.

## Run it

```bash
dotnet test samples/ConsumerNotepadTests/
```

Requires Windows + Notepad — each test class launches (or attaches to) a real Notepad window. Don't run this alongside `tests/AutoMancer.Engine.Tests`' Notepad integration suite in the same `dotnet test` invocation; see [CONTRIBUTING.md](../../CONTRIBUTING.md) for why.

## What's demonstrated where

| File | What it shows |
|---|---|
| [`NotepadFixture.cs`](NotepadFixture.cs) | The `AppFixture` subclass every other file in this project shares |
| [`TestSetup.cs`](TestSetup.cs) | `[ModuleInitializer]` turning on `AutoMancerTestOptions.CaptureScreenshotsOnFailure` once for the whole assembly, instead of per test class |
| [`ActionsDemoTests.cs`](ActionsDemoTests.cs) | `ClickAsync`/`TypeAsync`/`ClearAsync`/`SetFocusAsync`/`HoverAsync` against a live control |
| [`KeyboardDragScrollDemoTests.cs`](KeyboardDragScrollDemoTests.cs) | `HotkeyAsync`, `DragThroughAsync`, `ScrollIntoViewAsync` |
| [`WindowAndDiscoveryDemoTests.cs`](WindowAndDiscoveryDemoTests.cs) | Window management (resize/move/maximize) and session discovery/attach |
| [`WindowCloseDemoTests.cs`](WindowCloseDemoTests.cs) | Launches its **own** Notepad instance instead of sharing `NotepadFixture` — closing the window would break every other test still relying on the shared instance being open, and Notepad's single-instance behavior means there's no second window to fall back to; still joins the same collection to stay sequential |
| [`LocatorStrategiesDemoTests.cs`](LocatorStrategiesDemoTests.cs) | `ByPath`, `ByXPath`, `ByRuntimeId`, `ByClassName` — the locator strategies the other files don't already exercise via `ByName`/`ByAutomationId`/`ByControlType` |
| [`WaitsAndAssertionsDemoTests.cs`](WaitsAndAssertionsDemoTests.cs) | `WaitConditions`' ready-made conditions for `WaitForAsync` (including `Not()`), and the `Expect` assertions that use the same rules: `ToBeEnabledAsync`, `ToContainTextAsync`, and `ToHaveCountAsync` waiting for a new tab |
| [`InteractabilityDemoTests.cs`](InteractabilityDemoTests.cs) | The `IsEnabled`/`IsOffscreen` signals on `ElementHandle`, and the typed errors (`ElementNotInteractableError`, etc.) `App` throws instead of silently sending input nowhere. Also shows a held menu item rejected once its menu closes, `App.Force` acting on it anyway, and `TypeAsync` landing in its target while focus is elsewhere |
| [`StaleHandleDemoTests.cs`](StaleHandleDemoTests.cs) | A handle held past its element's lifetime (the Save-changes dialog after it closes) raising `StaleElementError`, which carries the handle, instead of clicking where the button used to be |
| [`OcclusionDemoTests.cs`](OcclusionDemoTests.cs) | `IsUnobscuredAsync` reporting the Settings button covered while the Save-changes dialog is up, and clear once it's dismissed |
| [`FailureShapeDemoTests.cs`](FailureShapeDemoTests.cs) | **Read this one if you want to know what an `Expect` failure message actually looks like.** Deliberately triggers each failure shape — name mismatch, condition-timeout-but-found, never-found — wrapped in `Assert.Throws`/`ThrowsAsync` so it stays a real passing test rather than a permanently-red demo |
| [`EngineLoggerDemoTests.cs`](EngineLoggerDemoTests.cs) | Attaching an `EngineLogger` via `AppOptions.Logger` to capture AutoMancer's own operational trace — separate from `Expect`'s on-failure diagnostics, which are self-contained and need no logger |
