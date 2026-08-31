# Contributing

Looking for how to write tests *against your own app* using AutoMancer instead of how to contribute to AutoMancer itself? See [TESTING.md](TESTING.md).

By participating in this project you're expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Prerequisites

Same as the engine — see [README.md](README.md#prerequisites).

## Build & test

```bash
dotnet build AutoMancer.slnx
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration"
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"   # requires Windows + Notepad
dotnet test samples/ConsumerNotepadTests/                                   # requires Windows + Notepad
```

CI runs the build and the non-integration suite on every push and pull request to `main`. Integration tests need a real interactive desktop session, so they're local-only.

**Don't run `dotnet test AutoMancer.slnx`** to exercise the integration suites. It builds and runs every test project in the solution concurrently, and Windows 11 Notepad is single-instance: `AutoMancer.Engine.Tests`'s Notepad integration tests and `samples/ConsumerNotepadTests`'s demo tests will fight over the same OS-level window, producing flaky, non-reproducible failures (stale UIA elements, text from one test landing in another). Run each Notepad-touching project one at a time via the commands above instead. `AutoMancer.Cli.Tests` has no live-UI dependency and is safe to run alongside anything.

## Code style

- Write the minimum code that satisfies the requirement — no speculative abstractions, no helper methods for single call sites. Multiple instances of a pattern (or a clear architectural plan) are reasons to start abstracting.
- `System.Text.Json` only, no Newtonsoft.Json.
- Every function/method and every class/struct/enum gets a one-line topline comment directly above it explaining its purpose — constructors and private helpers included.
- Beyond that, inline comments only when the *why* is non-obvious.
- Every `.cs` file starts with:

  ```csharp
  // Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
  ```

## Reporting bugs and requesting features

Open a [GitHub issue](https://github.com/pturch/AutoMancer/issues). For a bug, include the Windows version, the target app you were automating, and — if you can — the smallest repro you found. For a security issue, see [SECURITY.md](SECURITY.md) instead of opening a public issue.

## Pull requests

- Keep PRs focused on a single change.
- Make sure `dotnet build` and the non-integration test suite pass locally before opening a PR.
- Describe what changed and why in the PR description.
- By submitting a pull request, you agree to license your contribution under the project's [Apache License 2.0](LICENSE).
