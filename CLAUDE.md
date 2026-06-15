# AutoMancer

Windows UI automation engine — C# class library + CLI + HTTP daemon + polyglot SDKs.

## Build & test

```bash
dotnet build AutoMancer.slnx
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration"
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"   # requires Windows + Notepad
```

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

Write the minimum code that satisfies the requirement. No speculative abstractions, no helper methods for single call sites, no defensive error handling for internal code paths. Three similar lines beats a premature abstraction. Functions should have a topline header comment that is brief and explains the purpose of the function. Inline comments only when the *why* is non-obvious — never narrate what the code already says.

## Key invariants

**`ElementHandle` must stay opaque.** Only `Id` (string) and `NativeHandle` (object) are public. The Phase 2 daemon stores handles by `Id` between stateless HTTP requests — leaking internals breaks that contract.

**Providers never throw on not-found.** Return `null` / empty. Only `ElementResolver` throws `ElementNotFoundError`.

**`TypeAction` must use `KEYEVENTF_UNICODE`** with `wScan` set to the character codepoint. Never use VK codes for printable characters — this is what fixes WinAppDriver's QWERTY-only keyboard layout bug.

## Every `.cs` file starts with

```csharp
// Copyright (c) AutoMancer Contributors. Licensed under the MIT License.
```
