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
│   ├── ElementResolver.cs            opt-in stale-element re-resolve; FindScopedAsync/FindAllScopedAsync
│   └── IElementProvider.cs           FindScopedElementAsync/FindScopedElementsAsync take a required ElementHandle scope
├── Providers/
│   ├── Uia3Provider.cs, Uia2Provider.cs, Win32Provider.cs
│   │                                 implement the scoped find methods, using scope as the search root
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

- [x] **Implement and build**

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

- [x] **Write tests, run (expect FAIL), implement, run (expect PASS)**

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

- [x] **Implement and build**

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

- [x] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** `Locator.ByProperty(UiaProperty.HelpText, "Search")` compiles and produces the same resolved `Locator` as the equivalent raw-int call.

---

### Task 2.1.5: Custom property GUID resolution — `RegisterCustomPropertyAsync`

**What:** For app-registered custom properties, which don't have stable integer IDs across processes — a custom property's numeric ID is assigned at registration time and can't be hardcoded.

**⚠️ Verify the exact `IUIAutomationRegistrar` shape against `Interop.UIAutomationClient`'s generated types before implementing this task — the signature below is written from general UIA COM documentation, not confirmed against this project's actual package version.** As documented, `IUIAutomationRegistrar.RegisterProperty` takes a full `UIAutomationPropertyInfo { Guid guid; string programmaticName; UIAutomationType type; }`, not just a GUID — the caller has to already know the property's declared name and value type to resolve it, the same way the app that originally registered it did. A bare `Locator.ByProperty(Guid, object value)` factory can't supply that on its own, so **this task exposes a one-time registration call instead of a new `Locator` factory**, and the resolved ID feeds into Task 2.1.3's existing `ByProperty(int, object)`:

```csharp
// A live COM call, so it can't happen at Locator-construction time.
public async Task<int> RegisterCustomPropertyAsync(Guid propertyGuid, string programmaticName, UiaAutomationType type, CancellationToken ct = default);
```
Callers do `var id = await app.RegisterCustomPropertyAsync(guid, "MyApp.Status", UiaAutomationType.String); app.FindAsync(Locator.ByProperty(id, "Ready"))` — one extra line, but it doesn't require guessing at a `Locator` shape that can't actually carry enough information to work.

**Creates:**
- `src/AutoMancer.Engine/Providers/UiaRegistrarInterop.cs` — `RegisterCustomPropertyAsync(Guid, string, UiaAutomationType, CancellationToken)` → `int`, via `IUIAutomationRegistrar.RegisterProperty`. Not on `Uia3Provider`: `CUIAutomationRegistrar` is a standalone COM object with no dependency on either provider's automation root, so it lives in its own file rather than implying a UIA3-specific dependency that doesn't exist
- `src/AutoMancer.Engine/App.cs` — thin delegating overload

- [x] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors. Full behavior is only verifiable live (Task 2.1.6) since resolution requires a real UIA3 session against an app that registered the property, and requires confirming the `IUIAutomationRegistrar` signature noted above.

---

### Task 2.1.6: Unit and integration tests for Stage 2.1

**What:** Closes out the stage's locator coverage with the three end-to-end cases the "Done when" at the top of this section names.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Integration/Calculator/CalculatorSpatialLocatorIntegrationTests.cs` — spatial locator finds a live app's unlabeled `Edit` control next to its label (landed against Calculator's number pad, not a generic app, for unambiguous spatial neighbors)
- `tests/AutoMancer.Engine.Tests/Integration/Notepad/PropertyLocatorIntegrationTests.cs` — `Locator.ByProperty(UiaProperty.HelpText, ...)` finds an element via the named enum; a custom-property lookup against a test app that registers one via `AutomationProperties.RegisterProperty` (WPF) or an equivalent native registration, using `RegisterCustomPropertyAsync` (Task 2.1.5) to resolve the ID and `Locator.ByProperty(int, object)` (Task 2.1.3) to query it

- [ ] **Run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~Locator"
```

**Done when:** Locators cover the three cases the fixed strategy set can't reach: elements with no name, built-in properties nobody added a named strategy for, and properties that only exist because a specific app registered them.

> The custom-property case isn't actually covered: `PropertyLocatorIntegrationTests.cs`'s own topline comment says outright that no fixture app in this repo registers a custom property (it needs raw `IRawElementProviderSimple` COM interop, not just `AutomationPeer` overrides), so only the built-in `HelpText` case is tested live. `UiaRegistrarInteropTests.cs` unit-tests `RegisterCustomPropertyAsync`'s COM interop in isolation, never end-to-end against a real registered property. Leave this box unchecked until a fixture app that registers one exists.

---

### Task 2.1.7: Scoped/relative find

**What:** Every find today goes through `ElementResolver.FindAsync(locator, session, ct)` — always resolving against the whole session, with no way to restrict a search to a specific element's subtree. That's a real gap once a window has two elements that match the same locator in different places (e.g. a "Cancel" button on the main form *and* one in a dialog) — there's no way to say "only search inside this dialog." Adds scoped find as a distinctly-named `FindScopedAsync`/`FindAllScopedAsync` pair rather than an overload or a widened parameter on `FindAsync`/`FindAllAsync` (see below for why) — the original methods are completely untouched, so every existing call site keeps compiling and behaving identically.

**⚠️ Scope became its own method name, not a parameter on `FindAsync` — two rounds of real friction pushed it there, not aesthetics.** The first-pass implementation *did* try widening `App.FindAsync`/`FindAllAsync` via an `ElementHandle? scope` overload (avoiding a true in-place widen, since `src/AutoMancer.Testing/LocatorExpect.cs` already calls `_app.FindAllAsync(_locator, ct)` with `ct` positional, and inserting a new parameter before it would silently rebind `ct` to `scope`). That worked, but `ElementResolver.FindAsync`/`FindAllAsync` *were* widened in place with an optional `ElementHandle? scope = null` sitting between `session` and `ct` — which meant every internal call site that used to pass `ct` positionally as the next argument now had to switch to a named `ct: ct` to avoid binding it to `scope` instead. That recurring annoyance, plus a desire to make "did you mean to scope this" a compile-time choice rather than a runtime null-check, led to the final shape: `FindScopedAsync`/`FindAllScopedAsync` as separate methods with a *required* `scope` parameter, sitting alongside `FindAsync`/`FindAllAsync` untouched. The public `App`-level `ElementHandle`-scope form was dropped entirely once nothing needed it (see Task 2.1.7's own follow-up work) — `App` only exposes the `Locator`-scope form, which re-resolves scope fresh every call.

**Creates/Modifies:**
- `src/AutoMancer.Engine/Core/IElementProvider.cs` — `FindElementAsync`/`FindElementsAsync` stay unscoped; new `FindScopedElementAsync`/`FindScopedElementsAsync` take a required `ElementHandle scope`. Four in-assembly implementers, no external callers, so splitting the interface outright (rather than widening in place) cost nothing beyond the extra method count
- `src/AutoMancer.Engine/Providers/Uia3Provider.cs` / `Uia2Provider.cs` / `Win32Provider.cs` — each factors its search logic into a private helper parameterized by *how to get the root* (session root vs. `scope.NativeHandle`), so the four public methods per provider are thin one-liners with no `scope is null ? … : …` branch anywhere
- `src/AutoMancer.Engine/Core/ElementResolver.cs` — `FindAsync`/`FindAllAsync` stay 3-parameter and untouched; `FindScopedAsync`/`FindAllScopedAsync` are new methods (an `internal ElementHandle`-scope overload plus a `public Locator`-scope overload that resolves scope fresh, then calls the internal one)
- `src/AutoMancer.Engine/App.cs` — `FindScopedAsync(Locator, Locator, CancellationToken)` / `FindAllScopedAsync` are the only public scope surface; `FindAsync`/`FindAllAsync` are unchanged

- [x] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors; `app.FindAsync(locator)` and every other existing call site (including `LocatorExpect.cs`'s `FindAllAsync(_locator, ct)`) compile and behave identically to today — no existing test regresses.

---

### Task 2.1.8: Virtualized-list find — `App.FindByScrollingAsync`

**What:** Virtualizing `ListView`/`ComboBox`/`DataGrid` controls only realize visible rows in the UIA tree — an item 500 rows down simply isn't there to find yet, and `ScrollAction` (`ScrollItemPattern.ScrollIntoView`) only helps once you already have an `ElementHandle` for the item, which is exactly the problem. `App.FindByScrollingAsync(ElementHandle container, Locator itemLocator, int maxScrolls = 20, CancellationToken ct = default)` closes that gap: repeatedly try Task 2.1.7's `FindScopedAsync` against `container`, and if not found, scroll `container` one notch via the existing `ScrollWheelAction` and retry.

**Loop termination:** reads `IUIAutomationScrollPattern.CurrentVerticalScrollPercent` on `container` before and after each scroll. If the percentage is unchanged across two consecutive scroll attempts, the container has hit the end of its scrollable range and the item genuinely isn't there — throw `ElementNotFoundError` immediately rather than burning through the rest of `maxScrolls` on a container that's stopped moving.

**Creates:**
- `src/AutoMancer.Engine/App.cs` — `FindByScrollingAsync(ElementHandle container, Locator itemLocator, int maxScrolls = 20, CancellationToken ct = default)`, built on Task 2.1.7's `FindScopedAsync` and the existing `ScrollWheelAction`

- [x] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

> Task 2.2.2 later changed `container` from an `ElementHandle` to a `Locator` (`FindByScrollingAsync(Locator containerLocator, ...)`), so a stale container can be re-found by its locator.

---

### Task 2.1.9: Unit and integration tests for Tasks 2.1.7–2.1.8

**What:** Closes out scoped find and virtualized-list find with the two end-to-end cases the Stage 2.1 "Done when" line names.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Core/ScopedFindTests.cs` — against a fake provider, a scoped find matches only descendants of the given `scope` element, ignoring identically-matched elements elsewhere in the tree
- `tests/AutoMancer.Engine.Tests/Core/FindByScrollingTests.cs` — against a fake provider/scroll-pattern that "reveals" a target item only after N simulated scroll calls; a control test proving the loop throws `ElementNotFoundError` (not an infinite loop) once the simulated scroll percentage stalls across two attempts
- `tests/AutoMancer.Engine.Tests/Integration/ScopedFindIntegrationTests.cs` — scoped find disambiguates two identically-named buttons in different dialogs of a live app
- `tests/AutoMancer.Engine.Tests/Integration/VirtualizedListFindIntegrationTests.cs` — `FindByScrollingAsync` locates a far-down item in a long live `ListView`

- [x] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "FullyQualifiedName~ScopedFind|FullyQualifiedName~FindByScrolling"
```

**Done when:** `app.FindScopedAsync(locator, element)` only matches descendants of `element`; `app.FindByScrollingAsync(container, itemLocator)` finds a far-down item in a live virtualized list and throws (not hangs) when the item genuinely isn't there. Stage 2.1 complete.

> Unit coverage (`ScopedFindTests`, `FindByScrollingTests`) passes. Run live: `ScopedFindIntegrationTests` — all 4 pass, consistently. `VirtualizedListFindIntegrationTests` initially failed on real finds even at zero-scroll — two real bugs, both fixed: (1) Windows hides known file extensions by default, so a `file-0010.txt` row's UIA Name is `file-0010`, not `file-0010.txt` — locators matched on the on-disk name and never matched anything, at any scroll position; (2) a row can be realized in the tree right at a scroll's edge, `IsOffscreen=true`, before it's actually inside the visible viewport — `FindByScrollingCoreAsync` returned it anyway, so a caller acting on it (e.g. double-click) hit `ElementNotInteractableError`; fixed by calling `ScrollAction`'s `ScrollIntoView` and re-resolving via `RuntimeId` before returning (`App.ResolveFullyIntoViewAsync`). With both fixed, a clean run passes 4/5 with zero code-attributable failures — the remaining occasional `Assert.NotNull(dialog)` is confirmed environmental (an isolated timing check found the dialog appearing in 647ms, well under its 3s budget), not a defect in this feature.

---

## Stage 2.2 — Wait and Resilience Primitives

### Task 2.2.1: `WaitConditions` static class

**What:** Canned `Func<ElementHandle, bool>` factories — `IsVisible()`, `NameEquals(string)`, `NameContains(string)`, `TextEquals(string)`, `IsEnabled()`, plus the trivial field-based siblings `AutomationIdEquals(string)`, `ClassNameEquals(string)`, `ControlTypeEquals(string)`, `TextMatches(Regex)`, and the `Not(condition)` combinator — mirroring Selenium's `ExpectedConditions`. Each is a small predicate closure; this adds no new engine surface beyond the static class itself, since every predicate is a drop-in for `App.WaitForAsync`'s existing `Func<ElementHandle, bool>` parameter.

**Creates:**
- `src/AutoMancer.Engine/Core/WaitConditions.cs` — the ten factories above
- `tests/AutoMancer.Engine.Tests/Core/WaitConditionsTests.cs` — each predicate against a fake `ElementHandle`, true and false cases

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "WaitConditionsTests"
```

**Done when:** `app.WaitForAsync(locator, WaitConditions.IsEnabled())` compiles and behaves identically to a hand-written `e => e.IsEnabled == true` predicate.

**Future extension:** `NumberOfElementsToBe(locator, n)` (and `...MoreThan`/`...LessThan`) — a common ask for "wait until a list/grid finishes populating," but a different method shape from everything else here, since it needs `FindAllAsync`'s count rather than a single `ElementHandle`. Not part of this task's `Func<ElementHandle, bool>` contract.

---

### Task 2.2.2: Stale-element detection, `StaleElementError`, and opt-out auto re-resolve

**What:** Selenium's `StaleElementReferenceException` convention, not a silent swallow: a genuinely stale element (per the shared definition under "What counts as stale" below, checked in `ElementInputHelpers.EnsureForeground` and the other detection sites) always throws the new `StaleElementError` — distinct from the pre-existing, unrelated catch-and-continue in the same method for elements that just fail this one property lookup without actually being gone (the documented VLC split-button `MenuItem` case). `AppOptions.ReresolveOnStale` (default **`true`**) controls only what happens to that error afterward: each element-targeting `App` method (`ClickAsync`, `TypeAsync`, `ClearAsync`, `DoubleClickAsync`, `HoverAsync`, `ClickAtAsync(Locator)`, `ScrollIntoViewAsync`, `ScrollWheelAsync`, `SetFocusAsync`) runs as a short, self-narrating sequence — `EnsureWindowNotMinimized` → `FindAndRunAsync` (resolves the locator and runs the action; on `StaleElementError`, re-runs the **original locator** and retries, polling every `PollIntervalMs` until `ImplicitWaitMs` elapses — the same budget/cadence `FindAsync`/`WaitForAsync` already use) → log → `SettleAsync` (the `ActionDelayMs` pause). Re-resolution uses the original locator, not the stale element's `RuntimeId`: a UIA element that's destroyed and recreated (a re-rendered list, a reopened dialog) gets a *new* `RuntimeId`, so looking up the old one finds nothing in exactly the case staleness signals, and recycled `RuntimeId`s could even resolve to a different element — re-running the locator is how Playwright recovers too. Trade-off: re-querying means "first match again," so an ambiguous locator's retry inherits that ambiguity. Set `ReresolveOnStale = false` to opt out of the auto-retry and have `StaleElementError` always propagate untouched, for a caller that wants to drive its own recovery. `ClickAction`/`TypeAction`/`ClearAction`'s public `ExecuteAsync` signatures are untouched by any of this — the decision lives entirely in `EnsureForeground` (always throws) and `FindAndRunAsync` (retry or don't). `FindByScrollingAsync`'s container becomes a `Locator` too — `FindByScrollingAsync(Locator containerLocator, Locator itemLocator, ...)` replaces Task 2.1.8's `ElementHandle` signature, matching `FindScopedAsync`'s locator-only scope — but it does *not* run through `FindAndRunAsync`. That's built for atomic actions that take milliseconds; wrapped around a multi-second scroll loop (measured live: ~8s for 20 notches), a whole-call retry restarts the search from scratch, throws away its progress, leaves `maxScrolls` no longer bounding the work, and any time window either refuses a late rebuild or never ends against a list that keeps refreshing. Instead the loop handles its own container: a `StaleElementError` carrying the container (from the per-iteration scoped find or the scroll's `EnsureForeground`) re-finds it by locator and carries on, that iteration still counting toward `maxScrolls`, so the call stays bounded by scrolls rather than a clock, and a list that keeps rebuilding ends in the same `ElementNotFoundError` as a missing item; the final full-wait attempt gets one re-find; a stale *item* is still treated as not-yet-realized; `ReresolveOnStale = false` propagates the first stale container; a caller holding a handle passes `Locator.ByRuntimeId(handle.Id)` to pin that one element instead of following a rebuilt one. `GetValueAsync` closes the remaining silent-staleness gap on the read side: it runs through `FindAndRunAsync` too, with both operators' `TryGetValueAsync` raising `StaleElementError` instead of returning `null` (a read has no SendInput fallback to re-detect it — UIA2 needs a `Current.*` probe first, since a gone element's `GetCurrentPattern` throws the same "Unsupported Pattern" as a live one lacking it). The UIA2 fallback path detects staleness via `EnsureForeground`'s `Current.NativeWindowHandle` read, mirroring UIA3's `CurrentNativeWindowHandle`.

**Guiding rule: a `Locator` is the engine's problem, an `ElementHandle` is the caller's.** Staleness gets tricky once scopes are involved — the scope *and* the target can each go stale, a scope can be a `Locator` or a held `ElementHandle`, and single-element find polls while `FindAll` is single-pass, on top of UIA reporting a dead scope as empty/null rather than failing. Rather than designing each combination separately, one rule decides all of them: if the caller handed us a `Locator`, staleness is ours to recover from — re-run the locator; if they handed us an `ElementHandle`, it's theirs — raise `StaleElementError`. Providers only *report* staleness; the resolver/`App` decides. The pattern that keeps this manageable is **handles inside, locators at the edges**: internal code may hold handles freely, so long as every public entry point takes a `Locator` and re-runs it when a handle it holds goes stale — `FindByScrollingAsync` is the model, holding its container as a handle and re-finding it by locator inside its own loop when it goes stale. Any new scoped API, or any open question below, is settled by looking it up against this rule.

**`FindAll` staleness — reported, never returned as empty.** `FindAllAsync`/`FindAllScopedAsync` stay single-pass, matching Playwright's `locator.all()`/`count()`: a genuinely empty result returns immediately, and waiting for a collection to populate belongs in the assertion layer (Task 2.2.6's `ToHaveCountAsync`), not the query — polling on empty would charge every real "nothing matches" the full `ImplicitWaitMs`. What changes is that staleness stops masquerading as emptiness. Playwright's `all()` runs scope and descendant lookup as one atomic in-page script, so its scope can't die mid-query; UIA resolves a scope and searches it in separate cross-process calls, and today every provider's `FindAll` catch turns a scope that died in between into the same empty list as "nothing matches." Single-element scoped find has the same blind spot — it just hid it better, by polling. Fixing it splits into detection, which only the engine can do since a caller has nothing to tell the two apart by, and recovery, which the guiding rule settles:
1. **Providers detect a gone scope.** `FindScopedElementAsync` and `FindScopedElementsAsync` both raise `StaleElementError` when the scope itself is gone. Raw UIA `FindFirst`/`FindAll` on a dead scope don't throw — confirmed against a killed Paint process, UIA2 returns null/an empty collection and UIA3 null/a **null** array (which `Uia3Provider` then dereferenced, an uncaught `NullReferenceException` this also fixes) — so after a null/empty result both UIA providers call the shared `ElementInputHelpers.ThrowIfScopeGone`, which makes the same live read `EnsureForeground` uses (`CurrentNativeWindowHandle`/`Current.NativeWindowHandle`) and raises only if that reports the scope gone. A stale exception thrown by the search itself goes through the same `ThrowIfScopeGone` check, since a *descendant* rebuilt mid-search can throw too and mustn't be blamed on a live scope. Win32's `ValidScopeHandle` raises when the scope hwnd no longer belongs to the session's process (destroyed, possibly recycled for another process's window). Every other failure, including a non-stale failure of the probe itself, still degrades to null/empty; a scope `NativeHandle` from a different provider is still not-found; and stale isn't not-found, so "providers never throw on not-found" holds. Unscoped finds need no check: `TryGetRoot` re-fetches the root by hwnd every call, so a closed window really is empty.
2. **The resolver recovers on the `Locator` paths, per the guiding rule.** Every public scoped method takes a `Locator` scope, so each re-resolves it rather than surfacing the error. Single-element `FindScopedAsync` already re-resolves scope on every poll, so `FindViaFreshScopeAsync` just treats a scope that died between being resolved and searched as a miss for that poll. `FindAllScopedAsync` is single-pass, so its new private `RetryOnStaleScopeAsync` re-runs the resolve-and-search on `StaleElementError` — immediately rather than every `PollIntervalMs`, because the loop ends on its own: the re-run finds the rebuilt scope and searches it, or finds no scope and returns empty, since the scope really is gone. `ImplicitWaitMs` only caps a UI that keeps rebuilding faster than it can be read; past it, `StaleElementError` propagates rather than becoming empty. Both methods' Spatial-scope branches, which no provider can re-resolve per poll, hold the resolved scope and rerun the whole Spatial resolution through the same `RetryOnStaleScopeAsync` when it goes stale — before this, the single-element one polled its dead handle until `ElementNotFoundError`. This matches Playwright's split of locators recovering on their own and handles not — Playwright gets it from atomic queries, AutoMancer from a re-run.
3. **Held handles propagate.** The internal held-`ElementHandle` `FindScopedAsync`/`FindAllScopedAsync` let `StaleElementError` through, like Playwright's `ElementHandle` throwing on a detached node — a dead handle can't be re-resolved. Their only callers sit under a locator-level retry: the Spatial branches above, and `FindByScrollingAsync`'s `ResolveFullyIntoViewAsync`, whose stale container now reaches the scroll loop's container re-find immediately instead of after a full `ImplicitWaitMs` poll ending in `ElementNotFoundError`.

`ReresolveOnStale = false` opts out of all of it, propagating on the first stale, via a new `ElementProviderOptions.ReresolveOnStale` copied from `AppOptions` alongside `ImplicitWaitMs`/`PollIntervalMs`. Trade-off, as with actions: re-resolving is "first match again," so an ambiguous scope locator's retry can land on a different element.

**What counts as stale — one shared definition.** Matching on `UIA_E_ELEMENTNOTAVAILABLE` alone misses two real cases, found by holding an element while its UI was torn down across Paint, Notepad, and Calculator (identical under UIA2 and UIA3):

| Teardown | What the held element does |
|---|---|
| App process exits | E_UNEXPECTED (`0x8000FFFF`) for ~30ms, then `UIA_E_ELEMENTNOTAVAILABLE` |
| Win32 common dialog closes; XAML list item removed | `UIA_E_ELEMENTNOTAVAILABLE` |
| **XAML `ContentDialog` closes** (Notepad Go to line, Paint Resize) | **Never throws** — stays readable, detached: blank `Name`, empty bounds, no raw-view parent |
| XAML menu closes; UWP page navigates away (Calculator) | Still attached and readable, just hidden — not stale |

**Not yet measured: WPF, WinForms, and Electron/Chromium teardown.** The table covers native Win32, WinUI/XAML, and UWP only. Candidate apps for filling it in, run one at a time like every other live-UI suite:
- **WPF — PowerShell ISE** (in-box, an optional feature on by default): process exit; Find or Tools → Options closing (modal window); a script tab closing (item removed); a context menu closing (hidden, not stale). Worth watching for: a virtualized list recycling its item containers, so a held row stays attached but shows another row's data; and menus/popups being separate hwnds that may tear down like Win32 rather than XAML.
- **Chromium — Edge** (in-box), launched with its own `--user-data-dir` like the VS Code sample: a DOM modal closing (e.g. "Clear browsing data" on `edge://settings`); a tab closing, which is native Views UI rather than web content, so both kinds are covered; process exit. Chromium updates its accessibility tree asynchronously, so a removed node may read as detached before it starts throwing, like the XAML `ContentDialog` row.
- **Electron — VS Code**, via the existing `samples/ConsumerVsCodeTests` harness: the Command Palette closing (removed or only hidden?); a notification toast dismissed; an editor tab closing; the "Save changes?" prompt, which Electron shows as a native dialog.
- **WinForms — a small in-repo fixture app** (nothing usable ships in-box): buttons opening a modal and a modeless dialog, a ListBox and ListView with Add/Remove, a context menu, and an Exit button, so each table row can be produced on purpose; KeePass 2 portable is a real-app cross-check. WinForms dialogs are real Win32 windows, so they likely match the Win32 dialog row, but that needs measuring rather than assuming. A matching WPF fixture would make the WPF rows deterministic too.

For Electron the bigger risk was false positives rather than teardown shape — `IsDetached` runs on every action and treats "no raw-view parent" as gone, so a Chromium node reported without a parent would fail every action on it — and `samples/ConsumerVsCodeTests` rules that out: its end-to-end VS Code run (launch, first-run wizard, editor, save, integrated terminal) passes with the check on every action.

So every detection site — `EnsureForeground`, both operators' `TryGetValueAsync`, `ScrollAction`, `SetFocusAction`, both UIA providers' `ThrowIfScopeGone` calls — goes through `ElementInputHelpers`: `IsStaleFailure(ex, element)` for a failed live read (`UIA_E_ELEMENTNOTAVAILABLE`/`ElementNotAvailableException`, or E_UNEXPECTED once the owning process has exited, using an internal `ElementHandle.ProcessId` captured at wrap time since a dead element can't be asked; with the process alive E_UNEXPECTED stays an ordinary failure, preserving the VLC tolerance), and `IsDetached(element)` for a successful read of an element with no raw-view parent (every attached element has at least the desktop). Before this, UIA3 swallowed the post-exit E_UNEXPECTED and clicked at the dead element's cached `BoundingRect`, UIA2 leaked it as a raw `COMException`, and an element from a closed XAML dialog sailed through every check to a click wherever the dialog used to be. Hidden-but-attached elements (the last row) are deliberately *not* stale — whether acting on one is an error is an actionability question, not a staleness one.

Out of scope: the `AutoMancerXPath`/`AutoMancerPath` walkers swallow per-node failures and can return a short list (a separate gap); a single match that dies while being wrapped is dropped, which is correct, since it's gone.

**Creates:**
- `src/AutoMancer.Engine/Errors/StaleElementError.cs` — carries the stale `ElementHandle`, mirroring `ElementNotInteractableError`'s shape; `inner` is optional, since Win32's ownership check, the detached check, and the scope liveness probe have no exception to chain
- `src/AutoMancer.Engine/AppOptions.cs` — add `ReresolveOnStale` bool, default `true`
- `src/AutoMancer.Engine/Actions/ElementInputHelpers.cs` — shared `IsStaleFailure`/`IsDetached`/`Stale`/`ThrowIfScopeGone`, the one definition of stale every detection site uses (see above); `EnsureForeground` throws `StaleElementError` through them, while any other failure of its live read keeps the original swallow-and-continue behavior unchanged
- `src/AutoMancer.Engine/Core/ElementHandle.cs` — internal `ProcessId`, set by both UIA providers' `Wrap`
- `src/AutoMancer.Engine/App.cs` — private `FindAndRunAsync` (find + locator-based stale retry) and `SettleAsync`, called in sequence by every element-targeting action — applied uniformly (matching Playwright's single shared actionability/retry pipeline conceptually) rather than to a curated subset, since `EnsureForeground` throws unconditionally for all of them regardless; `FindByScrollingAsync` switches to a `Locator` container, which its scroll loop re-finds itself when it goes stale (each re-find counting toward `maxScrolls`)
- `src/AutoMancer.Engine/Providers/Uia3Provider.cs` / `Uia2Provider.cs` / `Win32Provider.cs` — `FindScopedElementAsync`/`FindScopedElementsAsync` raise `StaleElementError` for a gone scope instead of returning null/empty (UIA: private helpers take the scope handle directly and the shared `ElementInputHelpers.ThrowIfScopeGone` liveness probe confirms it; Win32: `ValidScopeHandle` throws). `Uia3Provider.Automation` becomes `internal`, so `IsDetached` reuses the one `IUIAutomation` instance for its raw-view parent lookup; every `Uia2Provider` catch also covers `COMException` and `ElementNotAvailableException` (which derives from `SystemException`, not `InvalidOperationException`), so post-exit E_UNEXPECTED can't escape a provider
- `src/AutoMancer.Engine/Core/ElementResolver.cs` — `FindViaFreshScopeAsync` treats a stale scope as a miss for that poll; private `RetryOnStaleScopeAsync` (immediate, `ImplicitWaitMs`-capped, honoring `ReresolveOnStale`) wraps `FindAllScopedAsync`'s `Locator`-scope path and both Spatial-scope branches
- `src/AutoMancer.Engine/Core/ElementProviderOptions.cs` — add `ReresolveOnStale`, set by `App` from `AppOptions`
- `src/AutoMancer.Engine/Operators/Uia3Operator.cs` / `Uia2Operator.cs` — `TryGetValueAsync` raises `StaleElementError` through the shared checks (detached included) instead of returning `null`; UIA2's read also tolerates a non-stale `COMException` like UIA3's
- `src/AutoMancer.Engine/Actions/ScrollAction.cs` / `SetFocusAction.cs` — classify failures through `IsStaleFailure`; `ScrollAction` also refuses a detached element, which would otherwise accept `ScrollIntoView` as a silent no-op (`SetFocusAction` gets that via `EnsureForeground`)
- `src/AutoMancer.Engine/Core/IElementProvider.cs` — `FindScopedElementAsync`/`FindScopedElementsAsync` contracts document `StaleElementError` for a gone scope as distinct from not-found

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

**Future extension:** A staleness *wait* — the inverse of this task's re-resolve — would let a caller wait until an element/dialog detaches from the tree, for close-and-continue flows, instead of polling `FindAsync` for absence. Same `StaleElementError` detection this task already adds, used to confirm absence rather than to recover from it.

---

### Task 2.2.3: Tests for stale-element and scoped-find resilience

**What:** A resilience test simulating a stale COM failure via a mock provider, verifying that with `ReresolveOnStale = true` (the default) the action re-runs its original locator and succeeds against the freshly found element; a second test verifying that with `ReresolveOnStale = false` the same setup lets `StaleElementError` propagate to the caller untouched, rather than being silently retried; plus a control test proving a *non*-stale `COMException` (the VLC split-button `MenuItem` case) is still silently swallowed exactly as before, regardless of `ReresolveOnStale` — `EnsureForeground` only promotes failures the shared stale definition recognizes (`UIA_E_ELEMENTNOTAVAILABLE`, E_UNEXPECTED once the owning process has exited, or a detached element), so every other failure path must stay provably untouched.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Actions/StaleElementResilienceTests.cs` — mock `IElementOperator`/`IElementProvider` throws the stale-element `COMException` once; with `ReresolveOnStale = true`, the original locator is re-run (returning a fresh handle with a *different* id, as real UIA would for a recreated element) and the action succeeds; with `ReresolveOnStale = false`, `StaleElementError` propagates out of `ClickAsync`/`TypeAsync`/`ClearAsync` for the caller to catch; a non-stale `COMException` from the same mock is swallowed and the action still succeeds via `SendInput`, either way; and, end to end through the real `ScrollWheelAction`, a `FindByScrollingAsync` container that goes stale mid-scroll — including only after `ImplicitWaitMs` has already elapsed — is re-found and the search continues
- `tests/AutoMancer.Engine.Tests/Core/FindByScrollingTests.cs` — the scroll loop's own container handling: a container that goes stale on a scroll is re-found and the search continues against it (no restart); one that goes stale every iteration ends after `maxScrolls` with `ElementNotFoundError`, one re-find per iteration; one that goes stale during the final attempt gets one re-find; `ReresolveOnStale = false` propagates the first stale container
- `tests/AutoMancer.Engine.Tests/Resolver/ElementResolverTests.cs` — scoped-find staleness: a held-`ElementHandle` scope's `StaleElementError` propagates out of `FindAllScopedAsync`; a `Locator` scope that goes stale mid-`FindAllScopedAsync` is re-resolved and returns the rebuilt scope's matches with no `PollIntervalMs` delay; one that no longer resolves returns empty; one that stays stale past `ImplicitWaitMs` propagates; `ReresolveOnStale = false` propagates on the first stale; a control proves a live scope with no matches makes exactly one pass; single-element `FindScopedAsync` treats a scope that went stale between resolve and search as a miss and finds the rebuilt scope next poll (or propagates with `ReresolveOnStale = false`); and a Spatial scope that goes stale reruns the Spatial resolution for both `FindScopedAsync` and `FindAllScopedAsync`
- `tests/AutoMancer.Engine.Tests/Providers/ScopeResolutionTests.cs` — Win32's foreign-process scope hwnd raises `StaleElementError` from both `FindScopedElementAsync` and `FindScopedElementsAsync`
- `tests/AutoMancer.Engine.Tests/Integration/Paint/StaleElementRegressionTests.cs` — `uia2`/`uia3` theory: a live scope with no matches returns null/empty, then after killing Paint both `FindScopedElementAsync` and `FindScopedElementsAsync` raise `StaleElementError` carrying the scope; a closed Resize dialog's held Cancel button raises `StaleElementError` from `EnsureForeground`, `TryGetValueAsync`, and as a `FindScopedElementsAsync` scope, while its native `TryClickAsync` and the held Horizontal box's `TrySetValueAsync` decline (a detached element exposes no patterns), so the native path falls through to `EnsureForeground` instead of silently succeeding; kills wait for the window owner's exit rather than a fixed delay, so they land in the E_UNEXPECTED window deterministically instead of skipping past it most of the time
- `tests/AutoMancer.Engine.Tests/Actions/ElementInputHelpersTests.cs` — `IsStaleFailure` classification (stale HRESULT, `ElementNotAvailableException`, E_UNEXPECTED with the process exited vs. alive vs. unknown, an unrelated COM failure) and `EnsureForeground` throwing on post-exit E_UNEXPECTED while keeping log-and-continue for a live process

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration&(FullyQualifiedName~StaleElementResilienceTests|FullyQualifiedName~ElementResolverTests|FullyQualifiedName~ScopeResolutionTests|FullyQualifiedName~ElementInputHelpersTests|FullyQualifiedName~FindByScrollingTests)"
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~StaleElementRegressionTests"
```

**Done when:** The auto-retry, opt-out-propagates, and non-stale-still-swallowed cases pass, along with the scoped-find staleness, stale-classification, and live Paint process-kill and closed-dialog cases.

---

### Task 2.2.4: Live actionability checks, and `app.Force` to opt out per call

**What:** `ElementInputHelpers.EnsureInteractable` reads `IsEnabled`/`IsOffscreen` as cached on the `ElementHandle` at find time, and only runs on the synthesized-input path (`GetCenter`, `TypeAction`, `ClearAction`) — the native Invoke/SetValue path runs no check at all. Holding Notepad's "New tab" File-menu item and then closing the menu showed the cost: cached `IsOffscreen=false` with its old bounds, live `IsOffscreen=true` with empty bounds and no clickable point — yet a native Invoke on it still opened a new tab, which no user can do with the menu closed, and on the synthesized path the cached check would have passed and clicked wherever the item used to be. (A locator can't reach that item while the menu is closed, since the popup sits outside the session's root window, so this bites held handles.) The cached check is wrong in the other direction too: an element found offscreen and since scrolled into view still fails it. Selenium (`ElementNotInteractableException`) and Playwright (auto-waited actionability) both enforce this at the base, so this task keeps the existing policy and makes it live:
1. **Live state.** `EnsureInteractable` reads `IsEnabled`, `IsOffscreen`, and the bounding rectangle live; empty live bounds count as not visible. A synthesized click aims at the center of the *live* bounds, not the cached ones.
2. **Both paths.** It runs before the native Invoke/SetValue attempt as well as before synthesized input, for every input action: `ClickAsync`, `RightClickAsync`, `DoubleClickAsync`, `HoverAsync`, `TypeAsync`, `ClearAsync`, `ClickAtAsync(Locator)`, `ScrollWheelAsync`. `ScrollIntoViewAsync` is exempt — making an offscreen element visible is its whole job — as are `SetFocusAsync` and `GetValueAsync`.
3. **Auto-wait.** `FindAndRunAsync` treats `ElementNotInteractableError` like `StaleElementError`: re-find and retry every `PollIntervalMs` until `ImplicitWaitMs`, then let it propagate — Playwright's "wait until actionable", so a button that's still enabling or a dialog still animating in doesn't fail a test outright.
4. **`app.Force` — a per-call opt-out.** `public App Force` returns a cached view of the same `App`: same session, providers, and options, created once. It's built through an internal constructor with `ownsSession: false`, so disposing it never closes the app under test, and its own `ElementResolver` (over the parent's providers) stamps an internal `ElementHandle.Force` alongside `Logger`/`ForegroundActivationTimeoutMs`. A forced handle skips step 1's enabled/visible gate and step 3's wait — the documented escape hatch for invoking a hidden menu command on purpose, or for a framework whose `IsOffscreen` can't be trusted: `await app.Force.ClickAsync(Locator.ByName("New tab"))`. Force never skips stale detection (a forced action on a gone element still raises `StaleElementError` and still re-resolves under `ReresolveOnStale`), and a forced *synthesized* input still needs non-empty live bounds — it skips the waiting, never the need for a real point, so it can't click at (0,0) or at an element's last-known position.

Force is opt-in per call only — there's deliberately no `AppOptions.Force` run-wide default yet. A per-call parameter was rejected: a `bool force` before `ct` on every action would silently rebind positional `ct` (the same trap Task 2.1.7 hit), and one after `ct` breaks the CancellationToken-last convention (CA1068). Behavior change: acting on a hidden or disabled element through a held handle used to succeed via native Invoke and now waits, then throws `ElementNotInteractableError` — `app.Force` is the documented way back.

**Creates:**
- `src/AutoMancer.Engine/Actions/ElementInputHelpers.cs` — `EnsureInteractable` reads live state and honors `ElementHandle.Force`; `GetCenter` uses live bounds
- `src/AutoMancer.Engine/Actions/ClickAction.cs` / `DoubleClickAction.cs` / `HoverAction.cs` / `TypeAction.cs` / `ClearAction.cs` / `ScrollWheelAction.cs` — call `EnsureInteractable` before the native-pattern attempt, not only before synthesized input
- `src/AutoMancer.Engine/Core/ElementHandle.cs` — internal `Force`
- `src/AutoMancer.Engine/Core/ElementResolver.cs` — stamps `Force` on returned handles when constructed for a forced view
- `src/AutoMancer.Engine/App.cs` — `Force` property and its internal non-owning constructor; `DisposeAsync` a no-op when the session isn't owned; `FindAndRunAsync` retries `ElementNotInteractableError` unless forced

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

**Future extensions:**
- `AppOptions.Force` — a run-wide default for a whole session against a framework whose `IsOffscreen` is unreliable; `app.Force` stays the per-call form.
- `ElementHandle.WithForce()` — force a handle already held from a non-forced `App` when no locator can re-find it (today: `app.Force.ClickAsync(Locator.ByRuntimeId(handle.Id))`, which works whenever the element is still findable).
- Folding Task 2.2.7's occlusion check into the base gate as Playwright's "receives events" step, if it proves cheap and reliable enough — it stays opt-in until then.

---

### Task 2.2.5: Tests for live actionability and `app.Force`

**What:** Unit tests against mocked live reads, plus a live Notepad regression for the hidden-menu-item case that motivated Task 2.2.4.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Actions/ActionabilityTests.cs` — live state overrides cached state in both directions (cached visible/live hidden is rejected; cached offscreen/live visible is allowed); a hidden or disabled element is rejected *before* the native Invoke runs (the mock's Invoke is never called); empty live bounds are rejected even with `IsOffscreen=false`; a synthesized click aims at the live center, not the cached one
- `tests/AutoMancer.Engine.Tests/Resolver/` or `AppTests.cs` — `FindAndRunAsync` polls a not-yet-interactable element and succeeds once it becomes interactable, and throws `ElementNotInteractableError` after `ImplicitWaitMs`; through `app.Force` the same element is acted on immediately with no wait; `app.Force` on a stale element still throws `StaleElementError`; a forced synthesized click with empty bounds still throws; disposing `app.Force` leaves the session (and process) alive; `app.Force` returns the same cached instance each time
- `tests/AutoMancer.Engine.Tests/Integration/Notepad/ActionabilityIntegrationTests.cs` — open Notepad's File menu and hold its "New tab" item twice, once found through `app` and once through `app.Force`, then close the menu: `ClickAction.ExecuteAsync` on the default handle throws `ElementNotInteractableError` and opens no tab, while on the forced handle it opens one — tab count asserted both ways. (Both handles are found while the menu is open because no locator, `ByRuntimeId` included, can reach the item once it's closed.)

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration&(FullyQualifiedName~Actionability|FullyQualifiedName~Force)"
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~Actionability"
```

**Done when:** All cases pass, including the live Notepad hidden-menu regression.

---

### Task 2.2.6: Extend `LocatorExpect` with `WaitConditions`-backed assertions

**What:** `AutoMancer.Testing`'s `LocatorExpect` gets `ToBeEnabledAsync()` (built on Task 2.2.1's `WaitConditions.IsEnabled()`), `ToContainTextAsync(string substring)` (built on `WaitConditions.NameContains`), and `ToHaveCountAsync(int expected)` — Playwright's `toHaveCount`, polling single-pass `FindAllAsync` via `Poll.UntilAsync` (like `ToHaveValueAsync`) and reporting the last count seen on timeout; it's the wait half `FindAllAsync` itself deliberately doesn't do (see Task 2.2.2), and the `Expect()` form of Task 2.2.1's `NumberOfElementsToBe` future extension — so `Expect()` gets the same conditions `WaitForAsync` does, instead of only the four hand-written checks Stage 1.9 shipped (`ToHaveNameAsync`/`ToBeVisibleAsync`/`ToHaveTextAsync`/`ToHaveValueAsync`). Each new assertion follows the existing `WaitOrFailAsync` pattern already used by `ToHaveNameAsync` et al. — no new failure-message plumbing needed.

**Creates:**
- `src/AutoMancer.Testing/LocatorExpect.cs` — add `ToBeEnabledAsync(CancellationToken ct = default)`, `ToContainTextAsync(string substring, CancellationToken ct = default)`, `ToHaveCountAsync(int expected, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)`
- `tests/AutoMancer.Engine.Tests/TestingAdapter/LocatorExpectTests.cs` — extend with cases mirroring the existing `ToHaveNameAsync` tests (matches immediately, retries until match, times out with a descriptive message)

- [ ] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "FullyQualifiedName~LocatorExpectTests"
```

**Done when:** `WaitForAsync` and `Expect()` share a starter vocabulary instead of `WaitForAsync` alone having one; `ToHaveCountAsync` waits for a collection to reach an expected size, the wait half single-pass `FindAllAsync` deliberately leaves out.

---

### Task 2.2.7: `App.IsUnobscuredAsync` — occlusion check

**What:** An opt-in occlusion check, not a gate on any existing action — `App.IsUnobscuredAsync(Locator locator, CancellationToken ct = default)`. Cross-window half works for every provider via a new `NativeMethods.WindowFromPoint` compared against the resolved element's own top-level HWND; same-window/sibling-overlay half only for `uia3`-resolved elements, via a new `Uia3Provider` internal helper that walks ancestors from `IUIAutomation.ElementFromPoint` (bounded by the existing `MaxTreeDepth`, compared via `CompareElements`) — a `uia2`/`win32`-resolved element degrades to the cross-window check alone rather than throwing. This is a different shape than the cached-state predicates of Tasks 2.2.1 and 2.2.6: it needs a live COM/Win32 call at check time, not a read of cached `ElementHandle` state — like Task 2.2.4's live actionability checks — and it belongs in this stage anyway as the same actionability-primitive family Selenium's `ExpectedConditions`/Playwright's actionability checks live in.

**Creates:**
- `src/AutoMancer.Engine/Providers/NativeMethods.cs` — add `WindowFromPoint` P/Invoke declaration
- `src/AutoMancer.Engine/Providers/Uia3Provider.cs` — internal ancestor-walk helper from `IUIAutomation.ElementFromPoint`
- `src/AutoMancer.Engine/App.cs` — `IsUnobscuredAsync(Locator locator, CancellationToken ct = default)`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** `app.IsUnobscuredAsync(locator)` returns `false` when another window or an in-app overlay is covering the element's hit point, and `true` once it's clear.

**Future extensions, once this lands:**
- `WaitConditions.ToBeClickable()` — a composite of `IsVisible()` + `IsEnabled()` (Task 2.2.1) + this task's live occlusion check; Selenium's single most-used `ExpectedCondition`. Needs this task first since it can't be a pure cached-state predicate like the rest of `WaitConditions`.
- `App.IsForegroundAsync(locator)` — a focus/foreground-window check via `GetForegroundWindow` comparison, same live-check shape as this task. Arguably higher real-world value than occlusion alone, since input silently landing on the wrong window is one of the most common WinAppDriver flake sources.
- A hung-window ("Not Responding") check via `IsHungAppWindow` or a `SendMessageTimeout` probe — same actionability-primitive family, catches slow-app flake that today has no engine-level answer.

---

### Task 2.2.8: Integration test for occlusion detection

**What:** Integration test against live Notepad — an on-screen button reports unobscured; the same button reports obscured once the Save-changes `ContentDialog` covers it.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Integration/OcclusionIntegrationTests.cs`

- [ ] **Run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~Occlusion"
```

**Done when:** Both cases pass against live Notepad. Stage 2.2 complete.

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

**What:** Walks an `ElementSnapshot` tree from the existing `App.SnapshotAsync`, flags nodes whose `ControlType` is interactive (`Button`, `Edit`, `CheckBox`, …) with a null or empty `Name`. Returns findings with a tree-path breadcrumb — likely buildable by walking the tree the same way `XPathEvaluator`'s index-to-element mapping already does, though whether that mapping is actually reusable for a breadcrumb string (rather than just an index lookup) isn't confirmed until tried.

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

**What:** `automancer audit <session>` prints findings as a table, matching `tree`'s existing output style — likely able to reuse the CLI's existing table-printing helper rather than needing a new formatter, though that depends on how reusable that helper actually turns out to be.

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

> **Two different capabilities share this stage because they're expected to share screenshot/pixel-diff plumbing, not because they solve the same problem.** Tasks 23–26 are the actual Visual Provider — a locator fallback for apps with no accessibility tree at all. Tasks 27–28 (`WaitForIdleAsync`, visual regression) are general-purpose and useful against any app, UIA-accessible or not; they're grouped here on the assumption they can reuse the capture/compare code Tasks 23–26 build, though that's only confirmed once 2.6.3 actually lands. See roadmap-spec.md's Stage 2.6 for the full split.

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

**What:** Diffs consecutive `ScreenshotAsync()` captures at a short interval until two frames match within a pixel-difference threshold, or a timeout is hit. Replaces the ad-hoc `Task.Delay(300) // flyout animation` waits already scattered through the Phase 1 integration test suite with a real settledness check — likely able to reuse the pixel-compare plumbing Task 2.6.3 builds for template matching rather than needing a separate diff implementation, assuming that plumbing ends up general enough once 2.6.3 actually lands.

**Creates:**
- `src/AutoMancer.Engine/App.cs` — `WaitForIdleAsync(TimeSpan? pollInterval = null, TimeSpan? timeout = null, CancellationToken ct = default)`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** `app.WaitForIdleAsync()` resolves once consecutive screenshots stop changing. As a follow-up cleanup (not required for this task's own "done"), the scattered `Task.Delay(300)` animation waits in the existing integration suite are good candidates to replace with this.

---

### Task 2.6.6: Visual regression snapshot assertion

**What:** Captures the current window/element region and pixel-diffs it against a stored baseline PNG, failing past a configurable difference threshold. Likely reuses the screenshot/pixel-compare plumbing built for template matching in Task 2.6.3, if that plumbing turns out general enough for a full-image diff rather than just a template match.

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

**Future extension:** A `WaitConditions.ToggleStateEquals(ToggleState)` predicate, for polling a checkbox/tri-state control via `WaitForAsync`/`Expect()` instead of a one-shot read. Blocked on this task landing `TogglePattern` support first, and on `ElementHandle` (or a follow-on read) actually carrying `ToggleState` for a sync predicate closure to read.

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

**Future extension:** A `WaitConditions.IsSelected()` predicate, for polling a list/combo item's selection state via `WaitForAsync`/`Expect()` instead of calling this task's one-shot `IsSelectedAsync` in a hand-rolled loop. Same blocker as `ToggleStateEquals` above — needs `SelectionItemPattern` support from this task first, plus a sync-readable field to close the predicate over.

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

- [x] Task 2.1.6 — spatial and property locators find elements the fixed strategy set can't reach
- [x] Task 2.1.9 — scoped find disambiguates identically-matched elements, and virtualized-list find locates unrealized items
- [ ] Task 2.2.6 — `Expect()` and `WaitForAsync` share a wait-condition vocabulary
- [ ] Task 2.3.4 — native context menu fallback works when UIA can't see the popup
- [ ] Task 2.4.2 — resource watch returns a plausible sample series against a live app
- [ ] Task 2.5.3 — accessibility audit reports a deliberately-unnamed control
- [ ] Task 2.6.4 — visual provider finds an element by rendered text with no UIA tree
- [ ] Task 2.6.6 — visual regression assertion fails on a drifted baseline
- [ ] Task 2.7.2–2.7.3 — DPI and Windows 10/11 compat confirmed
- [ ] Task 2.7.4 — all four example projects build and run
- [ ] Task 2.8.6 — toggle/select/grid/clipboard all verified against a live app
- [ ] Task 2.8.8 — a slider/progress bar's value round-trips through `RangeValueAction`
