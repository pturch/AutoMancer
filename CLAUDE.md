# AutoMancer

Windows UI automation engine — C# class library + CLI + HTTP daemon + polyglot SDKs.

## Build & test

```bash
dotnet build AutoMancer.slnx
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration"
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"   # requires Windows + Notepad
dotnet test samples/ConsumerNotepadTests/                                   # requires Windows + Notepad
```

**Never run `dotnet test AutoMancer.slnx`** to exercise the integration suites. It runs every test project in the solution concurrently, and Windows 11 Notepad is single-instance — `AutoMancer.Engine.Tests`'s Notepad integration tests and `samples/ConsumerNotepadTests`'s demo tests will fight over the same OS-level window, producing flaky, non-reproducible failures. Run each Notepad-touching project one at a time via the commands above. `AutoMancer.Cli.Tests` has no live-UI dependency and is safe to run alongside anything.

The solution file is `.slnx` (not `.sln`) — that is the .NET 10 SDK default.

## Never commit automatically

All git operations are the developer's responsibility. Never run `git add`, `git commit`, or `git push`.

## Tech stack

- `net10.0-windows10.0.22621.0` — Windows only, no cross-platform guards
- C# latest / `LangVersion=preview`
- `System.Text.Json` throughout — no Newtonsoft.Json
- No DI container — dependencies wired manually at call sites

## Packages

- Do **not** add `Microsoft.Windows.SDK.Contracts` — it is incompatible with .NET 5+. WinRT APIs (e.g. `Windows.Media.Ocr`) are available via the `windows10.x` TFM suffix with no extra package.
- UIA3 COM interop: `Interop.UIAutomationClient`

## Code style

Write the minimum code that satisfies the requirement. No speculative abstractions, no helper methods for single call sites, no defensive error handling for internal code paths. Three similar lines beats a premature abstraction.

**Every function/method gets a one-line topline header comment** that briefly explains its purpose. This applies to constructors, public methods, and private helpers alike. Place it directly above the signature:

```csharp
// Serializes and writes one JSON line; no-ops when level is below the configured minimum.
private void Write(LogLevel level, string message, object? data) { ... }
```

**Every class/struct/enum gets the same one-line topline header comment**, explaining what it's for. Place it directly above the type declaration:

```csharp
// Converts between logical (96 DPI baseline) and physical screen coordinates for SendInput targeting.
public static class DpiHelper { ... }
```

Inline comments only when the *why* is non-obvious — never narrate what the code already says.

## Key invariants

**`ElementHandle` must stay opaque.** `Id`, `NativeHandle`, and read-only metadata (`Name`, `AutomationId`, `ClassName`, `ControlType`, `BoundingRect`, `IsEnabled`, `IsOffscreen`, `ResolvedVia`) are public; `Provider`, `Operator`, and `Logger` stay `internal`. The Phase 2 daemon stores handles by `Id` between stateless HTTP requests — leaking the provider/operator internals breaks that contract.

**Providers never throw on not-found.** Return `null` / empty. Only `ElementResolver` throws `ElementNotFoundError`.

**`TypeAction` must use `KEYEVENTF_UNICODE`** with `wScan` set to the character codepoint. Never use VK codes for printable characters — this is what fixes WinAppDriver's QWERTY-only keyboard layout bug.

## Every `.cs` file starts with

```csharp
// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
```
