# AutoMancer Extended Coverage Implementation Plan

> **For agentic workers:** Steps use checkbox (`- [ ]`) syntax for tracking progress through this plan.
>
> **Version control stays manual.** Whoever picks up a task commits deliberately — nothing in this workflow auto-commits or auto-pushes. Bash blocks in this document are implementation reference — execute the build/test lines only, never the git lines.

**Phase:** 2 of 3 — this plan deepens the Phase 1 engine surface (richer locators, wait/resilience primitives, native fallbacks, a visual provider, and UIA pattern coverage). No Phase 1 source files are removed or have their existing signatures changed; every task here is additive. **Completing this phase is the v1 milestone** — a hardened, complete C# engine, CLI, and test adapter, shippable on its own. Phase 3 (daemon + SDKs) is deliberately deferred, long-running future work picked up later, not the next phase in the queue. See [roadmap-spec.md](./roadmap-spec.md) for how this fits into the overall three-phase plan. **Task numbers here are the same numbers as roadmap-spec.md's batches** — Task `2.1.7` below is batch `2.1.7` there, not a separately-tracked ID. Each stage's tasks restart at `.1`, independent of every other stage's count, so a stage never needs renumbering because a different stage gained or lost a task.

**Goal:** Take the Phase 1 engine — UIA3/UIA2/Win32 providers, `ElementResolver`, the full interaction/wait surface, and the `AutoMancer.Testing` adapter — and extend it with the coverage a real automation suite eventually needs but a proof of concept doesn't: locators for unlabeled/custom-property/scoped/virtualized-list elements, a canned wait vocabulary, stale-element resilience, native context menus, a resource-leak diagnostic, an accessibility audit, a visual (OCR/template) fallback provider plus the general-purpose idle-wait/visual-regression capabilities that reuse its screenshot plumbing, DPI/OS hardening, and the UIA pattern actions (`Toggle`/`ExpandCollapse`/`Selection`/`Grid`/`RangeValue`/clipboard) Phase 1 never reached — which Phase 3's daemon and SDKs also happen to assume exist, whenever that phase is picked up.

**Architecture:** Every task in this plan touches only `AutoMancer.Engine` (and, for Stage 2.2's `Expect()` extension, `AutoMancer.Testing`) — no `AutoMancer.Daemon` project exists yet, so nothing here is reachable over HTTP. `ElementHandle` stays opaque throughout (only `Id`/`NativeHandle` public) — Phase 3's daemon element registry depends on that contract holding. Where a new capability wraps a UIA pattern the engine doesn't already touch (`TogglePattern`, `GridPattern`, `RangeValuePattern`, …), it follows the existing `IElementOperator`-optional, no-op-when-unsupported shape `ScrollAction`/`SetFocusAction` already established in Phase 1, rather than inventing a new dispatch convention.

**Prior art:** `WaitConditions` (Stage 2.2) mirrors Selenium's `ExpectedConditions` — a starter vocabulary instead of every caller hand-rolling predicates. The Visual Provider batches of Stage 2.6 are the fallback tier neither Selenium, Playwright, nor WinAppDriver offer for apps with no accessibility tree at all; that same stage's `WaitForIdleAsync`/visual-regression batches are general-purpose and apply to any app, not just no-UIA ones.

**Tech Stack:** C# latest / .NET 10 Windows (`net10.0-windows10.0.22621.0`), `Interop.UIAutomationClient` (UIA3 COM — already referenced; this phase adds `IUIAutomationRegistrar` for custom-property GUID resolution and `TogglePattern`/`ExpandCollapsePattern`/`SelectionItemPattern`/`SelectionPattern`/`GridPattern`/`TablePattern`/`RangeValuePattern` usage), `Windows.Media.Ocr.OcrEngine` (on-device OCR via the existing `windows10.x` TFM suffix — no extra package, per CLAUDE.md), `OpenCvSharp4.Windows` (new — template matching), `System.Windows.Clipboard` (already available via `UseWPF`, no new dependency — see Task 2.8.1), raw Win32 P/Invoke additions to `NativeMethods.cs` (`GetGuiResources`, `GetMenu`/`GetSubMenu`/`GetMenuItemInfo`/`TrackPopupMenuEx`).

**Test Stack:** xunit 2.8, Moq 4.20 — unit tests in `tests/AutoMancer.Engine.Tests/`; integration tests in the same project under `[Trait("Category","Integration")]`, run separately per CLAUDE.md's Notepad single-instance rule; run with `dotnet test`.

**Prerequisite:** Phase 1 complete — `dotnet build AutoMancer.slnx` reports 0 errors, `tests/AutoMancer.Engine.Tests` passes both its unit and integration suites. Every task below is additive to that surface; existing callers and existing tests keep working unchanged whether or not a given task has landed, so tasks can be picked up out of order except where a "Depends on" note says otherwise.

---

## File Map

```
src/AutoMancer.Engine/
├── Core/
│   ├── LocatorStrategy.cs            Spatial, Property strategy cases
│   ├── Locator.cs                    Near, ByProperty(int)/(UiaProperty)/(Guid) factories
│   ├── SpatialDirection.cs           Above/Below/LeftOf/RightOf/Near enum
│   ├── SpatialMatcher.cs             anchor-relative candidate filtering, lives in ElementResolver's call path
│   ├── UiaProperty.cs                named enum for well-known UIA property IDs
│   ├── WaitConditions.cs             canned Func<ElementHandle, bool> factories
│   ├── ElementResolver.cs            opt-in stale-element re-resolve; scope-aware FindAsync/FindAllAsync
│   └── IElementProvider.cs           FindElementAsync/FindElementsAsync take an ElementHandle? scope
├── Providers/
│   ├── Uia3Provider.cs, Uia2Provider.cs, Win32Provider.cs
│   │                                 honor the scope parameter as the search root
│   ├── NativeMethods.cs              GetGuiResources, GetMenu family (context menu fallback)
│   └── VisualProvider.cs             IElementProvider via OCR + template matching
├── Actions/
│   ├── ContextMenuAction.cs          native HMENU right-click fallback
│   ├── ClipboardAction.cs            GetTextAsync/SetTextAsync, not element-scoped
│   ├── ToggleAction.cs               TogglePattern.Toggle()
│   ├── ExpandCollapseAction.cs       ExpandCollapsePattern.Expand()/Collapse()
│   ├── SelectionAction.cs            SelectionItemPattern/SelectionPattern
│   ├── GridAction.cs                 GridPattern/TablePattern row/column/cell access
│   └── RangeValueAction.cs           RangeValuePattern get/set
├── Diagnostics/
│   ├── ResourceWatch.cs              GDI/USER handle + working-set sampler
│   └── AccessibilityAuditor.cs       unnamed-interactive-element tree walker
├── App.cs                            façade methods for every task below
└── AppOptions.cs / Core/ElementProviderOptions.cs   ReresolveOnStale, debug-screenshot flag

src/AutoMancer.Testing/
└── LocatorExpect.cs                  ToBeEnabledAsync/ToContainTextAsync built on WaitConditions

src/AutoMancer.Cli/Commands/
└── AuditCommand.cs                   `automancer audit <session>`

examples/
├── notepad/
├── winforms-calculator/
├── legacy-no-uia/                    Visual Provider showcase
└── xunit-test-adapter/

tests/AutoMancer.Engine.Tests/
├── Core/SpatialMatcherTests.cs, WaitConditionsTests.cs, ScopedFindTests.cs, FindByScrollingTests.cs, ...
├── Actions/ContextMenuActionTests.cs, ClipboardActionTests.cs, ToggleActionTests.cs, RangeValueActionTests.cs, ...
├── Diagnostics/AccessibilityAuditorTests.cs, ...
└── Integration/ (one integration test class per stage)
```

---

## Stage 2.1 — Locator Power Tools

### Task 2.1.1: Spatial locator strategy — `Locator.Near`

**What:** Adds `LocatorStrategy.Spatial` and a `SpatialDirection` enum (`Above`, `Below`, `LeftOf`, `RightOf`, `Near`).

**`Locator` needs widening for this — it can't stay a single-string-`Value` record.** `Locator` is currently `sealed record Locator(LocatorStrategy Strategy, string Value)`: every existing factory (`ByName`, `ByPath`, …) fits because they all match against one string. A spatial locator needs to carry a *nested* `Locator` (the anchor) plus a `SpatialDirection` plus an `int` — that doesn't fit in `Value` without lossy string-encoding. Add optional `init`-only properties instead of serializing into `Value`:

```csharp
public sealed record Locator(LocatorStrategy Strategy, string Value)
{
    internal Locator? Anchor { get; init; }
    internal SpatialDirection? Direction { get; init; }
    internal int? MaxDistancePx { get; init; }
    // (Task 2.1.3 adds one more: internal object? PropertyValue { get; init; })

    public static Locator Near(Locator anchor, SpatialDirection direction, int maxDistancePx = 200) =>
        new(LocatorStrategy.Spatial, Value: string.Empty) { Anchor = anchor, Direction = direction, MaxDistancePx = maxDistancePx };
}
```
This adds properties to the record without touching its primary constructor, so every existing 2-arg `new(strategy, value)` call site (all seven current factories) keeps compiling unchanged. `internal` keeps them out of the public opaque-`Locator` surface — only `Uia3Provider`/`ElementResolver` (same assembly) ever read them; external consumers only ever see the factory methods.

**Creates:**
- `src/AutoMancer.Engine/Core/SpatialDirection.cs` — the four-plus-`Near` enum
- `src/AutoMancer.Engine/Core/LocatorStrategy.cs` — add `Spatial` case
- `src/AutoMancer.Engine/Core/Locator.cs` — add the `Anchor`/`Direction`/`MaxDistancePx` internal properties and the `Near(...)` factory, per the shape above

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors; `Locator.Near(Locator.ByName("Username"), SpatialDirection.RightOf)` compiles and both the anchor `Locator` and the direction are readable back off the result (internally — no round-trip through `Value` needed).

---

### Task 2.1.2: `SpatialMatcher`

**What:** Resolves the anchor via the existing `ElementResolver.FindAsync`, then filters candidates by `BoundingRect` proximity and direction. Lives once in `ElementResolver` (not duplicated per provider) since it operates on already-resolved rects, not raw UIA queries — `ElementResolver.FindAsync` special-cases `LocatorStrategy.Spatial` up front: resolve `locator.Anchor` through the normal provider chain to get the anchor's rect, then get the **candidate pool** from a full tree snapshot (`ElementResolver.TrySnapshotAsync`, the same call `App.SnapshotAsync` already exposes) taken from the anchor's session, and hand the anchor rect plus every element in that snapshot to `SpatialMatcher` instead of matching on a UIA property condition. (The roadmap batch this task implements doesn't say where candidates come from — this is the concrete answer: everything in the same session's current snapshot, not a scoped subtree.)

**Creates:**
- `src/AutoMancer.Engine/Core/SpatialMatcher.cs` — `FindNearest(Rect anchorRect, IReadOnlyList<ElementHandle> candidates, SpatialDirection direction, int maxDistancePx)` → `ElementHandle?`; direction test is a half-plane check relative to the anchor's edge (e.g. `RightOf` = candidate's left edge ≥ anchor's right edge, within `maxDistancePx`), nearest-by-center-distance breaks ties
- `tests/AutoMancer.Engine.Tests/Core/SpatialMatcherTests.cs` — synthetic rect layout: candidate directly right of anchor matches `RightOf`; candidate above-and-right does not match `RightOf` past a reasonable angular tolerance; candidate beyond `maxDistancePx` is excluded; nearest of two valid candidates wins

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "SpatialMatcherTests"
```

**Done when:** `SpatialMatcherTests` pass against a synthetic layout with no live UI needed.

---

### Task 2.1.3: `Locator.ByProperty(int propertyId, object value)`

**What:** Generic escape-hatch strategy for the built-in UIA property set — for properties AutoMancer hasn't given a named `LocatorStrategy` to. `Uia3Provider.BuildCondition` routes `LocatorStrategy.Property` straight to `IUIAutomation.CreatePropertyCondition(propertyId, value)`, bypassing the fixed strategy→property map every other strategy goes through. Same widening as Task 2.1.1: `propertyId` fits in `Locator.Value` fine (`value.ToString()` covers the common `string`/`bool`/`int` cases losslessly for the purposes of equality-matching), but the raw `object value` needs its actual CLR type preserved for `CreatePropertyCondition` (a boxed `bool` and the string `"true"` are not interchangeable there) — add one more internal property:

```csharp
internal object? PropertyValue { get; init; }

public static Locator ByProperty(int propertyId, object value) =>
    new(LocatorStrategy.Property, Value: propertyId.ToString()) { PropertyValue = value };
```
`Uia3Provider.BuildCondition` reads `propertyId` from `int.Parse(locator.Value)` and the typed value from `locator.PropertyValue`.

**Creates:**
- `src/AutoMancer.Engine/Core/LocatorStrategy.cs` — add `Property` case
- `src/AutoMancer.Engine/Core/Locator.cs` — add the `PropertyValue` internal property and `ByProperty(int propertyId, object value)` factory, per the shape above
- `src/AutoMancer.Engine/Providers/Uia3Provider.cs` — `BuildCondition` gains a `Property` branch

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 2.1.4: `UiaProperty` enum

**What:** Named constants for the built-in UIA properties worth surfacing without a raw int (`HelpText`, `LocalizedControlType`, `IsOffscreen`, `ItemStatus`, `IsContentElement`, `AriaRole`, `AriaProperties`, …), mapped to their well-known integer property IDs (`UIA_HelpTextPropertyId` etc. from `Interop.UIAutomationClient`). `Locator.ByProperty(UiaProperty property, object value)` overload forwards to Task 2.1.3's int-based factory so the common case never needs a raw ID.

**Creates:**
- `src/AutoMancer.Engine/Core/UiaProperty.cs` — the enum, plus an internal `ToPropertyId()` mapping
- `src/AutoMancer.Engine/Core/Locator.cs` — add `ByProperty(UiaProperty property, object value)` overload

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** `Locator.ByProperty(UiaProperty.HelpText, "Search")` compiles and produces the same resolved `Locator` as the equivalent raw-int call.

---

### Task 2.1.5: Custom property GUID resolution — `RegisterCustomPropertyAsync`

**What:** For app-registered custom properties, which don't have stable integer IDs across processes — a custom property's numeric ID is assigned at registration time and can't be hardcoded.

**⚠️ Verify the exact `IUIAutomationRegistrar` shape against `Interop.UIAutomationClient`'s generated types before implementing this task — the signature below is written from general UIA COM documentation, not confirmed against this project's actual package version.** As documented, `IUIAutomationRegistrar.RegisterProperty` takes a full `UIAutomationPropertyInfo { Guid guid; string programmaticName; UIAutomationType type; }`, not just a GUID — the caller has to already know the property's declared name and value type to resolve it, the same way the app that originally registered it did. A bare `Locator.ByProperty(Guid, object value)` factory can't supply that on its own, so **this task exposes a one-time registration call instead of a new `Locator` factory**, and the resolved ID feeds into Task 2.1.3's existing `ByProperty(int, object)`:

```csharp
// On Uia3Provider (or App, delegating to it) — a live UIA3 COM call, so it can't happen at Locator-construction time.
public async Task<int> RegisterCustomPropertyAsync(Guid propertyGuid, string programmaticName, UiaAutomationType type, CancellationToken ct = default);
```
Callers do `var id = await app.RegisterCustomPropertyAsync(guid, "MyApp.Status", UiaAutomationType.String); app.FindAsync(Locator.ByProperty(id, "Ready"))` — one extra line, but it doesn't require guessing at a `Locator` shape that can't actually carry enough information to work.

**Creates:**
- `src/AutoMancer.Engine/Providers/Uia3Provider.cs` — `RegisterCustomPropertyAsync(Guid, string, UiaAutomationType, CancellationToken)` → `int`, via `IUIAutomationRegistrar.RegisterProperty`
- `src/AutoMancer.Engine/App.cs` — thin delegating overload

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors. Full behavior is only verifiable live (Task 2.1.6) since resolution requires a real UIA3 session against an app that registered the property, and requires confirming the `IUIAutomationRegistrar` signature noted above.

---

### Task 2.1.6: Unit and integration tests for Stage 2.1

**What:** Closes out the stage's locator coverage with the three end-to-end cases the "Done when" at the top of this section names.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Integration/SpatialLocatorIntegrationTests.cs` — spatial locator finds a live app's unlabeled `Edit` control next to its label
- `tests/AutoMancer.Engine.Tests/Integration/PropertyLocatorIntegrationTests.cs` — `Locator.ByProperty(UiaProperty.HelpText, ...)` finds an element via the named enum; a custom-property lookup against a test app that registers one via `AutomationProperties.RegisterProperty` (WPF) or an equivalent native registration, using `RegisterCustomPropertyAsync` (Task 2.1.5) to resolve the ID and `Locator.ByProperty(int, object)` (Task 2.1.3) to query it

- [ ] **Run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~Locator"
```

**Done when:** Locators cover the three cases the fixed strategy set can't reach: elements with no name, built-in properties nobody added a named strategy for, and properties that only exist because a specific app registered them.

---

### Task 2.1.7: Scoped/relative find

**What:** Every find today goes through `ElementResolver.FindAsync(locator, session, ct)` — always resolving against the whole session, with no way to restrict a search to a specific element's subtree. That's a real gap once a window has two elements that match the same locator in different places (e.g. a "Cancel" button on the main form *and* one in a dialog) — there's no way to say "only search inside this dialog." Adds an optional `ElementHandle? scope` parameter threaded through the find path; when supplied, resolution is restricted to that element's descendants. Defaults to `null` everywhere so every existing call site keeps compiling and behaving identically — this task is purely additive, same as everything else in this phase.

**⚠️ `App.FindAsync`/`FindAllAsync` need a new overload, not a widened existing signature — inserting `scope` into the existing 2-parameter method breaks a real call site.** `src/AutoMancer.Testing/LocatorExpect.cs` already calls `_app.FindAllAsync(_locator, ct)`, passing `ct` positionally as the second argument. Inserting `ElementHandle? scope = null` before `ct` on the existing method would make that call try to bind a `CancellationToken` to an `ElementHandle?` parameter — a compile break, and a direct violation of this plan's own constraint that no existing Phase 1 public signature changes. Add a second overload instead, leaving the original 2-parameter method untouched:
```csharp
public Task<ElementHandle> FindAsync(Locator locator, CancellationToken ct = default) => ...; // unchanged
public Task<ElementHandle> FindAsync(Locator locator, ElementHandle? scope, CancellationToken ct = default) => ...; // new
```
`scope` must **not** have a default value on the new overload — if both overloads made every parameter after `locator` optional, `FindAsync(locator)` would be an ambiguous call between them (two equally-applicable candidates via omitted defaults is a compile error in C#, not a tiebreak). Requiring `scope` on the second overload means a 1-argument call only ever matches the first, and a 2-or-3-argument call only ever matches the second — no ambiguity. Same shape for `FindAllAsync`.

**Creates/Modifies:**
- `src/AutoMancer.Engine/Core/IElementProvider.cs` — `FindElementAsync`/`FindElementsAsync` gain `ElementHandle? scope = null`. Safe to widen in place (not via overload) — this interface has exactly four implementers, all within this assembly, and nothing outside the engine calls it directly, so there's no external call site to break the way there is with the public `App` facade
- `src/AutoMancer.Engine/Providers/Uia3Provider.cs` / `Uia2Provider.cs` — when `scope` is non-null, use `scope.NativeHandle` (cast to `IUIAutomationElement`) as the `FindFirst`/`FindAll` search root instead of the session's root element
- `src/AutoMancer.Engine/Providers/Win32Provider.cs` — when `scope` is non-null, `EnumChildWindows` starts from `scope`'s `hwnd` instead of the session root's hwnd
- `src/AutoMancer.Engine/Core/ElementResolver.cs` — `FindAsync`/`FindAllAsync` gain the same optional `scope` parameter, inserted after the required `session` parameter and before `ct`. Safe to widen in place here too — `session` is required (not optional), so no existing 2-argument call (`resolver.FindAsync(locator, session)`) is affected, and nothing in this codebase calls it with `ct` positionally as a third argument
- `src/AutoMancer.Engine/App.cs` — add the `FindAsync(Locator, ElementHandle?, CancellationToken)` / `FindAllAsync` overloads described above; the original 2-parameter overloads stay exactly as they are and simply delegate to the resolver with `scope: null`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors; `app.FindAsync(locator)` and every other existing call site (including `LocatorExpect.cs`'s `FindAllAsync(_locator, ct)`) compile and behave identically to today — no existing test regresses.

---

### Task 2.1.8: Virtualized-list find — `App.FindByScrollingAsync`

**What:** Virtualizing `ListView`/`ComboBox`/`DataGrid` controls only realize visible rows in the UIA tree — an item 500 rows down simply isn't there to find yet, and `ScrollAction` (`ScrollItemPattern.ScrollIntoView`) only helps once you already have an `ElementHandle` for the item, which is exactly the problem. `App.FindByScrollingAsync(ElementHandle container, Locator itemLocator, int maxScrolls = 20, CancellationToken ct = default)` closes that gap: repeatedly try Task 2.1.7's scoped find against `container`, and if not found, scroll `container` one notch via the existing `ScrollWheelAction` and retry.

**Loop termination:** reads `IUIAutomationScrollPattern.CurrentVerticalScrollPercent` on `container` before and after each scroll. If the percentage is unchanged across two consecutive scroll attempts, the container has hit the end of its scrollable range and the item genuinely isn't there — throw `ElementNotFoundError` immediately rather than burning through the rest of `maxScrolls` on a container that's stopped moving.

**Creates:**
- `src/AutoMancer.Engine/App.cs` — `FindByScrollingAsync(ElementHandle container, Locator itemLocator, int maxScrolls = 20, CancellationToken ct = default)`, built on Task 2.1.7's scoped `FindAsync` and the existing `ScrollWheelAction`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 2.1.9: Unit and integration tests for Tasks 2.1.7–2.1.8

**What:** Closes out scoped find and virtualized-list find with the two end-to-end cases the Stage 2.1 "Done when" line names.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Core/ScopedFindTests.cs` — against a fake provider, a scoped find matches only descendants of the given `scope` element, ignoring identically-matched elements elsewhere in the tree
- `tests/AutoMancer.Engine.Tests/Core/FindByScrollingTests.cs` — against a fake provider/scroll-pattern that "reveals" a target item only after N simulated scroll calls; a control test proving the loop throws `ElementNotFoundError` (not an infinite loop) once the simulated scroll percentage stalls across two attempts
- `tests/AutoMancer.Engine.Tests/Integration/ScopedFindIntegrationTests.cs` — scoped find disambiguates two identically-named buttons in different dialogs of a live app
- `tests/AutoMancer.Engine.Tests/Integration/VirtualizedListFindIntegrationTests.cs` — `FindByScrollingAsync` locates a far-down item in a long live `ListView`

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "FullyQualifiedName~ScopedFind|FullyQualifiedName~FindByScrolling"
```

**Done when:** `app.FindAsync(locator, scope: element)` only matches descendants of `element`; `app.FindByScrollingAsync(container, itemLocator)` finds a far-down item in a live virtualized list and throws (not hangs) when the item genuinely isn't there. Stage 2.1 complete.

---

## Stage 2.2 — Wait and Resilience Primitives

### Task 2.2.1: `WaitConditions` static class

**What:** Canned `Func<ElementHandle, bool>` factories — `IsVisible()`, `NameEquals(string)`, `NameContains(string)`, `TextEquals(string)`, `IsEnabled()` — mirroring Selenium's `ExpectedConditions`. Each is a small predicate closure; this adds no new engine surface beyond the static class itself, since every predicate is a drop-in for `App.WaitForAsync`'s existing `Func<ElementHandle, bool>` parameter.

**Creates:**
- `src/AutoMancer.Engine/Core/WaitConditions.cs` — the five factories above
- `tests/AutoMancer.Engine.Tests/Core/WaitConditionsTests.cs` — each predicate against a fake `ElementHandle`, true and false cases

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "WaitConditionsTests"
```

**Done when:** `app.WaitForAsync(locator, WaitConditions.IsEnabled())` compiles and behaves identically to a hand-written `e => e.IsEnabled == true` predicate.

---

### Task 2.2.2: Opt-in stale-element re-resolve

**What:** A wrapper — `AppOptions.ReresolveOnStale` flag, checked by the action classes' shared `SendInput`-fallback path — that catches a stale-element COM failure (`COMException` with `UIA_E_ELEMENTNOTAVAILABLE`) and re-resolves once via `RuntimeId` through `ElementResolver` before retrying. Default behavior for every existing call is unchanged; this only activates when a caller explicitly opts in. `RuntimeId` is already a first-class locator strategy (Phase 1, Stage 1.5), so re-resolution reuses `Locator.ByRuntimeId(element.Id)` against the same session rather than introducing a new resolution path.

**Creates:**
- `src/AutoMancer.Engine/AppOptions.cs` — add `ReresolveOnStale` bool, default `false`
- `src/AutoMancer.Engine/Actions/ClickAction.cs` (and the other SendInput-fallback actions) — catch the stale-element COM exception, and when `ReresolveOnStale` is set, re-resolve via `RuntimeId` and retry once before rethrowing

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 2.2.3: Unit tests for Stage 2.2's wait and resilience primitives

**What:** A resilience test simulating a stale COM failure via a mock provider, verifying the opt-in retry re-resolves and succeeds, plus a control test proving non-opted-in calls fail exactly as they do today (regression guard for the "default behavior never changes" claim in Task 2.2.2).

**Creates:**
- `tests/AutoMancer.Engine.Tests/Actions/StaleElementResilienceTests.cs` — mock `IElementOperator`/`IElementProvider` throws the stale-element `COMException` once; with `ReresolveOnStale = true`, `ElementResolver` re-resolves via `RuntimeId` and the action succeeds; with the flag unset (default), the same setup throws exactly as it does in current Phase 1 behavior

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "StaleElementResilienceTests"
```

**Done when:** Both the opt-in-succeeds and opt-out-fails-as-before cases pass.

---

### Task 2.2.4: Extend `LocatorExpect` with `WaitConditions`-backed assertions

**What:** `AutoMancer.Testing`'s `LocatorExpect` gets `ToBeEnabledAsync()` (built on Task 2.2.1's `WaitConditions.IsEnabled()`) and `ToContainTextAsync(string substring)` (built on `WaitConditions.NameContains`) — so `Expect()` gets the same conditions `WaitForAsync` does, instead of only the four hand-written checks Stage 1.9 shipped (`ToHaveNameAsync`/`ToBeVisibleAsync`/`ToHaveTextAsync`/`ToHaveValueAsync`). Each new assertion follows the existing `WaitOrFailAsync` pattern already used by `ToHaveNameAsync` et al. — no new failure-message plumbing needed.

**Creates:**
- `src/AutoMancer.Testing/LocatorExpect.cs` — add `ToBeEnabledAsync(CancellationToken ct = default)`, `ToContainTextAsync(string substring, CancellationToken ct = default)`
- `tests/AutoMancer.Engine.Tests/TestingAdapter/LocatorExpectTests.cs` — extend with cases mirroring the existing `ToHaveNameAsync` tests (matches immediately, retries until match, times out with a descriptive message)

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "FullyQualifiedName~LocatorExpectTests"
```

**Done when:** `WaitForAsync` and `Expect()` share a starter vocabulary instead of `WaitForAsync` alone having one; flaky COM staleness has an opt-in escape hatch that never changes default behavior. Stage 2.2 complete.

---

## Stage 2.3 — Context Menu Fallback Provider

### Task 2.3.1: `ContextMenuAction` — right-click and popup detection

**What:** Right-clicks the target via the existing `ClickAction` machinery (`MouseButton.Right`), then locates the resulting popup window (Win32 class `#32768`, the standard menu window class) via `EnumWindows`/`GetClassName`. Tries the existing UIA `Menu`/`MenuItem` locator path first and only falls back to native `HMENU` enumeration when UIA comes back empty — matching the existing provider-chain philosophy (UIA first, native as last resort) rather than introducing a second, parallel fallback strategy.

**Creates:**
- `src/AutoMancer.Engine/Actions/ContextMenuAction.cs` — `FindPopupWindowAsync(ElementHandle target)` → `IntPtr?` (the `#32768` window, if UIA found nothing)

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 2.3.2: Native menu item enumeration and invocation

**What:** `GetMenu`/`GetSubMenu`/`GetMenuItemInfo` read item text and state from the popup's `HMENU`; `GetMenuItemRect` plus a synthetic click (or `TrackPopupMenuEx` command dispatch) invokes the matched item by name.

**Creates:**
- `src/AutoMancer.Engine/Providers/NativeMethods.cs` — add `GetMenu`, `GetSubMenu`, `GetMenuItemInfo`, `GetMenuItemRect`, `TrackPopupMenuEx` P/Invoke declarations and the `MENUITEMINFO` struct
- `src/AutoMancer.Engine/Actions/ContextMenuAction.cs` — `FindItemByNameAsync(IntPtr hmenu, string itemName)` → item index/rect; `InvokeItemAsync`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 2.3.3: `App.ClickContextMenuItemAsync`

**What:** The façade method: `App.ClickContextMenuItemAsync(Locator target, string itemName)` — right-clicks `target`, tries UIA `Menu`/`MenuItem[Name=itemName]` first, falls back to Tasks 14–15's native path.

**Creates:**
- `src/AutoMancer.Engine/App.cs` — `ClickContextMenuItemAsync(Locator target, string itemName, CancellationToken ct = default)`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors; manual CLI/REPL smoke test right-clicks a live element and clicks a named item by eye.

---

### Task 2.3.4: Integration test for context menu fallback

**What:** Verifies the UIA path is tried first and the native fallback only engages when UIA finds nothing, against an app with a legacy/native context menu (a plain Win32 app without a UIA-exposed menu is the target — Notepad's WinUI3 menus are UIA-visible, so this needs a different test app, e.g. one of the Stage 2.7 example apps or a minimal native test harness).

**Creates:**
- `tests/AutoMancer.Engine.Tests/Integration/ContextMenuFallbackIntegrationTests.cs`

- [ ] **Run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~ContextMenu"
```

**Done when:** `app.ClickContextMenuItemAsync(target, itemName)` finds and clicks a menu item via the native `HMENU`, on an app whose popup menu UIA can't see. Stage 2.3 complete.

---

## Stage 2.4 — Resource Diagnostics

### Task 2.4.1: `ResourceWatch`

**What:** Samples `GetGuiResources` (`GR_GDIOBJECTS`/`GR_USEROBJECTS`) and `Process.WorkingSet64` on a timer. `App.WatchResourcesAsync(TimeSpan interval)` returns an `IAsyncDisposable` sampler plus the snapshot history — disposing stops the timer; the history is readable at any point via a property on the returned handle.

**Creates:**
- `src/AutoMancer.Engine/Providers/NativeMethods.cs` — add `GetGuiResources` P/Invoke, `GR_GDIOBJECTS`/`GR_USEROBJECTS` constants
- `src/AutoMancer.Engine/Diagnostics/ResourceWatch.cs` — `IAsyncDisposable` sampler; `IReadOnlyList<(DateTime Timestamp, int GdiObjects, int UserObjects, long WorkingSetBytes)> Samples`
- `src/AutoMancer.Engine/App.cs` — `WatchResourcesAsync(TimeSpan interval, CancellationToken ct = default)` → `ResourceWatch`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 2.4.2: Integration test for `ResourceWatch`

**What:** Wraps a resource watch around a live app across several launch/kill cycles — reusing the existing Notepad/Paint test fixtures rather than a dedicated new one — and asserts the sample series is non-empty and plausible (handle counts and working-set bytes both present and within a sane range, not necessarily flat).

**Creates:**
- `tests/AutoMancer.Engine.Tests/Integration/ResourceWatchIntegrationTests.cs`

- [ ] **Run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~ResourceWatch"
```

**Done when:** `app.WatchResourcesAsync()` returns a sampled series of GDI/USER handle counts and working-set memory across a run. Stage 2.4 complete.

---

## Stage 2.5 — Accessibility Audit Mode

### Task 2.5.1: `AccessibilityAuditor`

**What:** Walks an `ElementSnapshot` tree from the existing `App.SnapshotAsync`, flags nodes whose `ControlType` is interactive (`Button`, `Edit`, `CheckBox`, …) with a null or empty `Name`. Returns findings with a tree-path breadcrumb (built the same way `XPathEvaluator`'s index-to-element mapping already walks the snapshot tree, reused here for the breadcrumb string).

**Creates:**
- `src/AutoMancer.Engine/Diagnostics/AccessibilityAuditor.cs` — `Audit(IReadOnlyList<ElementSnapshot> tree)` → `IReadOnlyList<AccessibilityFinding>` (`ControlType`, `TreePath`); a fixed `HashSet<string>` of interactive control-type names
- `src/AutoMancer.Engine/App.cs` — `AuditAccessibilityAsync(CancellationToken ct = default)` → snapshots then audits

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 2.5.2: CLI `audit` command

**What:** `automancer audit <session>` prints findings as a table, matching `tree`'s existing output style (reuses the CLI's existing table-printing helper rather than a new formatter).

**Creates:**
- `src/AutoMancer.Cli/Commands/AuditCommand.cs`

- [ ] **Implement, build, and smoke test**

```bash
dotnet build src/AutoMancer.Cli/AutoMancer.Cli.csproj
dotnet run --project src/AutoMancer.Cli -- audit <session-id>
```

**Done when:** `automancer audit <session>` prints a readable table against a live session.

---

### Task 2.5.3: Unit and integration tests for Stage 2.5

**What:** Unit tests for the auditor against a synthetic tree with mixed named/unnamed nodes; an integration test against a live app asserting the report is well-formed — not a fixed finding count, since that drifts across Windows versions.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Diagnostics/AccessibilityAuditorTests.cs` — synthetic tree: named interactive node excluded, unnamed interactive node flagged with correct breadcrumb, unnamed *non*-interactive node (e.g. a `Pane`) excluded
- `tests/AutoMancer.Engine.Tests/Integration/AccessibilityAuditIntegrationTests.cs`

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS), then run integration**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "AccessibilityAuditorTests"
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~AccessibilityAudit"
```

**Done when:** `automancer audit <session>` reports at least one finding against a deliberately-unnamed test control. Stage 2.5 complete.

---

## Stage 2.6 — Visual Provider, Idle Wait, and Visual Regression

> **Two different capabilities share this stage because they share screenshot/pixel-diff plumbing, not because they solve the same problem.** Tasks 23–26 are the actual Visual Provider — a locator fallback for apps with no accessibility tree at all. Tasks 27–28 (`WaitForIdleAsync`, visual regression) are general-purpose and useful against any app, UIA-accessible or not; they only live here because they reuse the capture/compare code Tasks 23–26 already needed. See roadmap-spec.md's Stage 2.6 for the full split.

### Task 2.6.1: `VisualProvider` skeleton

**What:** Implements `IElementProvider` (find-only, matching every other provider's contract). Screenshots the target window via the existing `ScreenshotAction`'s GDI+ `BitBlt` path — no separate capture code — then hands the bitmap to whichever strategy branch (OCR or template) the incoming `Locator.Strategy` selects. Returns `null` on not-found, same as every other provider; never throws.

**Creates:**
- `src/AutoMancer.Engine/Providers/VisualProvider.cs` — `FindElementAsync`/`FindElementsAsync`/`SnapshotTreeAsync` (snapshot returns an empty tree — there's no accessibility tree to walk, only ad hoc find-by-text/find-by-image); `ProviderName => "visual"`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors; `VisualProvider` compiles against `IElementProvider` with no strategy branches wired yet (Tasks 24–25 add them).

---

### Task 2.6.2: OCR path — `automancer:text` strategy

**What:** `Windows.Media.Ocr.OcrEngine` (on-device, no external service, no extra package — available via the `windows10.x` TFM suffix per CLAUDE.md). Adds `automancer:text` as a locator value the `VisualProvider` recognizes: runs OCR against the captured bitmap, matches recognized word/line bounding boxes against the locator's text (exact or substring — substring by default, matching how `ToHaveText` already works elsewhere), and returns a synthetic `ElementHandle` whose `BoundingRect` is the matched text's screen rect and whose `NativeHandle` is the OCR result (opaque, per the `ElementHandle` contract).

**Creates:**
- `src/AutoMancer.Engine/Core/LocatorStrategy.cs` — add `AutoMancerText` (or reuse `Property`'s envelope pattern — pick whichever keeps `VisualProvider`'s dispatch simplest)
- `src/AutoMancer.Engine/Core/Locator.cs` — add `Locator ByText(string text)` (the `app.find(text: ...)` surface referenced in the roadmap is the SDK-layer shorthand for this in Phase 3; the engine-level entry point is this factory)
- `src/AutoMancer.Engine/Providers/VisualProvider.cs` — OCR branch using `OcrEngine.TryCreateFromUserProfileLanguages()` → `RecognizeAsync`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors. Live behavior verified in Task 2.6.4.

---

### Task 2.6.3: Template matching — `automancer:image` strategy

**What:** `OpenCvSharp4.Windows` — a new NuGet dependency, added to `AutoMancer.Engine.csproj`. `Locator.ByImage(byte[] templatePng)` (or a base64-string overload, matching Phase 3's `automancer:image` wire format) runs `Cv2.MatchTemplate` against the captured window bitmap and the decoded template, thresholded for a confident match, returning a synthetic `ElementHandle` at the matched region.

**⚠️ Verify `OpenCvSharp4.Windows` actually has a build compatible with `net10.0-windows10.0.22621.0` before starting this task.** It's a native-wrapping package (bundles OpenCV's native binaries), and those tend to lag behind bleeding-edge target framework versions. If no compatible version is published yet, options are: target `net8.0-windows`/`net9.0-windows` for just this one dependency via multi-targeting (adds real build complexity), pin an older OpenCvSharp4 release and confirm it still loads under .NET 10, or swap to a different template-matching approach (a hand-rolled normalized cross-correlation over `System.Drawing`/`System.Numerics` is slower but dependency-free) if neither is acceptable. Don't assume this "just works" — confirm it first.

**Creates:**
- `src/AutoMancer.Engine/AutoMancer.Engine.csproj` — add `OpenCvSharp4.Windows` package reference
- `src/AutoMancer.Engine/Core/Locator.cs` — add `ByImage(byte[] templatePng)` factory
- `src/AutoMancer.Engine/Providers/VisualProvider.cs` — template-match branch

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors, `OpenCvSharp4.Windows` restores cleanly on `net10.0-windows10.0.22621.0`.

---

### Task 2.6.4: Wire `visual` into the default provider chain

**What:** Adds `"visual"` to the default `ElementProviderOptions.ProviderChain`, as the last fallback after `win32` — an integration test against a no-UIA test app (e.g. one written in raw GDI/Direct2D with no accessibility support) proves `app.find(text: "Submit Order")` resolves through the full chain when no other provider can see the element.

**⚠️ Two existing unit tests assert the exact 3-element default chain and will fail once `"visual"` is added — update them as part of this task, not as an incidental side effect discovered later.** `tests/AutoMancer.Engine.Tests/AppOptionsTests.cs` and `tests/AutoMancer.Engine.Tests/Core/ElementProviderOptionsTests.cs` both currently assert `Assert.Equal(["uia3", "uia2", "win32"], options.ProviderChain)` against `AppOptions.Default`/`ElementProviderOptions.Default`. Both need their expected array updated to `["uia3", "uia2", "win32", "visual"]`.

**Creates:**
- `src/AutoMancer.Engine/Core/ElementProviderOptions.cs` — default chain becomes `["uia3", "uia2", "win32", "visual"]`
- `tests/AutoMancer.Engine.Tests/AppOptionsTests.cs`, `tests/AutoMancer.Engine.Tests/Core/ElementProviderOptionsTests.cs` — update the expected default-chain array in both (see warning above)
- `tests/AutoMancer.Engine.Tests/Integration/VisualProviderIntegrationTests.cs` — OCR-text find and template-image find, both against a no-UIA test app

- [ ] **Run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~VisualProvider"
```

**Done when:** `app.find(text: "Submit Order")` finds a button by its visible text in an app with no UIA elements.

---

### Task 2.6.5: `App.WaitForIdleAsync()`

**What:** Diffs consecutive `ScreenshotAsync()` captures at a short interval until two frames match within a pixel-difference threshold, or a timeout is hit. Replaces the ad-hoc `Task.Delay(300) // flyout animation` waits already scattered through the Phase 1 integration test suite with a real settledness check — reuses the pixel-compare plumbing Task 2.6.3 built for template matching rather than a separate diff implementation.

**Creates:**
- `src/AutoMancer.Engine/App.cs` — `WaitForIdleAsync(TimeSpan? pollInterval = null, TimeSpan? timeout = null, CancellationToken ct = default)`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** `app.WaitForIdleAsync()` resolves once consecutive screenshots stop changing. As a follow-up cleanup (not required for this task's own "done"), the scattered `Task.Delay(300)` animation waits in the existing integration suite are good candidates to replace with this.

---

### Task 2.6.6: Visual regression snapshot assertion

**What:** Captures the current window/element region and pixel-diffs it against a stored baseline PNG, failing past a configurable difference threshold. Reuses the screenshot/pixel-compare plumbing built for template matching in Task 2.6.3.

**`DpiHelper.PhysicalToLogical` is not directly reusable here — check its actual signature before assuming otherwise.** `ScreenshotAsync()` captures physical pixels, and the same logical window is a different physical *pixel size* at 100% vs. 150% DPI, so a naive pixel-diff spuriously fails whenever the baseline and the live capture were recorded at different scale factors. But `DpiHelper.PhysicalToLogical(double physicalX, double physicalY, int dpi, Rect windowRect)` converts a single *coordinate pair*, not a bitmap — there's nothing to "call it on" an image. What's actually needed is a bitmap **resize** using the DPI scale ratio (`dpi / 96.0` — the same ratio `DpiHelper` already computes internally as `BaseDpi`-relative `scale`, just not exposed as a standalone factor): resize whichever of the baseline/live capture was taken at the non-reference DPI down (or up) to match the other's pixel dimensions before diffing. Add a small `DpiHelper.GetScale(int dpi) => dpi / 96.0` (or inline the ratio directly in `VisualRegression`) rather than trying to route this through `PhysicalToLogical`.

**Creates:**
- `src/AutoMancer.Engine/Dpi/DpiHelper.cs` — optionally add `GetScale(int dpi)`, exposing the `dpi / 96.0` ratio the other methods already compute internally, for `VisualRegression`'s bitmap-resize step to reuse
- `src/AutoMancer.Engine/Diagnostics/VisualRegression.cs` — `CompareToBaseline(byte[] currentPng, string baselinePath, double maxDifferenceRatio = 0.01)` → pass/fail + diff ratio; resizes whichever capture was taken at a different DPI than the other before diffing
- `src/AutoMancer.Testing/LocatorExpect.cs` (or `ElementExpect.cs`) — `ToMatchBaselineAsync(string baselinePath)` assertion, following the existing `ExpectFailedError` message pattern
- `tests/AutoMancer.Engine.Tests/Diagnostics/VisualRegressionTests.cs` — identical images compare equal; a shifted/recolored image exceeds the threshold; a 150%-captured image resized to match a 100%-recorded baseline's dimensions compares equal for identical content

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "VisualRegressionTests"
```

**Done when:** A visual regression assertion fails when a window's rendered output drifts from a stored baseline. Stage 2.6 complete — the full UIA3 → UIA2 → Win32 → Visual fallback chain is done, plus animation-settle waits and pixel-level regression assertions. This stage is entirely engine-side; exposing `VisualProvider` over HTTP is Phase 3's job (daemon-spec.md Task 3.2.8).

---

## Stage 2.7 — Engine Hardening and Examples

### Task 2.7.1: Annotated error screenshots

**What:** On `ElementNotFoundError`, capture a screenshot and draw a red overlay around the search area / closest-match bounding rect via GDI+, save to `%TEMP%`. A new `AppOptions`/`ElementProviderOptions` flag (off by default) turns this on. The engine sibling of Stage 1.9's on-failure diagnostics, for direct `App` consumers rather than `Expect()` — same idea (annotate and save on failure), different trigger point (thrown exception vs. `Expect()`'s own retry loop).

**Creates:**
- `src/AutoMancer.Engine/Core/ElementProviderOptions.cs` — add `AnnotateFailureScreenshots` bool, default `false`
- `src/AutoMancer.Engine/Core/ElementResolver.cs` — on throwing `ElementNotFoundError` with the flag set, capture + annotate + save, folding the path into the exception (mirroring `ExpectFailedError`'s existing screenshot-path-in-message convention)

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** A failed find under the debug flag leaves an annotated PNG in `%TEMP%`.

---

### Task 2.7.2: DPI compat matrix

**What:** Runs the Notepad integration test suite at 100%, 125%, and 150% DPI, asserting identical logical coordinates across all three. This is a test-execution exercise, not new production code — it validates `DpiHelper`'s conversion logic (already present in Phase 1, unwired into the hot path per its own design note) actually holds across the three most common Windows scaling factors.

**Creates:**
- Nothing new in `src/` — this task is a verification pass. If it finds a genuine DPI-dependent coordinate bug, fix it in the offending action/provider and note the fix here.

- [ ] **Run the suite at each DPI setting** (Windows Settings → Display → Scale, or `SystemParametersInfo`/registry-driven automation if scripting this)

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"
```

**Done when:** The Notepad integration suite passes at 100%, 125%, and 150% DPI with identical logical coordinates.

---

### Task 2.7.3: Windows 10/11 compat

**What:** Runs the full `AutoMancer.Engine.Tests` integration suite on both Windows 10 and Windows 11; fixes any behavioral differences found (most likely candidates: WinUI3 Notepad's tree shape, which already has version-drift caveats noted in the Phase 1 test suite, and any Win32-provider fallback timing).

**Creates:**
- Nothing new in `src/` unless a genuine OS-version bug is found — same shape as Task 2.7.2.

- [ ] **Run the full integration suite on both OS versions**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration"
```

**Done when:** The full `AutoMancer.Engine.Tests` integration suite passes on both Windows 10 and 11.

---

### Task 2.7.4: Example projects

**What:** Four runnable example projects demonstrating the engine's public surface for new C# consumers — `examples/notepad/` (the README's Calculator-style walkthrough, expanded), `examples/winforms-calculator/` (a classic Win32/WinForms app, exercising the UIA2 fallback path), `examples/legacy-no-uia/` (a Visual Provider showcase — the same no-UIA test app Task 2.6.4 already needs), `examples/xunit-test-adapter/` (a minimal `AutoMancer.Testing.XUnit` walkthrough, complementing `samples/ConsumerNotepadTests`).

**Creates:**
- `examples/notepad/`, `examples/winforms-calculator/`, `examples/legacy-no-uia/`, `examples/xunit-test-adapter/` — each a minimal runnable console or test project with its own short README

- [ ] **Build and run each example**

```bash
dotnet build examples/notepad/
dotnet build examples/winforms-calculator/
dotnet build examples/legacy-no-uia/
dotnet build examples/xunit-test-adapter/
```

**Done when:** All four example projects build and run. Stage 2.7 complete — DPI/OS compat confidence, annotated failure diagnostics, and working examples. This stage is entirely engine-side; exposing the debug-screenshot flag over HTTP is Phase 3's job (daemon-spec.md Task 3.2.5/`automancer:debugScreenshots`).

---

## Stage 2.8 — Extended UIA Pattern and Clipboard Actions

> Checkboxes, list/combo selections, grid cells, sliders/progress bars, and clipboard text are as common in real Windows apps as clicking a button or typing into a field — Phase 1's action set has no answer for any of them, independent of anything else in the roadmap. Phase 3's daemon and SDKs also happen to assume most of this surface exists (`PatternEndpoints`/`ClipboardEndpoints`, `ComboBox`/`DataGrid` control classes) — a secondary bonus, not the primary reason to build it, since Phase 3 has no start date. See roadmap-spec.md's Stage 2.8 for the full framing.

### Task 2.8.1: `ClipboardAction`

**What:** `GetTextAsync`/`SetTextAsync`, not element-scoped — same category as `ScreenshotAction`/`WindowAction`, which also operate on the session/window rather than a resolved `ElementHandle`. `App.GetClipboardTextAsync()`/`SetClipboardTextAsync(string)` are the façade methods.

**Use `System.Windows.Clipboard` (WPF), not raw `OpenClipboard`/`GetClipboardData` P/Invoke.** `AutoMancer.Engine.csproj` already has `<UseWPF>true</UseWPF>` (enabled for the UIA2 provider) — the managed `System.Windows.Clipboard.GetText()`/`SetText(string)` API is already available for free. Hand-rolling `OpenClipboard`/`GetClipboardData`/`SetClipboardData`/`GlobalLock`/`GlobalUnlock` P/Invoke reimplements something already sitting one `using` away, and is meaningfully more error-prone (manual global-memory handle lifetime, manual retry-on-`OpenClipboard`-contention logic WPF's wrapper already handles). `Clipboard.GetText()`/`SetText()` are synchronous and STA-threading-sensitive like most clipboard APIs — wrap the call in `Task.Run` the same way other synchronous Win32-adjacent calls in this codebase already do (matching `ClickAction`'s `SendInput` dispatch pattern), rather than adding a new async-wrapping convention.

**Creates:**
- `src/AutoMancer.Engine/Actions/ClipboardAction.cs` — `GetTextAsync()`/`SetTextAsync(string)`, each a `Task.Run` wrapper around `System.Windows.Clipboard.GetText()`/`SetText(text)`
- `src/AutoMancer.Engine/App.cs` — `GetClipboardTextAsync(CancellationToken ct = default)`, `SetClipboardTextAsync(string text, CancellationToken ct = default)`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 2.8.2: `ToggleAction`

**What:** `TogglePattern.Toggle()`, mirroring `ScrollAction`'s no-op-when-unsupported shape from Phase 1 (checks `NativeHandle is IUIAutomationElement`, then `GetCurrentPattern(UIA_TogglePatternId)`; no-ops silently if either check fails, exactly like `ScrollAction`/`SetFocusAction` already do — no new dispatch convention). `App.ToggleAsync(Locator)`.

**Creates:**
- `src/AutoMancer.Engine/Actions/ToggleAction.cs`
- `src/AutoMancer.Engine/App.cs` — `ToggleAsync(Locator locator, CancellationToken ct = default)`
- `tests/AutoMancer.Engine.Tests/Actions/ToggleActionTests.cs` — no-op for non-UIA handle, no-op when `TogglePattern` unsupported, invokes `Toggle()` when supported (mirroring `ScrollActionTests`'s three-case shape)

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "ToggleActionTests"
```

**Done when:** `app.ToggleAsync(locator)` flips a checkbox's `Toggle.ToggleState` (verified live in Task 2.8.6).

---

### Task 2.8.3: `ExpandCollapseAction`

**What:** `ExpandCollapsePattern.Expand()`/`Collapse()`, same no-op-when-unsupported shape as Task 2.8.2. `App.ExpandAsync(Locator)`/`CollapseAsync(Locator)`.

**Creates:**
- `src/AutoMancer.Engine/Actions/ExpandCollapseAction.cs`
- `src/AutoMancer.Engine/App.cs` — `ExpandAsync(Locator locator, CancellationToken ct = default)`, `CollapseAsync(Locator locator, CancellationToken ct = default)`
- `tests/AutoMancer.Engine.Tests/Actions/ExpandCollapseActionTests.cs`

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "ExpandCollapseActionTests"
```

**Done when:** Both `ExpandCollapseActionTests` and a build pass; each action no-ops safely on an unsupported element.

---

### Task 2.8.4: `SelectionAction`

**What:** `SelectionItemPattern.Select()`/`AddToSelection()`/`RemoveFromSelection()` for writes; `SelectionItemPattern.CurrentIsSelected` for a single item's own state; `SelectionPattern.GetCurrentSelection()`/`CanSelectMultiple` for container-level reads. `App.SelectAsync(Locator)`, `AddToSelectionAsync(Locator)`, `RemoveFromSelectionAsync(Locator)`, `IsSelectedAsync(Locator)`, `GetSelectedItemsAsync(Locator)` — the last one resolves the *container* locator, reads `SelectionPattern`, and maps each selected native element back to an `ElementHandle` the same way `Uia3Provider`'s tree walk already wraps elements.

**Creates:**
- `src/AutoMancer.Engine/Actions/SelectionAction.cs`
- `src/AutoMancer.Engine/App.cs` — the five methods above
- `tests/AutoMancer.Engine.Tests/Actions/SelectionActionTests.cs`

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "SelectionActionTests"
```

**Done when:** `app.SelectAsync(locator)`/`GetSelectedItemsAsync(locator)` round-trip a list selection (verified live in Task 2.8.6).

---

### Task 2.8.5: `GridAction`

**What:** `GridPattern.CurrentRowCount`/`CurrentColumnCount`/`GetItem(row, col)`, falling back to `TablePattern` when only that's supported (some controls expose `TablePattern` without `GridPattern`, or vice versa — check both, prefer `GridPattern` when both are present since it's the more direct row/col API). `App.GetGridRowCountAsync(Locator)`, `GetGridColumnCountAsync(Locator)`, `GetGridCellAsync(Locator, int row, int col)` → `ElementHandle` for the cell, wrapped the same way `Uia3Provider` wraps any other element, so existing actions/locators work on the returned cell unchanged (a `GetGridCellAsync` result is a normal `ElementHandle` — `ClickAsync`, `Locator.ByProperty`, everything else, all just work on it).

**Creates:**
- `src/AutoMancer.Engine/Actions/GridAction.cs`
- `src/AutoMancer.Engine/App.cs` — the three methods above
- `tests/AutoMancer.Engine.Tests/Actions/GridActionTests.cs`

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "GridActionTests"
```

**Done when:** `app.GetGridCellAsync(locator, row, col)` returns the element at a `DataGrid` cell (verified live in Task 2.8.6).

---

### Task 2.8.6: Unit and integration tests for Stage 2.8

**What:** Unit tests for each action's no-op/unsupported-pattern path (mirroring `ScrollActionTests`/`SetFocusActionTests` — largely already covered per-task in Tasks 34–37, this task is the integration-level closeout). Integration tests: toggle a checkbox, select/multi-select a list, read a `ListView`/`DataGrid`-style control's row and column counts and fetch a specific cell, clipboard round-trip.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Integration/ExtendedPatternIntegrationTests.cs` — toggle, select/multi-select, grid row/column/cell, clipboard round-trip, all against a live test app with the relevant controls (a `ListView` and a checkbox are enough for most of this; `DataGrid`-pattern coverage may need a WinForms/WPF example app if Notepad has nothing suitable — Task 2.7.4's `examples/winforms-calculator/` or a dedicated fixture app can double as the test target)

- [ ] **Run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~ExtendedPattern"
```

**Done when:** All four "Done when" claims at the top of this stage hold against a live app.

---

### Task 2.8.7: `RangeValueAction`

**What:** Sliders, scrollbars, and progress bars expose their numeric value through `RangeValuePattern`, which nothing in the engine touches today — Phase 1's `ClickAction`/`DragAction` can only fake setting one by computing pixel offsets along the control's track, which is fragile across DPI and control-size changes. `RangeValuePattern.SetValue(double)` sets it directly; reads come from `CurrentValue`/`CurrentMinimum`/`CurrentMaximum`/`CurrentLargeChange`/`CurrentSmallChange`. Mirrors `ToggleAction`'s no-op-when-unsupported shape — same `IElementOperator`-optional pattern every Stage 2.8 action already follows.

**Creates:**
- `src/AutoMancer.Engine/Actions/RangeValueAction.cs` — `SetValueAsync(double)`, `GetValueAsync()` → `RangeValueInfo(double Value, double Minimum, double Maximum)`
- `src/AutoMancer.Engine/App.cs` — `SetRangeValueAsync(Locator, double value, CancellationToken ct = default)`, `GetRangeValueAsync(Locator, CancellationToken ct = default)`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 2.8.8: Unit and integration tests for `RangeValueAction`

**What:** Unit test for the no-op/unsupported-pattern path (mirroring `ScrollActionTests`/`SetFocusActionTests`, same as Task 2.8.6 did for the other five actions). Integration test: set a live slider or progress bar's value and read it back, and confirm `CurrentMinimum`/`CurrentMaximum` match the control's declared range.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Actions/RangeValueActionTests.cs`
- `tests/AutoMancer.Engine.Tests/Integration/ExtendedPatternIntegrationTests.cs` — add a range-value case alongside Task 2.8.6's toggle/select/grid/clipboard cases

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "FullyQualifiedName~RangeValue"
```

**Done when:** `app.SetRangeValueAsync(locator, value)`/`GetRangeValueAsync(locator)` round-trip a live slider/progress bar's value. **Phase 2 complete.** The daemon's `PatternEndpoints`/`ClipboardEndpoints` (daemon-spec.md Task 3.2.6) and the SDKs' `ComboBox`/`DataGrid` controls (sdks-spec.md Task 3.3.4/3.4.4) can now delegate to real `App` methods instead of reaching around the engine.

---

## Constraints

- Every `.cs` file starts with `// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.`
- `net10.0-windows10.0.22621.0` only — no cross-platform guards
- `System.Text.Json` throughout — no Newtonsoft.Json
- No DI container — new classes wired manually at call sites, same as Phase 1
- `ElementHandle` stays opaque throughout this phase — only `Id` (string) and `NativeHandle` (object) are public. Every new action/provider that needs native pattern access goes through the existing `Operator`/`NativeHandle` seam, never a new public field
- Every new UIA-pattern action (`ToggleAction`, `ExpandCollapseAction`, `SelectionAction`, `GridAction`, `RangeValueAction`) follows `ScrollAction`/`SetFocusAction`'s established shape: no-op silently when the handle isn't a UIA element or the pattern isn't supported, never throw for "pattern not available"
- `VisualProvider` returns `null`/empty on not-found and never throws, matching every other provider — only `ElementResolver` throws `ElementNotFoundError`
- Nothing in this phase modifies an existing Phase 1 public method's signature — every task adds new surface, it doesn't change what's already shipped
- Nothing in this phase requires `AutoMancer.Daemon` to exist — Phase 3 wraps whatever this phase produces, not the other way around

---

## Quick-Reference: Definition of Done

Mirrors roadmap-spec.md's per-phase checklist; see there for the authoritative, currently-tracked version. Duplicated here per-task for convenience while working through this document:

- [ ] Task 2.1.6 — spatial and property locators find elements the fixed strategy set can't reach
- [ ] Task 2.1.9 — scoped find disambiguates identically-matched elements, and virtualized-list find locates unrealized items
- [ ] Task 2.2.4 — `Expect()` and `WaitForAsync` share a wait-condition vocabulary
- [ ] Task 2.3.4 — native context menu fallback works when UIA can't see the popup
- [ ] Task 2.4.2 — resource watch returns a plausible sample series against a live app
- [ ] Task 2.5.3 — accessibility audit reports a deliberately-unnamed control
- [ ] Task 2.6.4 — visual provider finds an element by rendered text with no UIA tree
- [ ] Task 2.6.6 — visual regression assertion fails on a drifted baseline
- [ ] Task 2.7.2–2.7.3 — DPI and Windows 10/11 compat confirmed
- [ ] Task 2.7.4 — all four example projects build and run
- [ ] Task 2.8.6 — toggle/select/grid/clipboard all verified against a live app
- [ ] Task 2.8.8 — a slider/progress bar's value round-trips through `RangeValueAction`
