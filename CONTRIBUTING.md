# Contributing

## Prerequisites

- Windows 10/11 (the engine targets `net10.0-windows10.0.22621.0` — no cross-platform support)
- .NET 10 SDK

## Build & test

```bash
dotnet build AutoMancer.slnx
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration"
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"   # requires Windows + Notepad
```

CI runs the build and the non-integration suite on every push and pull request to `main`. Integration tests need a real interactive desktop session, so they're local-only.

## Code style

- Write the minimum code that satisfies the requirement — no speculative abstractions, no helper methods for single call sites.
- `System.Text.Json` only, no Newtonsoft.Json.
- Comments only when the *why* is non-obvious.
- Every `.cs` file starts with:

  ```csharp
  // Copyright (c) AutoMancer Contributors. Licensed under the MIT License.
  ```

## Pull requests

- Keep PRs focused on a single change.
- Make sure `dotnet build` and the non-integration test suite pass locally before opening a PR.
- Describe what changed and why in the PR description.
