# Contributing

By participating in this project you're expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Build & Test:

```bash
# Build:
dotnet build AutoMancer.slnx

# Unit Tests:
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration"

# Integration Tests (requires Windows + Notepad):
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"   
dotnet test samples/ConsumerNotepadTests/                                   
```

CI runs the build and the non-integration suite on every push and pull request to `main`. Integration tests need a real interactive desktop session, so they're local-only.

Avoid running `dotnet test AutoMancer.slnx` because it runs every test project in the solution concurrently, causing the tests to fight over the same OS-level window resources.

## Code Style:

There are many ways to write valid code, but making sure they share a look and feel makes the project feel more cohesive. These rules are covered in the `CLAUDE.MD` file for agents to consider, but keep them in mind while authoring:

- **Write the minimum code that satisfies the requirement.** Speculative abstractions and helper methods for single call sites tend to lead to bloat. Multiple instances of a pattern (or a clear architectural plan) are reasons to start abstracting.
- **Leverage existing packages instead of introducing new ones.** For example, AutoMancer is currently using `System.Text.Json`, not Newtonsoft.Json. Changing from one to the other would merit a discussion.
- **Use comments wisely.** Every function/method and every class/struct/enum gets a one-line topline comment directly above it explaining its purpose. This includes  constructors and private helpers that might seem obvious to the author; the intention is to help the other maintainers. Beyond that, use inline comments only when the *why* is non-obvious.
- **Formatting:** Every `.cs` file starts with:

  ```csharp
  // Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
  ```

## Reporting Bugs and Requesting Features:

Open a [GitHub issue](https://github.com/pturch/AutoMancer/issues). For a bug, include the Windows version, the target app you were automating, and — if you can — the smallest repro you found. For a security issue, see [SECURITY.md](SECURITY.md) instead of opening a public issue.

## Pull Requests:

Just like with code style, there are many ways to both log and fix a bug.

- **Keep PRs focused on a single change.** Feature scope can be opinionated, but try for the smallest possible deliverable. Massive refactors and changes can complicate and invalidate other folks' branches.
- **Test before opening PRs.** Make sure `dotnet build` compiles and all the test suites pass locally before opening a PR. The Integration tests are subject to flukes and not part of the PR process, but please make sure there are no functional regressions. 
- **Keep up with documentation.** Describe what changed and why in the PR description, and update any docs the change affects so they don't drift out of sync with the code.
- **Always have a human touch on anything you submit.** AI tools are fine for generating code or docs, but review and understand everything before opening a PR. Don't submit unread output. Keep prose human-readable and write PR descriptions in your own words.

By submitting a pull request, you agree to license your contribution under the project's [Apache License 2.0](LICENSE).
