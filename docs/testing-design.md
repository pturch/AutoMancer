# AutoMancer Testing Design

## The Selenium vs. Playwright split

**Selenium** gives you a raw API (`IWebDriver`, `By`, `IWebElement`) and almost zero opinion about test structure. Page Object Model, assertions, fixtures — all third-party patterns you assemble yourself. The only "testing" helper it ships is `WebDriverWait`/`ExpectedConditions` in a support package.

**Playwright** makes a much cleaner distinction:
- `Microsoft.Playwright` — core automation, zero test-framework dependency
- `Microsoft.Playwright.NUnit` / `.MSTest` — separate adapter packages with base classes (`PageTest`, `ContextTest`), pre-wired fixtures, and test output routing
- Built into core: retry-asserting `Expect(locator).ToBeVisibleAsync()`, tracing, screenshots
- The adapter packages wire those diagnostics to the test runner's output on failure

The key insight: **Playwright's `Expect()` assertions poll with a timeout**, rather than being single-shot. That's the feature that makes UI tests resilient — not the find-retry, but the *assert-retry*.

---

## Where AutoMancer currently sits

AutoMancer has Playwright-quality find-retry (implicit wait in `ElementResolver`), but nothing on the assertion side. Test consumers today must:

1. Manually manage `App` lifecycle in xUnit fixtures/constructors
2. Write their own assertions (`ElementHandle != null`, inspect `.Name`, etc.)
3. Manually dump the tree or take screenshots on failure
4. Wire `EngineLogger` output to xUnit's `ITestOutputHelper` themselves

---

## What belongs where

**Engine (`AutoMancer.Engine`) — already correct or close:**
- `App`, `AppSession`, `AppOptions` — the right entry points
- `Locator` strategies, `ElementHandle`, error types
- Implicit find-retry is correctly here (not test-framework specific)
- `SnapshotAsync()` for tree dumps — good building block

**Engine gaps worth filling:**
- `App.WaitForAsync(locator, condition)` — a retry-asserting primitive. Playwright's killer feature. "Wait until this element's text equals X" without polling yourself.
- `AppOptions.TestDefaults` static — slightly longer implicit waits, shorter action delay, suitable for CI environments

**Test adapter layer (does not exist yet — this is the gap):**

A separate `AutoMancer.Testing` package (no framework coupling) plus thin `AutoMancer.Testing.XUnit` adapter would give consumers:

| Thing | Where | Why not in Engine |
|---|---|---|
| `AppFixture` (xUnit `IClassFixture`) | `.Testing.XUnit` | References xUnit types |
| `AutoMancerTest` base class with teardown | `.Testing.XUnit` | Same |
| On-failure tree dump / screenshot | `.Testing` | Uses `EngineLogger`, no test coupling |
| `Expect(handle).ToHaveName("X")` assertion API | `.Testing` | Can be framework-agnostic |
| Route `EngineLogger` → `ITestOutputHelper` | `.Testing.XUnit` | xUnit-specific sink |

---

## What test projects should bring themselves

- Which test framework (xUnit, NUnit, MSTest) — not our concern
- Test parallelism decisions — whether to share one `App` per collection or one per class
- What to assert about business logic — we provide "does this element exist / have this value", not "did the save succeed"
- Test data setup — launching the right app with the right initial state

---

## Recommended priority

The most impactful single addition is **retry-asserting `WaitForAsync` in the engine**, because it changes test resilience fundamentally:

```csharp
// Instead of this fragile pattern:
var el = await app.FindAsync(Locator.ByName("Status"));
Assert.Equal("Saved", el.Name);  // races

// You'd write:
await app.WaitForAsync(Locator.ByName("Status"), e => e.Name == "Saved");
```

After that, `AutoMancer.Testing.XUnit` with a base fixture class handles the boilerplate that every consumer will otherwise copy-paste.
