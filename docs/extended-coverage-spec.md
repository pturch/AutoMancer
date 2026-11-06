# AutoMancer Extended Coverage Implementation Plan

> **For agentic workers:** Steps use checkbox (`- [ ]`) syntax for tracking progress through this plan.
>
> **Version control stays manual.** Whoever picks up a task commits deliberately — nothing in this workflow auto-commits or auto-pushes. Bash blocks in this document are implementation reference — execute the build/test lines only, never the git lines.

**Phase:** 2 of 3 — this plan deepens the Phase 1 engine surface (richer locators, wait/resilience primitives, native fallbacks, idle-wait and visual-regression, and UIA pattern coverage). No Phase 1 source files are removed or have their existing signatures changed; every task here is additive. **Completing this phase is the v1 milestone** — a hardened, complete C# engine, CLI, and test adapter, shippable on its own. Phase 3 (daemon + SDKs) is deliberately deferred, long-running future work picked up later, not the next phase in the queue. See [roadmap-spec.md](./roadmap-spec.md) for how this fits into the overall three-phase plan. **Task numbers here are the same numbers as roadmap-spec.md's batches** — Task `2.1.7` below is batch `2.1.7` there, not a separately-tracked ID. Each stage's tasks restart at `.1`, independent of every other stage's count, so a stage never needs renumbering because a different stage gained or lost a task.

**Goal:** Take the Phase 1 engine — UIA3/UIA2/Win32 providers, `ElementResolver`, the full interaction/wait surface, and the `AutoMancer.Testing` adapter — and extend it with the coverage a real automation suite eventually needs but a proof of concept doesn't: locators for unlabeled/custom-property/scoped/virtualized-list elements, a canned wait vocabulary, stale-element resilience, native context menus, a resource-leak diagnostic, an accessibility audit, the general-purpose idle-wait and visual-regression capabilities, DPI/OS hardening, and the UIA pattern actions (`Toggle`/`ExpandCollapse`/`Selection`/`Grid`/`RangeValue`/clipboard) Phase 1 never reached — which Phase 3's daemon and SDKs also happen to assume exist, whenever that phase is picked up.

**Architecture:** Every task in this plan touches only `AutoMancer.Engine` (and, for Stage 2.2's `Expect()` extension, `AutoMancer.Testing`) — no `AutoMancer.Daemon` project exists yet, so nothing here is reachable over HTTP. `ElementHandle` stays opaque throughout (only `Id`/`NativeHandle` public) — Phase 3's daemon element registry depends on that contract holding. Where a new capability wraps a UIA pattern the engine doesn't already touch (`TogglePattern`, `GridPattern`, `RangeValuePattern`, …), it follows the existing `IElementOperator`-optional, no-op-when-unsupported shape `ScrollAction`/`SetFocusAction` already established in Phase 1, rather than inventing a new dispatch convention. "Unsupported" means the element lacks the pattern, or Win32 has no equivalent — never "not a UIA3 handle": every UIA action covers both UIA3 and UIA2 handles, and Win32 hwnds too where user32 has a real counterpart (`ScrollAction`/`SetFocusAction` originally returned early for anything not UIA3, so a UIA2-resolved handle silently didn't scroll or focus; fixed alongside Task 2.2.5, with `SetFocusAction` also covering Win32 hwnds via `AttachThreadInput` + `SetFocus`. Scrolling into view stays a Win32 no-op: user32 has no general "scroll this child window into view", and list rows — what you'd scroll to — aren't windows the Win32 provider can find).

**Prior art:** `WaitConditions` (Stage 2.2) mirrors Selenium's `ExpectedConditions` — a starter vocabulary instead of every caller hand-rolling predicates. Stage 2.6's `WaitForIdleAsync`/visual-regression batches are general-purpose and apply to any app; the OCR/template-matching Visual Provider is unscheduled Future Work.

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
│   ├── Popup.cs                      the public popup handle: an App view rooted at the popup's surface (IsOpenAsync, CloseAsync, WaitUntilClosedAsync)
│   └── IElementProvider.cs           FindScopedElementAsync/FindScopedElementsAsync take a required ElementHandle scope
├── Providers/
│   ├── Uia3Provider.cs, Uia2Provider.cs, Win32Provider.cs
│   │                                 implement the scoped find methods, using scope as the search root
│   └── NativeMethods.cs              GetGuiResources, GetMenu family (context menu fallback)
├── Actions/
│   ├── PopupAction.cs                opens a popup from its trigger and detects it (context menus, dropdowns, tooltips, flyouts; native HMENU through Win32Provider)
│   ├── ClipboardAction.cs            GetTextAsync/SetTextAsync, not element-scoped
│   ├── ToggleAction.cs               TogglePattern.Toggle()
│   ├── ExpandCollapseAction.cs       ExpandCollapsePattern.Expand()/Collapse()
│   ├── SelectionAction.cs            SelectionItemPattern/SelectionPattern
│   ├── GridAction.cs                 GridPattern/TablePattern row/column/cell access
│   └── RangeValueAction.cs           RangeValuePattern get/set
├── Diagnostics/
│   ├── ResourceMonitor.cs, ResourceSample.cs, ResourceSummary.cs   static capture/measure for an app or the engine host; GDI/USER/handle/thread/memory reading and before/after deltas
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
└── xunit-test-adapter/

tests/AutoMancer.Engine.Tests/
├── Core/SpatialMatcherTests.cs, WaitConditionsTests.cs, ScopedFindTests.cs, FindByScrollingTests.cs, ...
├── Actions/ClipboardActionTests.cs, ToggleActionTests.cs, RangeValueActionTests.cs, ...
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

- [x] **Write tests, run (expect FAIL), implement, run (expect PASS)**

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

- [x] **Implement and build**

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

- [x] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration&(FullyQualifiedName~StaleElementResilienceTests|FullyQualifiedName~ElementResolverTests|FullyQualifiedName~ScopeResolutionTests|FullyQualifiedName~ElementInputHelpersTests|FullyQualifiedName~FindByScrollingTests)"
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~StaleElementRegressionTests"
```

**Done when:** The auto-retry, opt-out-propagates, and non-stale-still-swallowed cases pass, along with the scoped-find staleness, stale-classification, and live Paint process-kill and closed-dialog cases.

---

### Task 2.2.4: Actionability gate on every input path

**What:** `ElementInputHelpers.EnsureInteractable` only ran on the synthesized-input path (`GetCenter`, `TypeAction`, `ClearAction`) — the native Invoke/SetValue path ran no check at all, so a hidden or disabled element exposing InvokePattern/ValuePattern was acted on anyway (an offscreen row invoked, a field in a collapsed panel cleared), doing something no user could. Selenium (`ElementNotInteractableException`) and Playwright (auto-waited actionability) both enforce this at the base, so this task keeps the existing policy and applies it everywhere:
1. **Both paths.** `ClickAction`, `TypeAction`, and `ClearAction` call `EnsureInteractable` before the native-pattern attempt, not only before synthesized input; `DoubleClickAction`/`HoverAction`/`ScrollWheelAction` have no native path and stay gated through `GetCenter`. That covers every input action: `ClickAsync`, `RightClickAsync`, `DoubleClickAsync`, `HoverAsync`, `TypeAsync`, `ClearAsync`, `ClickAtAsync(Locator)`, `ScrollWheelAsync`. `ScrollIntoViewAsync` is exempt — making an offscreen element visible is its whole job — as are `SetFocusAsync` and `GetValueAsync`.
2. **One definition of visible and enabled.** The gate judges an element with the same rules as Task 2.2.1's `WaitConditions.IsVisible()`/`IsEnabled()`, now defined once as internal `WaitConditions.Visible(bool? isOffscreen, Rect bounds)` (not reported offscreen, and non-empty bounds) and `WaitConditions.Enabled(bool? isEnabled)` (not reported disabled — unknown counts as enabled, so a provider that can't tell never blocks a wait or an action). Previously they disagreed: `IsVisible()` ignored bounds, `IsEnabled()` treated unknown as disabled while actions treated it as enabled, and `Expect`'s visibility checks ignored `IsOffscreen` (Task 2.2.6 moves them onto `IsVisible()`). The gate's error names which check failed (disabled, offscreen, or empty bounds).
3. **Auto-wait.** `FindAndRunAsync` treats `ElementNotInteractableError` like `StaleElementError`: re-find and retry every `PollIntervalMs` until `ImplicitWaitMs`, then let it propagate — Playwright's "wait until actionable", so a button that's still enabling or a dialog still animating in doesn't fail a test outright.

**Two independent live checks: usable, and where to aim.** Whether an element is usable and where pointer input should hit are separate questions with separate consumers — every input action needs the first, only synthesized pointer input needs the second — so each is its own self-contained helper taking just the element. `EnsureInteractable(element)` reads `IsEnabled`/`IsOffscreen`/bounds live and judges them with the shared rules; `GetCenter(element)` reads bounds live and returns their center, validating its own read (empty bounds throw `ElementNotInteractableError`, "no point to aim at") rather than relying on the check having run — so it never aims at (0,0) or a stale position. Each read is one batched cross-process call through the private `ReadState` — a UIA3 `IUIAutomationCacheRequest` via `BuildUpdatedCache`, or UIA2's `CacheRequest` via `GetUpdatedCache` — that also detects staleness (a gone element fails it); a read that fails without the element being gone (the VLC split-button case `EnsureForeground` already tolerated) logs and falls back to the find-time values, as do non-UIA handles. `EnsureForeground(element)` is unchanged, with its own window-handle read (also a stale check) and detached-element check. A synthesized pointer action costs four UIA calls (check, foreground's two, aim); a native click costs one plus the Invoke. Keeping the two apart is also what later tasks build on: Task 2.2.5's `app.Force` skips `EnsureInteractable` while `GetCenter` still demands a real point; keyboard input's equivalent of the aim step is 2.2.5's `EnsureKeyboardFocus`; and Task 2.2.7's occlusion check hit-tests the same point `GetCenter` returns. Live state matters because `ElementHandle`'s `IsEnabled`/`IsOffscreen`/`BoundingRect` are a find-time snapshot: for `App`'s locator actions, which re-find before every attempt, it's nearly fresh anyway, but a held handle — and the daemon, which stores handles between requests — would otherwise act on old state and aim at an old position. Holding Notepad's "New tab" File-menu item and clicking it after the menu closes showed both: cached state still said visible, and a native Invoke opened a new tab no user could reach. Playwright and Selenium likewise check a handle's live state at action time. (A first version read each property with its own call, eleven per synthesized click; one shared snapshot passed between helpers then cut that to two but tangled their signatures — independent helpers with one batched read each is the balance.)

Behavior change: acting on a hidden or disabled element used to succeed via native Invoke/SetValue and now waits, then throws `ElementNotInteractableError`. Task 2.2.5's `app.Force` is the way back; until it lands there is no opt-out. `WaitConditions.IsVisible()` now also requires non-empty bounds, and `WaitConditions.IsEnabled()` now passes for an unknown enabled state.

Existing tests adjusted for the new gate: `StaleElementResilienceTests`' failing handle now fails the one `BuildUpdatedCache` call instead of `CurrentNativeWindowHandle`; `StaleElementResilienceTests`' fresh handle sets non-empty bounds (an empty rect fails the gate), and its non-stale control runs with `ImplicitWaitMs=0`, the failed read falling back to find-time state so the check rejects it as disabled rather than stale; `ElementInputHelpersTests` covers each helper on its own: the check and the aim each judge one batched live read whose values override the find-time ones, `GetCenter` rejects empty bounds itself, and it still returns a point for a disabled element; `NotepadInteractionTests`' double-click, Shift+click, and Ctrl+click-on-a-word tests no longer wrap the Document's UIA element in a synthetic handle carrying a word's rect (live bounds would aim at the whole Document) — they click the word's point with public API only, `KeyDownAsync`/`ClickAtAsync(x, y)`/`KeyUpAsync` for the modifier clicks and two `ClickAtAsync(x, y)` calls (inside Windows' double-click time at the default `ActionDelayMs`) for the double-click, and its `GetTextPatternAsync` helper returns just the pattern; since those no longer exercise `ClickAction`'s modifier path or `DoubleClickAction`, two locator-based tests cover them end to end through the gate: `ClickAsync_Locator_WithShiftModifier_ExtendsSelectionToEnd` (Shift+clicking the Document's center, past the end of one short line, selects the whole line) and `DoubleClickAsync_Locator_SelectsWordAtDocumentCenter` (with every line one long unbroken word, the Document's center always lands on a word, which the double-click selects); `WaitConditionsTests` covers the empty-bounds and unknown-enabled rules, and its fixture now sets non-empty bounds.

**Creates:**
- `src/AutoMancer.Engine/Actions/ElementInputHelpers.cs` — private `ReadState` (one batched live read); `EnsureInteractable(element)` judges it with `WaitConditions`' shared rules and names the failed check; `GetCenter(element)` aims at the live bounds and rejects empty ones itself
- `src/AutoMancer.Engine/Core/WaitConditions.cs` — internal `Visible`/`Enabled`, the shared definitions `IsVisible()`/`IsEnabled()` now delegate to
- `src/AutoMancer.Engine/Actions/ClickAction.cs` / `TypeAction.cs` / `ClearAction.cs` / `DoubleClickAction.cs` / `HoverAction.cs` / `ScrollWheelAction.cs` — Click/Type/Clear call `EnsureInteractable` before the native-pattern attempt; DoubleClick/Hover/ScrollWheel call it before `EnsureForeground`, then aim with `GetCenter`
- `src/AutoMancer.Engine/App.cs` — `FindAndRunAsync` retries `ElementNotInteractableError`; `ClickAtAsync(Locator)` checks with `EnsureInteractable` before aiming

- [x] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

**Future extensions:**
- Folding Task 2.2.7's occlusion check into the base gate as Playwright's "receives events" step, if it proves cheap and reliable enough — it stays opt-in until then.
- Building handles with `FindFirstBuildCache`/`FindAllBuildCache` in the providers, so a find fetches every property `Wrap` reads in one call instead of one call each — the same batching, applied to finds.

---

### Task 2.2.5: `app.Force` to opt out per call, keyboard focus for synthesized typing, and tests for actionability

**What:** The per-call escape hatch for Task 2.2.4's gate, the keyboard counterpart of pointer input's aim point, then unit tests for all three, plus live Notepad regressions for the hidden-menu-item case that motivated Task 2.2.4 and for typing into an unfocused element.

**`app.Force`.** `public App Force` returns a cached view of the same `App`: same session, providers, and options, created once. Its `DisposeAsync` is a no-op (it shares the parent's session, which only the parent tears down), so disposing it never closes the app under test, and its own `ElementResolver` (over the parent's providers) stamps an internal `ElementHandle.Force` alongside `Logger`/`ForegroundActivationTimeoutMs`. A forced handle skips Task 2.2.4's enabled/visible gate and its wait — the documented escape hatch for invoking a hidden menu command on purpose, or for a framework whose `IsOffscreen` can't be trusted: `await app.Force.ClickAsync(Locator.ByName("New tab"))`. Force never skips stale detection (a forced action on a gone element still raises `StaleElementError` and still re-resolves under `ReresolveOnStale`), and a forced *synthesized* input still needs non-empty bounds — `GetCenter`'s own empty-bounds check (Task 2.2.4) already enforces this, independently of the gate — so it skips the waiting, never the need for a real point. The view's held keys/mouse buttons are tracked by the parent `App`, so disposing the parent releases input pressed through `app.Force`. Coordinate methods — `ClickAtAsync(x, y)` and the drag/mouse-move methods — target no element, so they have no gate, wait, or stale check to skip and behave identically through `app.Force`; both views still check the window isn't minimized and bring it to the foreground. That split is deliberate: Force means "act on this element regardless of its state", while acting on a point with no element behind it is what the coordinate methods are for — so Force never needs to aim at an element's last-known position.

Force is opt-in per call only — there's deliberately no `AppOptions.Force` run-wide default yet. A per-call parameter was rejected: a `bool force` before `ct` on every action would silently rebind positional `ct` (the same trap Task 2.1.7 hit), and one after `ct` breaks the CancellationToken-last convention (CA1068).

**Keyboard focus.** `TypeAction`/`ClearAction`'s synthesized fallback foregrounds the element's window and sends keystrokes (Unicode text, or Ctrl+A/Delete) to whichever control in it already has focus — the target element itself is never focused, so `TypeAsync(Locator.ByAutomationId("SearchBox"), ...)` on an element without a usable ValuePattern lands in the search box only if focus happened to be there. Pointer input aims at its target through `GetCenter`'s bounds; keyboard input's equivalent is focus. A new `ElementInputHelpers.EnsureKeyboardFocus` runs after `EnsureForeground` and before any keystroke: it calls `SetFocus` on the element (UIA3 `IUIAutomationElement.SetFocus`, UIA2 `AutomationElement.SetFocus`), then confirms live that focus landed on the element or one of its descendants (a combo box's inner edit, say) — the element's `HasKeyboardFocus`, or else a `FindFirst` over its descendants for one with `HasKeyboardFocus` (one call on the element itself, rather than walking up from the session's focused element comparing each ancestor, which also keeps it mockable). If not, it throws `ElementNotInteractableError` ("did not take keyboard focus"), which `FindAndRunAsync` waits on like any other not-yet-actionable element. A stale `SetFocus` or read raises `StaleElementError`, as `SetFocusAction` already does; any other `SetFocus` failure is logged and the focus read decides, so an element that rejects `SetFocus` but already holds focus still passes. A Win32 hwnd is focused with plain user32 — `SetFocus` while briefly joined to the target thread's input queue via `AttachThreadInput` (SetFocus only works within the caller's queue), confirmed by `GetGUIThreadInfo`'s `hwndFocus` being the hwnd or one of its children (`IsChild`), a window that no longer exists raising `StaleElementError`; `SetFocusAction` uses the same path. Alongside it `EnsureForeground` now foregrounds a Win32 hwnd's top-level window (it used to return early for any non-UIA handle, so Win32 keystrokes went to whatever app was foreground). Only a non-UIA stand-in, with nothing to focus, keeps today's type-into-the-window behavior. `app.Force` skips this check too: it still calls `SetFocus` and still raises `StaleElementError`, but when focus didn't land it logs and types anyway, so a caller can force text at an element that misreports focus, accepting that the keystrokes go wherever focus actually is (unlike empty bounds for a forced click, nothing physically blocks the input). Behavior change: synthesized typing into an element that won't take focus now waits, then throws, instead of typing into whatever has focus.

As built: `App`'s forced view comes from its private constructor (a `forcedFrom` parent argument rather than a separate internal one), and the held-input trackers stay private, with test-only internal accessors (`HeldKeysForTesting`/`HeldMouseButtonsForTesting`, beside `CreateForTesting`) so the unit tests can assert the view shares the parent's without sending real input. The "synthesized click aims at the live center" case is `ElementInputHelpersTests.GetCenter_Uia3_AimsAtLiveBounds` from Task 2.2.4, not repeated; the App-level cases live in `Resolver/ActionabilityWaitTests.cs`.

**Creates:**
- `src/AutoMancer.Engine/Actions/ElementInputHelpers.cs` — `EnsureInteractable` skips its gate for `ElementHandle.Force`; new `EnsureKeyboardFocus`
- `src/AutoMancer.Engine/Actions/TypeAction.cs` / `ClearAction.cs` — call `EnsureKeyboardFocus` between `EnsureForeground` and the keystrokes
- `src/AutoMancer.Engine/Core/ElementHandle.cs` — internal `Force`
- `src/AutoMancer.Engine/Providers/NativeMethods.cs` — `TrySetFocus` (`AttachThreadInput` + `SetFocus`) and `HasFocusWithin` (`GetGUIThreadInfo` + `IsChild`) for Win32 focus; `ElementInputHelpers.EnsureForeground` foregrounds a Win32 hwnd's top-level window
- `src/AutoMancer.Engine/Actions/SetFocusAction.cs` / `ScrollAction.cs` — UIA2 branches (and Win32 for focus), logging when nothing was focused or scrolled; `App.ScrollIntoViewAsync` drops its unconditional "Scrolled into view" log, and `GetVerticalScrollPercent` reads UIA2's `ScrollPattern`
- `src/AutoMancer.Engine/Core/ElementResolver.cs` — stamps `Force` on returned handles when constructed for a forced view
- `src/AutoMancer.Engine/App.cs` — `Force` property, built by the private constructor's `forcedFrom` argument so it shares the parent's held-input trackers; `DisposeAsync` a no-op for the forced view; `FindAndRunAsync` doesn't wait on `ElementNotInteractableError` when forced
- `tests/AutoMancer.Engine.Tests/Actions/ActionabilityTests.cs` — live state overrides find-time state in both directions (found visible/now hidden is rejected; found offscreen/now visible is allowed), and a synthesized click aims at the live center; a hidden or disabled element is rejected *before* the native Invoke/SetValue runs (the mock's Invoke is never called); empty bounds are rejected even with `IsOffscreen=false`; the error names which check failed; on the synthesized typing/clearing path, `SetFocus` is called on the element before any keystroke, an element that doesn't report keyboard focus afterward throws `ElementNotInteractableError` before any keystroke, focus landing on a descendant is accepted, a forced handle still attempts `SetFocus` but proceeds without focus, and a non-UIA stand-in skips the focus step (Win32 hwnds are focused, covered against real windows in `ElementInputHelpersTests`)
- `tests/AutoMancer.Engine.Tests/Resolver/ActionabilityWaitTests.cs` — `FindAndRunAsync` polls a not-yet-interactable element and succeeds once it becomes interactable, and throws `ElementNotInteractableError` after `ImplicitWaitMs`; through `app.Force` the same element is acted on immediately with no wait; `app.Force` on a stale element still throws `StaleElementError`; a forced synthesized click with empty bounds still throws; disposing `app.Force` leaves the session (and process) alive; disposing the parent releases a key held through `app.Force`; `app.Force` returns the same cached instance each time
- `tests/AutoMancer.Engine.Tests/Integration/Notepad/ActionabilityIntegrationTests.cs` — open Notepad's File menu and hold its "New tab" item twice, once found through `app` and once through `app.Force`, then close the menu: `ClickAction.ExecuteAsync` on the default handle throws `ElementNotInteractableError` (its live state now reads hidden) and opens no tab, while on the forced handle it opens one — tab count asserted both ways. (Both handles are found while the menu is open because no locator, `ByRuntimeId` included, can reach the item once it's closed.) Plus keyboard focus: move focus off Notepad's Document to another control, then type into the Document through the synthesized path (its handle's operator stripped so native SetValue can't run) — the text lands in the Document, not the control that had focus. And the Win32 equivalent: through the `win32` provider, move the Open dialog's focus to Cancel, then type into its file-name `Edit` — the text lands in the Edit. UIA2 coverage lives with the tests it extends: `NotepadInteractionTests.SetFocusAsync_Uia2Handle_SetsKeyboardFocusToElement` and `VirtualizedListFindIntegrationTests.FindByScrollingAsync_Uia2_FarDownItemInLongList_ScrollsUntilFoundAndIntoView`.

- [x] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category!=Integration&(FullyQualifiedName~Actionability|FullyQualifiedName~Force)"
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~Actionability"
```

**Done when:** All cases pass, including the live Notepad hidden-menu and unfocused-typing regressions.

**Future extensions:**
- `AppOptions.Force` — a run-wide default for a whole session against a framework whose `IsOffscreen` is unreliable; `app.Force` stays the per-call form.
- `ElementHandle.WithForce()` — force a handle already held from a non-forced `App` when no locator can re-find it (today: `app.Force.ClickAsync(Locator.ByRuntimeId(handle.Id))`, which works whenever the element is still findable).

---

### Task 2.2.6: Extend `LocatorExpect` with `WaitConditions`-backed assertions

**What:** `AutoMancer.Testing`'s `LocatorExpect` gets `ToBeEnabledAsync()` (built on Task 2.2.1's `WaitConditions.IsEnabled()`), `ToContainTextAsync(string substring)` (built on `WaitConditions.NameContains`), and `ToHaveCountAsync(int expected)` — Playwright's `toHaveCount`, polling single-pass `FindAllAsync` via `Poll.UntilAsync` (like `ToHaveValueAsync`) and reporting the last count seen on timeout; it's the wait half `FindAllAsync` itself deliberately doesn't do (see Task 2.2.2), and the `Expect()` form of Task 2.2.1's `NumberOfElementsToBe` future extension — so `Expect()` gets the same conditions `WaitForAsync` does, instead of only the four hand-written checks Stage 1.9 shipped (`ToHaveNameAsync`/`ToBeVisibleAsync`/`ToHaveTextAsync`/`ToHaveValueAsync`). Each new assertion follows the existing `WaitOrFailAsync` pattern already used by `ToHaveNameAsync` et al. — no new failure-message plumbing needed. The existing `LocatorExpect.ToBeVisibleAsync()` and `ElementExpect.ToBeVisible()` move onto `WaitConditions.IsVisible()` too: today they check only for non-empty bounds and ignore `IsOffscreen`, so a row scrolled out of view passes them while an action on it waits and throws — after this, `Expect()` visibility and enabled mean exactly what actions wait on (Task 2.2.4's shared rules).

**Creates:**
- `src/AutoMancer.Testing/LocatorExpect.cs` — add `ToBeEnabledAsync(CancellationToken ct = default)`, `ToContainTextAsync(string substring, CancellationToken ct = default)`, `ToHaveCountAsync(int expected, int? timeoutMs = null, int? pollIntervalMs = null, CancellationToken ct = default)`
- `src/AutoMancer.Testing/ElementExpect.cs` — `ToBeVisible()` delegates to `WaitConditions.IsVisible()`, its message adding `IsOffscreen`
- `tests/AutoMancer.Engine.Tests/TestingAdapter/LocatorExpectTests.cs` — extend with cases mirroring the existing `ToHaveNameAsync` tests (matches immediately, retries until match, times out with a descriptive message); `ToBeEnabledAsync` also passes for an unknown enabled state, `ToHaveCountAsync` also passes immediately for zero expected and no matches, and `ToBeVisibleAsync` rejects an offscreen element with non-empty bounds
- `tests/AutoMancer.Engine.Tests/TestingAdapter/ElementExpectTests.cs` — `ToBeVisible()` rejects an offscreen element with non-empty bounds

As built: `DescribeActualAsync`'s failure message adds the found element's `IsEnabled`/`IsOffscreen` (`unknown` when unreported), since `ToBeEnabledAsync`/`ToBeVisibleAsync` failures can't be explained by `Name` and `BoundingRect` alone.

- [x] **Write tests, run (expect FAIL), implement, run (expect PASS)**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "FullyQualifiedName~LocatorExpectTests"
```

**Done when:** `WaitForAsync` and `Expect()` share a starter vocabulary instead of `WaitForAsync` alone having one; `ToHaveCountAsync` waits for a collection to reach an expected size, the wait half single-pass `FindAllAsync` deliberately leaves out.

---

### Task 2.2.7: `App.IsUnobscuredAsync` — occlusion check

**What:** An opt-in occlusion check, not a gate on any existing action — `App.IsUnobscuredAsync(Locator locator, CancellationToken ct = default)`. Cross-window half works for every provider via a new `NativeMethods.WindowFromPoint` compared against the resolved element's own top-level HWND; same-window/sibling-overlay half only for `uia3`-resolved elements, via a new `Uia3Provider` internal helper that walks ancestors from `IUIAutomation.ElementFromPoint` (bounded by the existing `MaxTreeDepth`, compared via `CompareElements`) — a `uia2`/`win32`-resolved element degrades to the cross-window check alone rather than throwing. This is a different shape than the cached-state predicates of Tasks 2.2.1 and 2.2.6: it needs a live COM/Win32 call at check time, not a read of cached `ElementHandle` state, and it belongs in this stage anyway as the same actionability-primitive family Selenium's `ExpectedConditions`/Playwright's actionability checks live in.

**Where it sits.** Occlusion is a question about a point, so it's not part of Task 2.2.4's `EnsureInteractable` — native Invoke/SetValue and keyboard input don't care what covers an element. It sits beside `GetCenter` and takes its point from it, so it hit-tests exactly where a synthesized click would go:
1. **`ElementInputHelpers.IsUnobscured(ElementHandle element, (int X, int Y) point)`** — internal; `true` when input at `point` would reach `element` (the cross-window and same-window halves above).
2. **`App.IsUnobscuredAsync(locator)`** — finds the element fresh, takes the point from `GetCenter(element)`, and returns `IsUnobscured(element, point)`. A yes/no query, so `GetCenter`'s empty-bounds `ElementNotInteractableError` becomes `false` (nothing to hit) rather than propagating; a stale element still raises `StaleElementError`. Enabled/disabled is irrelevant to occlusion and isn't checked.
3. **No foregrounding.** It answers "is it covered right now" and changes nothing on the desktop — an app behind another window reports obscured, even though an action would foreground it first and succeed.

If it's later folded into actions (Task 2.2.4's future extension, Playwright's "receives events" step), it becomes a pointer-only step after foregrounding — `EnsureInteractable` → `EnsureForeground` → `GetCenter` → `EnsureUnobscured(element, point)`, throwing `ElementNotInteractableError` ("covered by …") that `FindAndRunAsync` waits on, so an overlay animating away doesn't fail the action. `app.Force` would skip it like the gate, while still needing `GetCenter`'s real point. It stays opt-in until then because it's the costliest and least certain check: `ElementFromPoint` is one call but the ancestor walk is one per level, transparent overlays and Electron/XAML hit-testing can report false coverage, and `uia2`/`win32` handles only get the cross-window half.

**Creates:**
- `src/AutoMancer.Engine/Providers/NativeMethods.cs` — add `WindowFromPoint` P/Invoke declaration
- `src/AutoMancer.Engine/Providers/Uia3Provider.cs` — internal ancestor-walk helper from `IUIAutomation.ElementFromPoint`
- `src/AutoMancer.Engine/Actions/ElementInputHelpers.cs` — internal `IsUnobscured(element, point)`
- `src/AutoMancer.Engine/App.cs` — `IsUnobscuredAsync(Locator locator, CancellationToken ct = default)`

As built: the helper is `Uia3Provider.HitTestReaches(target, x, y)`, walking raw-view ancestors (the same view `IsDetached` uses). A windowless UIA element (XAML, Electron) has a `NativeWindowHandle` of 0, so the cross-window half looks up its owning window as the nearest raw-view ancestor that has one. If that lookup fails without the element being gone, it logs and skips the cross-window half, the way `ReadState` handles a failed read. A failed `ElementFromPoint`/ancestor walk logs and reports `false`, because the failure comes from the hit element rather than the target, which `GetCenter` has just read live. `IsUnobscuredAsync` runs through `FindAndRunAsync`, so a stale element is re-resolved according to `ReresolveOnStale`, as in `GetValueAsync`. It catches `GetCenter`'s empty-bounds error inside the action, so that case returns `false` straight away without waiting. `HitTestReaches` returns `bool?`. When `ElementFromPoint` stops at one of the target's own ancestors, it returns `null` ("can't tell"), and `IsUnobscured` then falls back to the cross-window result, the same way `uia2`/`win32` handles do. Task 2.2.8 found the reason: once the managed UIA2 client loads in-process (any find that falls through to `uia2` in the default chain), UIA3's hit-test stops at a XAML island's `DesktopChildSiteBridge` host window instead of descending into it. Before this fallback, every XAML element then reported covered for the rest of the process. Checked live against VS Code (Electron) in `samples/ConsumerVsCodeTests/WorkbenchStateDemoTests.cs`:
- The Command Palette is reported covering the editor area.
- Right after the UI changes, Chromium can answer the first hit-test from the previous state, about one call within ~200ms. Every later call was correct. So poll rather than trusting a single call; `LocatorExpect` gained `ToBeObscuredAsync()`/`ToBeUnobscuredAsync()` for this. Notepad's Save-changes dialog needs the same: its backdrop fades in and out, and a single call right after the dialog appears or leaves the tree failed about one full-suite run in five.
- The title-bar caption buttons (Minimize/Maximize/Close) report covered when nothing covers them, because Chromium's hit-test returns the web content drawn underneath them.
- Elements on the taskbar-overlapped bottom edge of a window that extends under the taskbar correctly report covered.

- [x] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** `app.IsUnobscuredAsync(locator)` returns `false` when another window or an in-app overlay is covering the element's hit point (the point `ElementInputHelpers.GetCenter` returns — what a synthesized click would hit), `false` for an element with no on-screen bounds, and `true` once it's clear.

**Future extensions, once this lands:**
- `EnsureUnobscured` folded into pointer actions after `EnsureForeground`, per "Where it sits" above, once it's proven reliable on real apps. Folding it in would also absorb Chromium's stale first hit-test, since `FindAndRunAsync` already retries a not-yet-actionable element.
- `WaitConditions.ToBeClickable()` — a composite of `IsVisible()` + `IsEnabled()` (Task 2.2.1) + this task's live occlusion check; Selenium's single most-used `ExpectedCondition`. Needs this task first since it can't be a pure cached-state predicate like the rest of `WaitConditions`.
- `App.IsForegroundAsync(locator)` — a focus/foreground-window check via `GetForegroundWindow` comparison, same live-check shape as this task. Arguably higher real-world value than occlusion alone, since input silently landing on the wrong window is one of the most common WinAppDriver flake sources.
- A hung-window ("Not Responding") check via `IsHungAppWindow` or a `SendMessageTimeout` probe — same actionability-primitive family, catches slow-app flake that today has no engine-level answer.

---

### Task 2.2.8: Integration test for occlusion detection

**What:** Integration test against live Notepad — an on-screen button reports unobscured; the same button reports obscured once the Save-changes `ContentDialog` covers it.

**Creates:**
- `tests/AutoMancer.Engine.Tests/Integration/Notepad/OcclusionIntegrationTests.cs`

As built: the file sits with the other Notepad suites under `Integration/Notepad/`, on the shared `NotepadFixture`. The button is `SettingsButton`. The obscured case opens a second tab, types into it, presses Ctrl+W, and asserts Settings reports covered while the dialog's own "Don't save" reports clear. It then dismisses the dialog and asserts Settings is clear again, which covers Task 2.2.7's "`true` once it's clear". Seen live: the dialog's smoke layer covers the whole window, title-bar caption buttons included. A `uia2`-resolved Settings handle, which gets only the cross-window half, also reports covered, because `WindowFromPoint` puts the dialog's popup in a different top-level window from the button's. So this test can't isolate UIA3's same-window hit-test. It's an end-to-end check that both halves agree, not proof that the UIA3 half catches overlays by itself. A third case does a `uia2` find first and asserts Settings is still clear. It guards the UIA2 hit-test fallback described in Task 2.2.7's "As built"; these tests failed on that problem when the whole integration suite ran together, though they passed alone.

- [x] **Run integration tests**

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

As built: `ContextMenuAction` is `internal`. `FindPopupWindowAsync` became `OpenAsync`, because it owns the right-click as well as the detection: the before-snapshot below has to be taken before the click, so the two can't be split. It also takes the `AppSession`, because a provider search needs one. Task 2.3.3 split it in two:
- `OpenAsync(target, session)` takes the snapshot, right-clicks, and returns a `ContextMenuBaseline`.
- `TryDetectAsync(baseline)` makes one detection pass and returns the menu as one `ElementHandle`, or `null` if no menu has appeared yet. The handle is a UIA3 `Menu`, or, for the native path, the `#32768` popup as a `win32` handle whose `NativeHandle` is its HWND.

The polling now lives in the resolver (see Task 2.3.3).

Before the trigger fires, it records two things. First, the `Menu`s on screen inside the target's own top-level window, read through UIA3. Second, the visible top-level windows of every process involved: the session's, the target element's, and its containing window's (`NativeMethods.GetVisibleTopLevelWindows`). Only something that wasn't there before counts, so a menu already open, or a pre-loaded one still offscreen, is never mistaken for the new one. Each `TryDetectAsync` pass tries three things in order:

1. **An on-screen `Menu` in the target's window that's new.** This catches in-window menus and toolkit popups that UIA nests under the app window, and returns it as `UiaMenu`.
2. **A new top-level window, of any class, where UIA3 finds `MenuItem`s.** This always goes through UIA3 (a static `Uia3Provider` in `ContextMenuAction`), whichever provider found the target. The window is wrapped through a new `Uia3Provider.TryWrapWindow` (`ElementFromHandle`) and returned. A `StaleElementError` from a companion window that closed mid-search (e.g. a `SysShadow`) is skipped.
3. **Otherwise, a new `#32768`, returned wrapped by `Win32Provider`.** The native `HMENU` path (Task 2.3.2) drives it.

When no menu appears within `ImplicitWaitMs`, the resolver's polling raises the standard `ElementNotFoundError`; no menu-specific error is added.

Why step 2 exists: a popup menu is usually its own top-level window. UIA may expose it as a child of the desktop rather than of the app window, so the window-scoped search in step 1 never sees it, but UIA3 exposes it in full through `ElementFromHandle`. Window class alone can't drive this: `#32768` covers only menus USER draws (`TrackPopupMenu`, system menus, menu-bar dropdowns), the only kind with an `HMENU`. Toolkit menus use their own classes and are UIA-only. Checked live through `ContextMenuAction` against four menus:

| Menu | Path taken | Result |
|---|---|---|
| Native `TrackPopupMenuEx` (`#32768`), `uia3` target | Step 2: `Menu`, plus `NativeWindow` | Items had the right enabled state; clicking one delivered its command ID and closed the menu |
| Same menu, `uia2` target (2.3.1 run, before detection always used UIA3) | Step 3: native only | UIA2 sees the popup only as an empty `Pane`; today the same case goes through UIA3 |
| WinForms `ContextMenuStrip` | `Menu` (`WindowsForms10.Window…`) | Clicked an item; its handler ran |
| WPF `ContextMenu` | `Menu` (`ContextMenu`) | Clicked an item; its handler ran |
| WinUI3 Notepad | Step 2: a new `Microsoft.UI.Content.PopupWindowSiteBridge` `Pane` | Its full item list came back with correct enabled state |

Step 3 only runs when UIA3 finds no `MenuItem`s in any new window. Every native menu tried was visible to UIA3, so in practice the native path is the fallback for menus UIA3 truly can't see.

- [x] **Implement and build**

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

As built:

**Win32 calls.** `GetMenu`, `GetSubMenu` and `TrackPopupMenuEx` weren't needed:
- `GetMenu` reads a window's menu bar. A `#32768` popup gives up its `HMENU` through `MN_GETHMENU`, sent with `SendMessageTimeout` (`SMTO_ABORTIFHUNG`, 1s) so a hung app can't block the caller.
- `TrackPopupMenuEx` would open a second menu in our own process; it can't trigger an item in another app's open menu.

`NativeMethods` gains `GetPopupMenuHandle`, `GetMenuItemCount`, `GetMenuItemInfo` (declared `CharSet.Unicode`, or it binds to the ANSI export), `GetMenuItemRect`, the `MENUITEMINFO` struct, and one shared read, `ReadMenuItem(popup, hmenu, position)`. It returns the item's raw text, whether it's enabled (disabled means `MFS_DISABLED` or `MFS_GRAYED`) and its screen bounds, or `null` once the menu or position is no longer valid. Separators and owner-drawn items come back with no text. It was first built as a whole-menu `ReadMenuItems`. It became a single-item read when `Win32Provider` (building handles) and `ElementInputHelpers.ReadState` (re-reading one item live) both needed the same native calls; it's now the only place they're made.
**`ContextMenuAction` methods (as first built; since moved, see "Native menus as first-class elements" under Task 2.3.3).** They took the popup window rather than the `HMENU`, because `GetMenuItemRect` needs both:
**`ContextMenuAction` methods.** They take the popup window rather than the `HMENU`, because `GetMenuItemRect` needs both:
- `GetNativeItemsAsync(popup)` returns `ElementHandle`s in menu order, skipping items with no text, or `null` once the popup has closed. Each handle has `ResolvedVia` `"win32"`, `ControlType` `"MenuItem"`, and the item's name, enabled state and rectangle. Its `NativeHandle` is a `NativeMenuItemRef(PopupWindow, Menu, Position)`, deliberately not an `IntPtr`, so window-oriented input helpers never try to foreground it; foregrounding would dismiss the menu. Using `ElementHandle`s lets the existing `ElementNotInteractableError`/`StaleElementError` carry the item.
- `FindItemByNameAsync` was removed in the post-2.3.3 simplification pass: `ContextMenu` filters `GetNativeItemsAsync`'s list by name itself (ordinally, like UIA's `Name` condition).
- `InvokeItemAsync(item)` re-reads the item live and throws `StaleElementError` if the popup no longer shows the same `HMENU`; a closed popup's window handle can be reused for the next menu. It throws `ElementNotInteractableError` for a disabled item unless `item.Force` is set. Then it clicks the live center with `SendMouseClick`, which sends a leading move, without foregrounding anything. Windows ignores a forced click on a disabled item: the menu stays open and no command fires.

**Names match UIA3.** `MenuItemDisplayName` converts raw text to the `Name` UIA3 reports for the same item, so one `itemName` works on both paths. It drops the tab-separated shortcut and lone `&` mnemonics but keeps `&&` as-is, as UIA3's menu proxy does: `B&ravo && Co` becomes `Bravo && Co`.

**Checked live** against a `TrackPopupMenuEx` menu opened through a `uia2` target, which forces the native path:
- Names, enabled states and rectangles matched UIA3's view item for item.
- Clicking an item delivered its command ID.
- A disabled item was refused.
- An item held after Escape closed the menu raised `StaleElementError`.

**Limits.** Cascading submenus are only reachable as top-level items; a submenu gets its own popup only once it's opened. Owner-drawn items can't be matched by name.

- [x] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 2.3.3: `App.OpenContextMenuAsync` and `App.ClickContextMenuItemAsync`

**What:** Two public entry points, a step-by-step one and a one-call shortcut built on it.

**Why opening is its own public step:** a right-click through `RightClickAsync` followed by a `FindAsync` for the item only works when UIA nests the menu under the app window (WinForms `ContextMenuStrip`). WinUI3 Notepad's menu and a classic `#32768` popup are separate top-level windows, which the session-rooted search never sees. A `#32768` that UIA can't see has no public route at all. Without this step, the only way to reach those menus would be a single click-by-name call, with no chance to inspect the menu before acting.

**`App.OpenContextMenuAsync(Locator target, CancellationToken ct = default)` → `ContextMenu`**
- Finds `target` through `FindAndRunAsync`, so a stale target is re-resolved, like every other action. Then it calls `ContextMenuAction.OpenAsync`, passing `ImplicitWaitMs`/`PollIntervalMs`.
- **When no menu appears** (`OpenAsync` returns `null`), it falls back to a resolver `FindAsync(Locator.ByControlType("Menu"))`. A miss there raises the standard `ElementNotFoundError` from the resolver itself, which keeps the "only `ElementResolver` throws it" invariant. A late hit is used as the menu.

**`ContextMenu`** (`src/AutoMancer.Engine/Core/ContextMenu.cs`) is a public class with an internal constructor. It holds the `App` and the `ContextMenuPopup`. The UIA menu handle and the native window handle stay internal, the same way `ElementHandle` keeps its provider internal. Members:
- **`GetItemsAsync()`** → `IReadOnlyList<ContextMenuItem>`, where `ContextMenuItem` is a public record (`Name`, `IsEnabled`). On the UIA path it's a scoped `MenuItem` search under the menu. On the native path it's Task 2.3.2's enumeration. Lets a test assert on what a menu offers (e.g. Notepad's "Insert table" is disabled) without clicking anything.
- **`ClickItemAsync(string itemName)`.**
  - *UIA path:* polls a scoped `MenuItem` find by name for up to `ImplicitWaitMs`, because some menus fill in after they open (Notepad's AI items). Each found handle is stamped with the logger, the foreground-activation timeout and `Force`, as `ElementResolver` would do. The click then goes through `ClickAction`, so the actionability gate refuses a disabled item unless `app.Force` is used.
  - *Native path:* Task 2.3.2's lookup and invoke, with the same disabled-item rule.
  - *Either path:* it waits for the UI to settle afterwards (`SettleAsync`).
- **`CloseAsync()`:** dismisses a still-open menu with Escape, for a test that inspects a menu and doesn't click anything.
- **A menu that has since closed** is reported as stale (`StaleElementError` on the UIA path, the equivalent on the native path), not as a missing item. It isn't reopened automatically, because a second right-click could land somewhere else.
- **Item not found:** the error a missed item raises in `ClickItemAsync` is decided at implementation. The "only `ElementResolver` throws `ElementNotFoundError`" invariant either covers it or gets one stated exception.

**`App.ClickContextMenuItemAsync(Locator target, string itemName, CancellationToken ct = default)`:** `OpenContextMenuAsync(target)` followed by `ClickItemAsync(itemName)`.

**Not covered here:** `ContextMenu` lives in-process only. The Phase 3 daemon would need to store it by id between requests, the way it stores `ElementHandle`s, and that's left for Phase 3.

**Creates:**
- `src/AutoMancer.Engine/Core/ContextMenu.cs`: `ContextMenu` and `ContextMenuItem`
- `src/AutoMancer.Engine/App.cs`: `OpenContextMenuAsync(Locator target, CancellationToken ct = default)` and `ClickContextMenuItemAsync(Locator target, string itemName, CancellationToken ct = default)`

As built:

**Error handling stays inside the resolver.** `ElementResolver` gains an internal `PollOutsideChainAsync(locator, source, find, ct)`. It polls a lookup the provider chain can't make, every `PollIntervalMs` up to `ImplicitWaitMs`, and applies the App's settings (logger, foreground timeout, `Force`) to the handle it returns, as the resolver's own polling does. On timeout it throws `ElementNotFoundError` with `source` as the attempted provider, e.g. `uia3 context menu` or `win32 context menu`. It gives no closest-match hint, since it never searched the session tree. That settles both open questions while keeping the "only `ElementResolver` throws `ElementNotFoundError`" invariant intact:
- `OpenContextMenuAsync` polls `TryDetectAsync` through it, replacing the planned fallback to a resolver `Menu` find. That fallback could have returned an old, pre-loaded menu.
- A missed item in `ClickItemAsync` raises `ElementNotFoundError` through it too.

Only the item about to be clicked goes through that polling, so it's the only one that needs those settings. `GetItemsAsync` returns plain records, and `ApplySettingsTo` stays private.

**`ContextMenu`'s dependencies.** It takes the session, the `AppOptions`, the App's resolver and the detected menu handle (UIA3 `Menu`, or a native `#32768` wrapped by `Win32Provider`; its own `Provider` lists the items, so there's no path switch), not the `App` itself. Holding the resolver means `app.Force.OpenContextMenuAsync` stamps `Force` onto the item it clicks.

**Items are matched by name in code.** `GetItemsAsync` and `ClickItemAsync` both read the menu's full `MenuItem` list and filter by `Name`, rather than running a scoped `ByName` find, because a WPF `MenuItem`'s child `Text` carries the same name.

**Retries match `FindAndRunAsync`.** `ClickItemAsync` keeps retrying a stale item (under `ReresolveOnStale`) and a disabled one (unless forced) until `ImplicitWaitMs` runs out. A closed menu is never retried: every item read first checks the menu is still open, and throws `StaleElementError` ("The context menu has closed; open it again with App.OpenContextMenuAsync.") if not.

**How "open" is decided:**
- Native: the popup still answers `MN_GETHMENU`.
- UIA: the menu is attached, on screen, and its containing window is visible. That last check was added after a live run: a closed WinForms `ContextMenuStrip` only hides its dropdown window, and UIA keeps reporting it as attached and on screen. The check is `ElementInputHelpers.IsShowing`, composed from shared internal building blocks rather than its own logic: `ReadState` (the live read, also behind `EnsureInteractable` and `GetCenter`), `WaitConditions.Visible` (the one visibility rule), `IsDetached`, and a new general `IsInVisibleWindow`. It differs from the action gate only by skipping the enabled check, returning a bool instead of throwing, and adding the hidden-window rule. Whether that rule belongs in the shared definition of visible, which would give every action and wait the same check, is an open question for after Stage 2.3.
- Detection's in-window step uses the same `IsShowing` re-check on each candidate. The sample `ContextMenuDemoTests` found why: opening a menu with Shift+F10 straight after clicking an item in the previous one (3 of 12 back-to-back attempts) let a pre-loaded WinUI3 `MenuFlyout` show as on screen for a moment, then read offscreen a millisecond later. Detection took it as the new menu, so the menu immediately read as closed. With the re-check: 0 of 24.

**Sample.** `samples/ConsumerNotepadTests/ContextMenuDemoTests.cs` shows the API from a test author's side: the shortcut (Undo after `TypeDirectAsync`; `TypeAsync`'s set-value leaves nothing to undo), inspect-then-close, and Shift+F10.

**Closing.** `CloseAsync` is a no-op on a closed menu, so a stray Escape can never reach the app. Otherwise it sends Escape (`KeyboardAction`, no foregrounding) and waits for the menu to close through the resolver's `PollUntilGoneOutsideChainAsync`, which throws `ElementStillPresentError` if it doesn't. That keeps every wait-timeout error raised by the resolver. `ClickContextMenuItemAsync` closes the menu if the click fails, so it can't swallow the next input. A close failure there is swallowed so the original error survives.

**Logging.** The engine's JSON logger can't serialize `IntPtr`, so popup handles are logged as `ToInt64()`.

**Smoke test.** The CLI has no context-menu command, so the manual smoke test was a scripted live run of the public API instead. It ran against four menus:
- a native `TrackPopupMenuEx` menu, through UIA3 and through a UIA2-only provider chain (the native path);
- a WinForms `ContextMenuStrip`;
- a WPF `ContextMenu`;
- WinUI3 Notepad.

It covered:
- listing items, with names and enabled state matching on both paths;
- `ClickItemAsync` and the `ClickContextMenuItemAsync` shortcut both delivering the command;
- `CloseAsync` as a no-op after a click;
- `StaleElementError` from a closed menu;
- a disabled item refused after `ImplicitWaitMs`;
- a missing item raising `ElementNotFoundError` with the menu closed afterwards, then a clean reopen;
- on Notepad, open, list and close.

Unit tests cover `PollOutsideChainAsync` in `ElementResolverTests`.

**Follow-up: widening how and where menus are found.** A review after 2.3.3 found three places where opening a menu still assumed too much:
- **How it opens.** A new public `ContextMenuTrigger` enum (`RightClick`, the default; `ShiftF10`; `LeftClick`) is an optional parameter on `OpenContextMenuAsync` and `ClickContextMenuItemAsync`. `ShiftF10` focuses the target through `SetFocusAction` first, because the shortcut opens the focused element's menu. `LeftClick` covers "⋯" and split-button menus.
- **Which provider detects.** Detection always looks through UIA3, a static `Uia3Provider` in `ContextMenuAction`, whichever provider found the target. Before, a `uia2`-found target lost UIA3 detection entirely. A first version only used UIA3 when the App's chain included it, so that a `uia2`-only chain could force the native path for testing. That was dropped as overengineering that shaped product behavior around a test. The chain still decides how the target is found, not how its menu is detected.
- **Where it looks.** Step 1 now searches the target's own top-level window rather than the session root, which matters for a target in a secondary window. The window diff now spans the session's process, the target element's process and its containing window's process. Packaged apps whose frame window belongs to a host process are the case this is meant for.

Checked live with every regression case from the 2.3.3 run plus:
- a `uia2`-only chain driving a native menu through UIA3;
- WPF `ShiftF10` on a `TextBox` (its built-in Cut/Copy/Paste menu, with the right enabled state);
- WPF `LeftClick` on a "⋯" button whose click opens a `ContextMenu`;
- a right-click in a WPF secondary owned window, driven from the main window's `App`;
- WinUI3 Notepad `ShiftF10`;
- Calculator's results display (Copy/Paste).

**What wasn't exercised:** the cross-process case. Win11 Calculator's popup belongs to the session's own process, so no app tried here put its popup in a different process.

**Follow-up: native menus as first-class elements.** A later review moved the native-menu code out of the context-menu files and into the classes that already own each job. The result is that `ContextMenu` treats both paths identically:
- **Finding items → `Win32Provider`.** A scoped search whose scope is a `#32768` popup returns that popup's menu items, read from the `HMENU` it displays, instead of child windows. It matches by `Name` (substring, case-insensitive, like window titles) or `ControlType` `"MenuItem"`. Each item's `NativeHandle` is a `NativeMenuItem(PopupWindow, Menu, Position)`, deliberately not an `IntPtr`. `MenuItemDisplayName` moved here too. `WrapWindowHandle` became internal so detection can wrap the popup itself, giving the native menu handle a `Provider` like any other element.
- **Live state → `ElementInputHelpers.ReadState`.** A new `NativeMenuItem` case re-reads the item from its popup, and raises `StaleElementError` once the popup closes or shows a different `HMENU`. Through it, the existing `EnsureInteractable` (disabled check and `Force`), `GetCenter` and `IsShowing` work for native items unchanged.
- **Clicking → plain `ClickAction`.** No new operator was needed. `ClickAction` already handles a handle with no operator: actionability gate, no foregrounding for handle types it doesn't recognize, then a synthesized click at the live center.
- **Which processes can show an element's UI → `ElementInputHelpers.GetOwningProcessIds`.** The element's own process plus its containing window's. Detection adds the session's process, and derives its search scope itself as `NativeMethods.GetTopLevelWindow(GetContainingWindow(target))`, the same composition `IsUnobscured` uses.

**What that leaves:**
- `ContextMenuAction` holds only the trigger, the before/after snapshot and the three detection steps. `GetNativeItemsAsync`, `InvokeItemAsync`, `NativeMenuItemRef` and its native helpers are gone.
- `ContextMenu` has no path switch: items come from `_menu.Provider`, clicks from `ClickAction`, and "open" from `IsShowing`.

**Visible differences:**
- A disabled native item now fails with the standard message ("is disabled (IsEnabled=false)").
- A native item held across a close fails with the standard stale message ("is no longer in the UI tree").

**Verified:** the full unit suite; the full engine integration suite (99/99); the Notepad sample; the scratch native, WinForms and WPF apps through the public API; and the native path driven directly, covering listing, missing item, disabled, forced-disabled (menu stays open), click, a held item going stale after close, and close.

**Foreground-lock gotcha for Task 2.3.4.** If a test's last action goes through UIA Invoke, which sends no input, and the app is then killed, Windows can refuse to foreground the next launched app (`WindowActivationError`). That makes the next test's first click fail. Seen when tests ran back to back; ending each test on a real synthesized click removed it. This is pre-existing `ClickAction` behavior, not specific to context menus.

**Testing the native path in Task 2.3.4.** With detection always going through UIA3, a provider chain can't force the native path, and no menu tried was invisible to UIA3. So the native machinery (`Win32Provider`'s `#32768` scoped search, `ReadState`'s native-item case, `ClickAction` on native items) gets tested by building a native-path `ContextMenu` directly around a real `#32768` popup wrapped by `Win32Provider.WrapWindowHandle`, using its internal constructor, rather than through `OpenContextMenuAsync`'s fallback decision.

- [x] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors. A manual CLI/REPL smoke test right-clicks a live element and clicks a named item by eye, and also opens a menu, lists its items and closes it without clicking anything.

---

### Task 2.3.4: Integration test for context menu fallback

**What (original plan):** Verifies the UIA path is tried first and the native fallback only engages when UIA finds nothing, against an app with a legacy/native context menu (a plain Win32 app without a UIA-exposed menu is the target — Notepad's WinUI3 menus are UIA-visible, so this needs a different test app, e.g. one of the Stage 2.7 example apps or a minimal native test harness).

**Revised plan (proposed after 2.3.1–2.3.3; for review before starting).** Coverage is meant to be as wide as possible: every menu kind seen during 2.3.1–2.3.3, every trigger, both paths and every error case. Two findings change the original plan:
- **No app was found whose `#32768` menu UIA3 can't see.** Detection now always goes through UIA3 first, so a provider chain can't force the native path either. The native path is therefore tested by building a native-path `ContextMenu` directly around a real `#32768` popup, through its internal constructor. That tests the native machinery fully. It can't test the "UIA3 found nothing, so fall back" decision end-to-end, because nothing triggers it.
- **Foreground lock.** If a test's last action goes through UIA Invoke, which sends no input, and the app is then killed, Windows can refuse to foreground the next app. Fixtures keep one app instance per test class instead of relaunching per test. Each test ends with no menu open, and the last action before a fixture's teardown is real synthesized input.

**Test apps** (new; in the solution and built by the test project, launched from its output folder):
- **`tests/TestApps/ContextMenuHost`.** WinForms; the menu itself is real Win32 (`CreatePopupMenu`/`TrackPopupMenuEx`), so this is a "native app" for every purpose that matters here. A status label shows the last command picked (e.g. `picked 102`), so tests assert on visible results. It contains:
  - a **native-menu area**. Right-click and `WM_CONTEXTMENU` (so `ShiftF10` works) both open one `#32768` menu, with items covering every naming and state case: `&Alpha\tCtrl+A`, a separator, `B&ravo && Co`, a grayed `Charlie`, an owner-drawn item, and a cascading `Delta` submenu with children. The last two pin down the documented limits.
  - a **"⋯" button** that opens the same native menu on left-click (`LeftClick`).
  - a **WinForms `ContextMenuStrip` area**: the in-window toolkit popup case.
  - a **secondary owned form** with its own native menu: the target-in-another-window case.
- **`tests/TestApps/WpfMenuHost`.** WPF: a `TextBlock` with a `ContextMenu`, a `TextBox` with its built-in Cut/Copy/Paste menu (`ShiftF10`), and a "⋯" `Button` whose click opens a `ContextMenu` (`LeftClick`). Plus a status label.

**Tests** (`tests/AutoMancer.Engine.Tests/Integration/ContextMenu/`, `Category=Integration`, with a class fixture per host):

| File | Covers |
|---|---|
| `ContextMenuDetectionTests.cs` | Each menu kind is detected and reports its path (`uia3 context menu`): native `#32768` (UIA3 popup-window step), WinForms strip (in-window step), WPF popup, the secondary-window menu. A `uia2`-found target still detects through UIA3. No-menu timeout raises `ElementNotFoundError` (`tried: context menu detection`). |
| `ContextMenuTriggerTests.cs` | `RightClick`, `ShiftF10` and `LeftClick` on each host where the app supports them. |
| `ContextMenuItemTests.cs` | `GetItemsAsync` names and enabled state, including `Bravo && Co`. `ClickItemAsync` delivers the command. A disabled item is refused with `ElementNotInteractableError` after `ImplicitWaitMs`; `app.Force` goes through (with the observed outcome recorded). A missing item raises `ElementNotFoundError` naming the path: from `ClickItemAsync` the menu stays open; from the `ClickContextMenuItemAsync` shortcut it's closed. A closed menu raises `StaleElementError`. `CloseAsync` closes an open menu and is a no-op on a closed one. |
| `NativeMenuPathTests.cs` | A native-path `ContextMenu` built directly around the real `#32768`. Item list matches the UIA3 path's names and states exactly. Separator and owner-drawn items are skipped. The submenu parent is listed and clickable, but its children aren't reachable (documented limit). Click delivers the command. Disabled is refused. `Force` click is ignored by Windows. An item held across close and reopen is `StaleElementError` (the HMENU mismatch check). `CloseAsync` behaves the same as on the UIA path. |
| `MenuItemDisplayName` unit tests (existing, `Providers/Win32ProviderMenuItemTests.cs`) | Extended with the owner-drawn and submenu raw texts as they actually come back. |

**Real-app coverage, in the existing suites:**
- **Notepad** (`Integration/Notepad/`, under the run-alone rule): open, list and close via `RightClick` and `ShiftF10`, with disabled items reported correctly. Then type text, click `Undo` from the context menu, and assert the text is gone. That's a real click through the WinUI3 popup-window path.
- **Calculator** (`Integration/Calculator/`): `Copy` from the results display's context menu, asserting the clipboard holds the displayed value. Packaged app and a real popup window; known to be timing-sensitive (see the UWP flake memory), so it gets the existing Calculator fixture's warm-up.

**Not covered, and why:**
- **Cross-process menus** (a popup owned by a different process than the session's): no available app does this; Win11 Calculator's popup is in its own process. The process-union code stays unverified live.
- **The automatic UIA3-to-native fallback decision end-to-end**: it needs an app whose menu UIA3 can't see, and none was found.

**Creates:**
- `tests/TestApps/ContextMenuHost/` and `tests/TestApps/WpfMenuHost/`, added to `AutoMancer.slnx`
- `tests/AutoMancer.Engine.Tests/Integration/ContextMenu/` (the four files above plus fixtures)
- Additions to the Notepad and Calculator integration suites

As built (after Stage 2.9, so the tests target `Popup`; where the plan says `ContextMenu`/`GetItemsAsync`/`ClickItemAsync`, read `Popup`/`FindAllAsync(MenuItem)`/`ClickAsync` or `ClickContextMenuItemAsync`):
- **Test apps** as planned, but **kept outside the repo**, in `%TEMP%\AutoMancer.TestApps\ContextMenuHost` and `\WpfMenuHost`, not in `tests/TestApps` or the solution. The shared `TestAppFixture` base builds an app there with `dotnet build` if its exe is missing, and fails with the build output if the sources aren't there. Two additions: clicking either app's status label resets it to "ready", so a status assertion can't pass on an earlier test's pick; and the WPF window opens at a fixed spot clear of the WinForms one, since both run at once. The fixture ends on a synthesized click before killing the app.
- **Engine bug found and fixed: the baseline was taken before the target's window was foregrounded.** With another app in front, the first Shift+F10 on the WinForms app was "detected" as the window's title-bar `System` MenuBar: activating the window made its `System` MenuItem read as on screen, so it looked new. `PopupAction.OpenAsync` now foregrounds the target's window before snapshotting; the trigger foregrounded it anyway, just after the snapshot.
- **What UIA3 reports for the native menu:** names without mnemonics or the tab-separated shortcut, `&&` kept as-is, and the owner-drawn item listed with an empty name. The native path skips that item (its HMENU entry has no text), so its list is UIA3's minus the empty name. Everything else matches exactly.
- **`Force` on a grayed item:** Windows ignores the click on both paths; no command, and the menu stays open.
- **The shortcut's missing-item error** names `uia3 context menu`, and leaves no `#32768` showing.
- **Calculator's `Copy`** is checked through the clipboard with a few `user32` calls in the test, since nothing in the engine reads it yet (Task 2.8.1).
- **`MenuItemDisplayName` unit tests** gained the submenu texts (`&Delta`, `Delta &One`). The owner-drawn item has no raw text, so it never reaches that method.
- **A test-app bug found live, fixed in the app:**
  - The WinForms "⋯" button opened its menu inside its click handler. `TrackPopupMenuEx` is modal, and the engine clicks a button through UIA Invoke, so the Invoke didn't return until the menu closed (a 65-second test). The menu now opens after the handler returns.
- **Not fixed in the app or the engine: two quick clicks on the same spot are a double-click.** Reopening the native menu within the double-click time failed every second time: Windows turns a second right-click at the same point into `WM_RBUTTONDBLCLK`, which the edit control opens no menu for. This applies to any two engine clicks at the same point inside the double-click time, not just this app (Notepad's and the WPF menus didn't show it). Decided: the engine doesn't guard against it; callers click somewhere else in between, and `WINDOWS_TESTING.md` documents it. The context-menu test classes click the host's status label before every test (an `IAsyncLifetime` per class), and `ItemHeldAcrossReopen_IsStale` clicks it before reopening.

- [x] **Run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&(FullyQualifiedName~ContextMenu|FullyQualifiedName~NativeMenuPath)"
```

Passed: the 35 tests above, twice in a row, and the full engine integration suite (133/133).

**Done when:** every case above passes. In particular, `app.ClickContextMenuItemAsync(target, itemName)` clicks items through UIA3 on native, WinForms, WPF and WinUI3 menus, and a native-path `ContextMenu` lists and clicks items through the `HMENU` alone. Stage 2.3 complete.

---

## Stage 2.4 — Resource Diagnostics

### Task 2.4.1: Resource measurement

**What:** `ResourceMonitor.MeasureAsync(App app, Func<Task> work)` reads the app's GDI/USER objects (`GetGuiResources`), kernel handle count, thread count, working set and private bytes before and after `work`, and returns a `ResourceSummary` with after-minus-before deltas and a readable table `ToString()`. `Capture(app)` takes a single reading and `ResourceSample.DiffTo` compares any two, for checkpoints. Internal `CaptureHost()`/`MeasureHostAsync(work)` measure the process running AutoMancer (not the machine) with no `App` involved, garbage-collecting at each reading so unfinalized wrappers do not read as leaks; they are internal because checking the engine itself is the maintainers' question, not a tester's, and the README and sample only cover the app-side API. It lives outside `App` because it needs only `App.ProcessId`. (An earlier timer-based `ResourceWatch` sampler and an `App`-method version with a `ResourceTarget` flag were built and removed: a sample history added a background thread, a lock and polling waits that the leak use case never read.)

**Creates:**
- `src/AutoMancer.Engine/Providers/NativeMethods.cs` — add `GetGuiResources` P/Invoke, `GR_GDIOBJECTS`/`GR_USEROBJECTS` constants
- `src/AutoMancer.Engine/Diagnostics/ResourceMonitor.cs`, `ResourceSample.cs`, `ResourceSummary.cs`
- `src/AutoMancer.Engine/App.cs` — no change; `ResourceMonitor` uses the public `App.ProcessId`

- [x] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors.

---

### Task 2.4.2: Tests for resource measurement

**What:** Wraps a resource watch around a live app across several launch/kill cycles — reusing the existing Notepad/Paint test fixtures rather than a dedicated new one — and asserts the sample series is non-empty and plausible (handle counts and working-set bytes both present and within a sane range, not necessarily flat).

**Creates:**
- `tests/AutoMancer.Engine.Tests/Diagnostics/ResourceSummaryTests.cs`, `tests/AutoMancer.Engine.Tests/Integration/ResourceMeasureIntegrationTests.cs`

- [x] **Run integration tests**

```bash
dotnet test tests/AutoMancer.Engine.Tests/ --filter "Category=Integration&FullyQualifiedName~ResourceMeasure"
```

**Done when:** `ResourceMonitor.MeasureAsync(app, work)` returns before/after GDI, USER, handle, thread and memory readings with deltas. Stage 2.4 complete.

As built: the integration tests measure Notepad around typing, and measure the engine host (`ResourceMonitor.MeasureHostAsync`) across launch/kill cycles after one warm-up cycle. 20 `FindAsync` calls grow no handles, but 10 launch/kill cycles grew the host by roughly 30-50 kernel handles even after a GC, so `MeasureHostAsync_LaunchKillCycles_DoNotLeakHandles` currently fails against its bound of 20: an open finding in launch/kill, not yet traced to an owner.

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

## Stage 2.6 — Idle Wait and Visual Regression

> **Two general-purpose capabilities, useful against any app, UIA-accessible or not:** `WaitForIdleAsync` and visual regression. The Visual Provider (OCR and template-matching locator fallback) used to share this stage and now lives in "Future Work: Visual Provider" at the end of this document. See roadmap-spec.md's Stage 2.6.

---

### Task 2.6.1: `App.WaitForIdleAsync()`

**What:** Diffs consecutive `ScreenshotAsync()` captures at a short interval until two frames match within a pixel-difference threshold, or a timeout is hit. Replaces the ad-hoc `Task.Delay(300) // flyout animation` waits already scattered through the Phase 1 integration test suite with a real settledness check — likely able to reuse the pixel-compare plumbing Task 2.6.3 builds for template matching rather than needing a separate diff implementation, assuming that plumbing ends up general enough once 2.6.3 actually lands.

**Creates:**
- `src/AutoMancer.Engine/App.cs` — `WaitForIdleAsync(TimeSpan? pollInterval = null, TimeSpan? timeout = null, CancellationToken ct = default)`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** `app.WaitForIdleAsync()` resolves once consecutive screenshots stop changing. As a follow-up cleanup (not required for this task's own "done"), the scattered `Task.Delay(300)` animation waits in the existing integration suite are good candidates to replace with this.

---

### Task 2.6.2: Visual regression snapshot assertion

**What:** Captures the current window/element region and pixel-diffs it against a stored baseline PNG, failing past a configurable difference threshold. Likely shares its pixel-compare code with Task 2.6.1.

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

**Done when:** A visual regression assertion fails when a window's rendered output drifts from a stored baseline. Stage 2.6 complete — animation-settle waits and pixel-level regression assertions. This stage is entirely engine-side.

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

**What:** Three runnable example projects demonstrating the engine's public surface for new C# consumers — `examples/notepad/` (the README's Calculator-style walkthrough, expanded), `examples/winforms-calculator/` (a classic Win32/WinForms app, exercising the UIA2 fallback path), `examples/xunit-test-adapter/` (a minimal `AutoMancer.Testing.XUnit` walkthrough, complementing `samples/ConsumerNotepadTests`).

**Creates:**
- `examples/notepad/`, `examples/winforms-calculator/`, `examples/xunit-test-adapter/` — each a minimal runnable console or test project with its own short README

- [ ] **Build and run each example**

```bash
dotnet build examples/notepad/
dotnet build examples/winforms-calculator/
dotnet build examples/xunit-test-adapter/
```

**Done when:** All three example projects build and run. Stage 2.7 complete — DPI/OS compat confidence, annotated failure diagnostics, and working examples. This stage is entirely engine-side; exposing the debug-screenshot flag over HTTP is Phase 3's job (daemon-spec.md Task 3.2.5/`automancer:debugScreenshots`).

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

## Stage 2.9 — Windows and Popups

> **Runs now, in the middle of Stage 2.3, before Task 2.3.4**, despite its number (stages are append-only within a phase), so 2.3.4's tests target the final `Popup`-based shape. See roadmap-spec.md's Stage 2.9 for the framing. In short: searches stay scoped to one window; real windows are reached through explicit *window views*; popups are reached through the interaction that opened them, with Stage 2.3's `ContextMenu` as the first case. A find never falls back to popups automatically, which would bring back "which 'Undo'?" ambiguity. Planning decisions so far:
> - no automatic popup fallback;
> - the API is named `WaitForWindowAsync`;
> - `FindDialogAsync` was removed outright once window views covered every dialog tried (Task 2.9.4); the repo is too early for `[Obsolete]` shims;
> - the popup pattern is generalized now, not left context-menu-only.

**Starting facts (checked in code during Stage 2.3):**
- **Every search starts from one window.** `Uia3Provider`/`Uia2Provider` start every search from `session.RootWindowHandle`, and `Win32Provider` only enumerates that window's *child* windows. Snapshots, XPath and path locators start there too.
- **Owned windows happen to be reachable.** UIA nests a window under its owner, which is why a WPF window created with `Owner = main` and a WinForms `ContextMenuStrip` dropdown were findable from the main window. Unowned top-level windows aren't: Notepad's popup-window menu, `#32768` menus, unowned dialogs, second main windows.
- **Per-element machinery already copes with other windows.** Foregrounding (`EnsureForeground` uses the element's own top-level window), the actionability gate, `GetCenter`, `IsShowing`, `IsUnobscured`, stale detection and scoped UIA finds all work for an element in another window once you hold it. Only `EnsureWindowNotMinimized` (checks the root) and `Win32Provider.ValidScopeHandle` (requires the session's process) assume the root.
- **`FindDialogAsync` has a dispose bug.** It returns a separate `App` around the same process, and `App.DisposeAsync` calls `KillAppBestEffort()`, so disposing the dialog `App` kills the application.

### Task 2.9.1: Disposing a dialog `App` must not kill the app

**What:** An `App` from `FindDialogAsync` doesn't own its process. Its `DisposeAsync` releases what it holds (held input, the event-log artifact) but never kills the process; only the `App` that launched or attached to the app does.

**Creates:**
- `src/AutoMancer.Engine/App.cs`: an "owns the process" flag set by the launch/attach factories and cleared for `FindDialogAsync`; `DisposeAsync`/`KillAsync` honor it (`KillAsync` on a non-owning `App` is decided here: throw, or kill on explicit request)
- `tests/AutoMancer.Engine.Tests/`: a unit test that disposing a non-owning `App` leaves the process alone

**Done when:** a test that disposes a dialog `App` leaves Notepad running.

As built: `App` gains a private `_ownsProcess` flag, set by an optional `ownsProcess` constructor argument (default `true`).
- **Which `App`s don't own their process:**
  - `FindDialogAsync` passes `false`.
  - **`WithOptions` does too.** Same bug, found while fixing this one: it also builds an `App` around the *shared* session, so disposing it killed the app and disposed the session the original `App` was still using.
  - `app.Force` views keep their existing early return.
- **What dispose does for a non-owning `App`:** it releases its own held input and stops there. No kill, no Event Log artifact, no session dispose.
- **Explicit kills are unchanged:** `Kill()`/`KillAsync()` on any `App` still kill, since that's a deliberate request rather than cleanup.
- **Tests:** `App.CreateForTesting` takes `ownsProcess`. Two `AppTests` cover a non-owning `App` leaving a live process running, and a `WithOptions` view leaving the process to its owner, whose own dispose still kills it.
- **Live check:** opened Notepad's Open dialog, disposed the dialog `App` with `await using`; Notepad stayed running, and the main `App` still found its Document.
- **No existing test changes needed:** none of the existing integration tests disposed their dialog `App`s, consistent with people working around this.

- [x] **Implement, build and test**

### Task 2.9.2: Window views — `App.WaitForWindowAsync`

**What:** `App.WaitForWindowAsync(WindowMatchOptions match, CancellationToken ct = default)` polls for a visible top-level window of the app's processes matching `match` (`TitleContains`, `ClassName`, `RequireNewWindow`; `ExpectedPid` defaults to the app's process). It returns an `App` *view* rooted at that window. The view:
- shares the session's process, `AppOptions`, logger, held-key/held-button trackers and `Force` handling, built like `app.Force`'s `forcedFrom` view;
- has a no-op `DisposeAsync`;
- has an `AppSession` view with the same process and a different `RootWindowHandle`. Every provider already reads `session.RootWindowHandle`, so finds, waits, snapshots, XPath and `Expect` inside the view are scoped to that window with no provider changes.

**Open questions for implementation:**
- The timeout error: `ElementNotFoundError` with a window locator via the resolver's polling, or a new window-not-found error.
- Whether a view of a view and `view.Force` both just compose.
- Whether `RequireNewWindow` needs a baseline taken at the call (e.g. "the dialog this click is about to open").

**Creates:** `App.WaitForWindowAsync`; an `AppSession` view constructor; unit tests with a fake session.

**Done when:** a unit test proves a view searches its own root and disposing it leaves the process alone. The live check is Task 2.9.8.

As built:
- **Views are one mechanism.** `App`'s private constructor takes `viewOf` (and `force`) in place of `forcedFrom`. A view shares its parent's held-input trackers, has `_isView` set (so `DisposeAsync` is a no-op), and never owns the process. `app.Force` and window views are both built this way.
- **The view's session:** `AppSession.WithRootWindow(hwnd)` returns the same session (same process and `SessionId`) with a different `RootWindowHandle`. No provider changed.
- **The wait goes through the resolver.** `WaitForWindowAsync` polls `PollOutsideChainAsync`, wrapping candidate windows through `Win32Provider`. A timeout raises `ElementNotFoundError` with `tried: window` and a `Name=`/`ClassName=` locator built from the criteria. That settles the first open question.
- **Matching reuses the launch-time rules.** `AppSession.WindowSatisfiesCriteria` (now internal) runs over the visible top-level windows of `ExpectedPid` (default: this app's process).
- **`RequireNewWindow` means "not this `App`'s own root window".** The first version excluded windows already open when `WaitForWindowAsync` was called. That's wrong for the natural flow (click, *then* wait): the dialog already exists by the time the wait starts, and was excluded. The message-box check in Task 2.9.4 caught it.
- **Views compose:** `view.Force` and `view.WithOptions(...)` work (the latter used live with a `win32` chain inside the Open dialog). A view of a view would compose the same way, though no test does that yet.
- **Not unit-tested:** a view needs a real top-level window of the target process, so the "searches its own root" and "dispose leaves the process" checks were done live (Task 2.9.4 below) rather than as unit tests.

### Task 2.9.3: Per-element window checks

**What:**
- `EnsureWindowNotMinimized` checks the target element's own top-level window (or the view's root), not the session root, so acting on an element held from another window works.
- `Win32Provider` records the owning process of each window handle when it finds one. `ValidScopeHandle` raises stale when that owner *changes* (window destroyed, handle reused), instead of requiring the session's process, so a scope in a helper process's window is accepted.

**Done when:** unit tests cover both; the existing integration suites still pass.

As built:
- **`Win32Provider` records the owner.** `WrapWindowHandle` now sets each handle's `ProcessId` from `GetWindowThreadProcessId`. `ValidScopeHandle` raises stale when the window's current owner differs from the recorded one, falling back to the session's process for a handle that didn't record one, as before. Two new `ScopeResolutionTests` cover it: a window still owned by its recorded process in another process is accepted, and a recorded owner that no longer matches is stale. The existing tests (no recorded owner, so session-process rule) pass unchanged.
- **`EnsureWindowNotMinimized` is unchanged, on purpose.** Every `App` method that calls it finds its element through the `App`'s own root, which for a view *is* the view's window. A handle from another window is only acted on through the static actions (`ClickAction.ExecuteAsync(handle)`) or a `Popup`, and neither calls it. So the root check never misfires in practice.

### Task 2.9.4: Dialogs, and `FindDialogAsync`'s fate

**What:** Drive three dialog kinds through window views and note anything extra they need:
- a classic common dialog (Notepad's Save As: `#32770`, an owned top-level window);
- a message box;
- an in-window XAML `ContentDialog`. That one has no new window at all, so it should already work from the main `App`.

Points to check: modality (the owner is disabled while the dialog is up), foregrounding, and whether UIA already nests the owned dialog under its owner.

Then decide:
- **Deprecate:** `FindDialogAsync` becomes `[Obsolete]`, pointing to `WaitForWindowAsync`, if views cover dialogs as they are.
- **Keep as a thin wrapper:** if dialogs need something views don't do. Record what it is.

**Done when:** the decision and its evidence are recorded here.

As built, and the decision:

**Notepad's Open dialog** (`#32770`, owned top-level window, same process) worked through a view end to end:
- the view's root is the dialog, and its snapshot root reads `#32770 'Open'`;
- `FindAsync` by HelpText worked, and a `win32`-chain `WithOptions` sibling found the dialog's `Edit`;
- `TypeAsync` / `GetValueAsync` round-tripped, and `PressKeyAsync(Escape)` closed the dialog;
- disposing the view left Notepad running, and the main `App` kept working.

The main `App` could *also* see into the dialog, since UIA nests owned windows under their owner. A view is still what keeps finds scoped to the dialog.

**A WinForms message box** (`#32770`, title "MenuHost message", opened from a menu command) worked through a view with `RequireNewWindow` (needed because the main window's title, "MenuHost", is a substring): the view read the message text and clicked OK, and the app stayed alive.

**An in-window XAML `ContentDialog`** has no window of its own, so the main `App` already drives it (the existing occlusion tests do).

None needed anything views don't do. Modality and foregrounding just worked, since the view's actions foreground the dialog (its root).

**Decision:** both `App.FindDialogAsync` overloads were removed outright, not marked `[Obsolete]`: the repo is too early to carry deprecation shims. `AppSession.FindDialogAsync` (its only implementation) and its private `FindWindow` helper went with them, along with the three `AppSessionTests` argument-validation tests. Every caller migrated: three Notepad integration tests, two Paint helpers, the Paint save test, and the VS Code sample's Save As.
- **The optional "Replace?" confirmation** in `PaintArtTestBase` relied on `FindDialogAsync` returning `null` on timeout. It now waits through a short-timeout `WithOptions` view and catches `ElementNotFoundError`.
- **`WINDOWS_TESTING.md`'s "Native dialogs" section** now documents window views.

Verified: the full engine integration suite (99/99), the Notepad sample project (54/54), and the VS Code sample's `HelloWorldScriptDemoTests`.

### Task 2.9.5: The general popup pattern — `Popup`

**What:** Generalize Stage 2.3's detection into `PopupAction`:
- **Baseline:** what's showing in the target's window, and the visible top-level windows of the target's processes.
- **Trigger, then a detection pass:** a new showing element in the window, or a new top-level window that a *recognizer* accepts. Every pass goes through the resolver's polling, keeping its wait errors.
- **Recognizers are small predicates over a candidate:** a menu has `MenuItem`s; a dropdown has a `List` or `ListItem`s; a flyout or tooltip is any new showing surface.

**The public `Popup` handle:**
- created by `App.OpenPopupAsync(Locator target, PopupTrigger trigger = …, …)`; the recognizer parameter shape is decided here;
- offers `FindAsync`/`FindAllAsync(Locator)` scoped to the popup, through its own provider like `ContextMenu`'s items;
- acts on elements through the normal actions (or `Popup`-level shortcuts, decided here);
- has `CloseAsync` (Escape; a no-op when already closed) and `WaitUntilClosedAsync`;
- raises `StaleElementError` once the popup has closed.

**`PopupTrigger`** generalizes `ContextMenuTrigger`: right-click, left-click, Shift+F10, hover (tooltips), Alt+Down (combo dropdowns). `ContextMenuTrigger` isn't shipped yet, so renaming or aliasing it is free.

**Done when:** engine builds and unit tests pass. Live checks are in Tasks 2.9.6 and 2.9.7.

As built:
- **`ContextMenuAction` became `PopupAction`** (`Actions/PopupAction.cs`). The recognizer is a single optional **`Locator? expect`**: *the popup is the new surface that is, or contains, an element matching `expect`*. That settles the open question.
- **Why not presets:** the first version had a `PopupKind` enum (`Any`/`Menu`/`List`/`ToolTip`) with per-kind rules (an in-window control type, a required item type, "native allowed"). A review replaced it before anything depended on it, because those rules were just a fixed set of `expect` values, and the native path was a menu-only special case. "Menu" is no longer a concept detection knows about. `ContextMenu` is `expect: ControlType "MenuItem"` plus item helpers, and anything an app exposes through UIA (or `Win32Provider`) can be named directly (`ByAutomationId("CalendarView")`, `ByName("Rename")`, `ByControlType("DataItem")`).
- **One pipeline, two steps, the same question in both:**
  1. **In-window:** new, showing matches of `expect` inside the target's window (not on screen in the before-snapshot, then re-checked live). The popup is the match's **parent**, asked of the provider that found it through the new `IElementProvider.FindParentAsync`: a `MenuItem`'s menu, a `ListItem`'s list. This is a heuristic; an item nested under a group would make the group the popup.
  2. **New top-level windows:** a window counts if UIA3 sees that it *is* (`MatchesItself`, on the handle's own `ControlType`/`Name`/`AutomationId`/`ClassName`) or *contains* an `expect` match. Failing UIA3, it counts if **`Win32Provider`** finds `expect` under it. That's how a UIA-invisible native `#32768` menu is reached: `Win32Provider` already returns its `HMENU` items for `ControlType "MenuItem"`. The native path is a provider fallback, not a menu special case.
- **Parent lookup is a provider capability.** `IElementProvider` gained `FindParentAsync(ElementHandle element, AppSession session, CancellationToken ct = default)`, returning null at the top of the provider's tree or for another provider's handle, per the never-throw rule:
  - `Uia3Provider` and `Uia2Provider`: the control-view parent;
  - `Win32Provider`: the parent window via `GetAncestor(GA_PARENT)` (new `NativeMethods.GetParentWindow`), or, for a native menu item, its `#32768` popup.

  It replaced a UIA3-only `TryGetParent` helper, so every piece of popup detection is now a general capability: a provider's scoped search, its parent lookup, `IsShowing`, window enumeration, the resolver's wait. The four hand-written test fake providers return null. `Providers/FindParentTests.cs` covers the null cases for all three providers and the native-item-to-popup case.

  **Not yet exercised live:** the in-window step's use of the parent. Every popup in the test apps, including the WinForms `ContextMenuStrip` (which the earlier control-type-based version caught in-window), is now found by the window step, because its window also contains the `MenuItem`s. Task 2.9.7 needs a popup that lives entirely inside the app's window. If no real app needs the in-window step, it should be reconsidered.
- **Without `expect`:**
  - the in-window step looks for newly showing elements of a small built-in list of popup-shaped control types (`Menu`, `List`, `ToolTip`, `Window`), and the popup is the element itself;
  - the window step takes the first new, showing window that isn't a `SysShadow` drop shadow.
  - That built-in list is the only one left, and only as the default's guess.
- **`PopupTrigger` replaced `ContextMenuTrigger`:** `RightClick`, `LeftClick`, `ShiftF10`, `Hover` (`HoverAction`) and `AltDown` (focus, then Alt+Down).
- **`Popup` (`Core/Popup.cs`) is the public handle:**
  - `FindAsync` (waits through the resolver), `FindAllAsync` (one pass) and `ClickAsync` (the retry loop formerly in `ContextMenu`), all searching only through the surface's own `Provider`;
  - `IsOpenAsync`, `CloseAsync` (Escape, a no-op when already closed) and `WaitUntilClosedAsync`;
  - a closed popup raises `StaleElementError` ("The {label} has closed; open it again from its target.");
  - its internal `ReadAsync`/`WaitForAsync`/`ClickAsync(search, filter, reportAs)` are what `ContextMenu` reuses.
- **`App.OpenPopupAsync(target, trigger = LeftClick, expect = null)`** and `OpenContextMenuAsync` (`expect: ContextMenu.Items`) share one private `OpenPopupCoreAsync`, labelled "popup" or "context menu" for errors and logs. No popup raises `ElementNotFoundError (tried: popup detection)`.
- **Known limit:** a tooltip ignores Escape (it closes when the pointer leaves), so `CloseAsync` can't close one. The documented pattern is to hover elsewhere, then `WaitUntilClosedAsync`.
- **Live** (WPF test app): a "⋯" button's menu opened by `LeftClick` with no `expect`, with `expect: ControlType "MenuItem"` and with `expect: Name "Bravo"`, each with find, click (delivered its command) and close. A label's tooltip opened by `Hover`, with `expect: ControlType "ToolTip"` and with no `expect`, each with its text read and a wait for it to close. The no-popup timeout reads `ElementNotFoundError (tried: popup detection)`. `Win32Provider` was confirmed to list the native menu's three items under a `#32768`, which is what the fallback asks. Detection itself still went through UIA3 there, since UIA3 can see that menu; the fallback route needs a menu UIA3 can't see, and none has been found yet.

**Follow-up: `Popup` became a view of `App`.** A review found `Popup` growing into a second copy of `App`'s API (only finds and click were there, with no hover, type, scroll, waits or `Expect`). The difference is only where searches start, so a popup is now a view the same way a window view is:
- **The root lives in the session, not the resolver.** `AppSession.WithRootElement(surface, label)` sets `RootElement`, and every provider searches from it (`Uia3Provider`/`Uia2Provider` through `TryGetRoot`, `Win32Provider` by handing it to its scoped search, which reads a `#32768`'s items). `RootWindowHandle` stays the app's window. A first version put an ambient `Scope` on `ElementResolver` instead; it was reverted because it brought back the runtime scoped/unscoped branch Task 2.1.7 had designed out, and left the resolver with search state.
- **`Popup : App`.** It keeps only `IsOpenAsync`, `CloseAsync` and `WaitUntilClosedAsync`; everything else is inherited, including `Force`, which keeps the root because it reuses the session.
- **A popup view searches through its root's provider alone**, so a `#32768` surface works even when `win32` isn't in the configured chain.
- **"Has closed":** providers call `AppSession.ThrowIfRootClosed()` before searching from the root, raising the same `StaleElementError` as before. `WaitUntilGoneAsync` treats it as gone, and the stale-scope retry doesn't retry it.
- **Inside a popup view:** raw input doesn't foreground the app's window (that would dismiss the popup); `CloseWindowAsync`, `MoveWindowAsync`, `ResizeWindowAsync` and `SetWindowStateAsync` throw, pointing at `CloseAsync`. A window view opened from a popup drops the popup root.
- **Behaviour that follows from the root:** the closest-match hint, Spatial locators, snapshots and XPath/path locators all work from the popup's tree; a path's first segment must match the surface. Errors name the bare provider (`uia3`), not `uia3 context menu`; the label stays in the "has closed" message.
- **Not yet exercised live:** the `Uia2Provider` and `Win32Provider` root paths (Notepad's menu covers `Uia3Provider`), raw-input menu navigation, and submenus opened from a popup.

### Task 2.9.6: `ContextMenu` on `Popup`

**What:** `ContextMenu` becomes a specialization of `Popup`: `expect: ControlType "MenuItem"`, `MenuItem` listing and naming, and the native `#32768` path through `Win32Provider`. Its public API is unchanged. Regression: the Notepad `ContextMenuDemoTests` sample plus the Stage 2.3 live checks (the native, WinForms and WPF test apps, and the native path driven directly). Task 2.3.4's integration tests come after this stage and target the final shape.

**Done when:** the `ContextMenuDemoTests` sample and the Stage 2.3 live checks pass unchanged.

As built:
- **`ContextMenu` is now a thin wrapper over a `Popup`** opened with `expect: ContextMenu.Items` (`ControlType "MenuItem"`) and labelled "context menu":
  - `GetItemsAsync` reads `MenuItem`s;
  - `ClickItemAsync` clicks by exact name over the `MenuItem` list (not a `ByName` search, because a WPF `MenuItem`'s child `Text` has the same name and `Win32Provider`'s `Name` match is a substring);
  - `CloseAsync` delegates.
- **Public surface unchanged except the trigger:** `OpenContextMenuAsync` and `ClickContextMenuItemAsync` take `PopupTrigger` instead of `ContextMenuTrigger` (renamed, not yet shipped). Error messages keep `tried: uia3 context menu` / `win32 context menu`.
- **Regression:** the native, WinForms and WPF test apps through the public API (list, missing item, click, close no-op, stale after close, shortcut), the native path driven directly through a `Win32Provider`-wrapped `#32768` inside a `Popup`, and the Notepad sample project (54/54, including `ContextMenuDemoTests`).

**Follow-up: `ContextMenu` collapsed into `Popup`.** A later review removed the wrapper, following Playwright's model of one scoped-locator handle with no menu-specific type:
- `ContextMenu` and `ContextMenuItem` are gone. `OpenContextMenuAsync` now returns the `Popup` itself: `OpenPopupAsync` with `expect: ControlType "MenuItem"`, a right-click by default, and the "context menu" label.
- Listing items is `popup.FindAllAsync(Locator.ByControlType("MenuItem"))`, which returns full `ElementHandle`s (`Name`, `IsEnabled` and the rest) instead of snapshot records.
- `ClickContextMenuItemAsync` keeps the exact-name match, now inline through `Popup`'s internal `ClickAsync(search, filter, reportAs)` (since Task 2.9.5's view follow-up, `App`'s private `ClickFirstWhereAsync`). A plain `popup.ClickAsync(Locator.ByName(...))` still has `Win32Provider`'s substring `Name` match on a native `#32768`.

Stage 2.3's notes above predate this stage. Where they say `ContextMenuAction`/`ContextMenuTrigger`, read `PopupAction`/`PopupTrigger`; where they say `ContextMenu`, read `Popup`.

### Task 2.9.7: More popup kinds

**What:** Through the generic `Popup` handle, on the Task 2.3.4 test apps plus a `ComboBox` added to them:
- a WinForms/WPF `ComboBox` dropdown (Alt+Down, and a click on the arrow): select an item and assert the combo's value;
- a tooltip (hover): read its text, then wait for it to close;
- a flyout from a "⋯" button (left-click): act on an element inside it.

**Done when:** all three pass live.

As built (`Integration/ContextMenu/PopupKindTests.cs`, on the out-of-repo test apps, which gained a fruit ComboBox and a label with a tooltip each, plus a WPF Filter button opening a flyout with a TextBox and an Apply button):
- **ComboBox dropdowns,** by Alt+Down and by a click, on both apps, with `expect: ListItem`. Both apps write the selection to their status label, which is what the tests check: a WPF ComboBox exposes no value through UIA (no `ValuePattern`, empty Name). The WPF dropdown is its own `Popup` window, and a clicked item is selected. The WinForms dropdown is its `ComboLBox` list window, but **UIA Invoke on its items reports success and does nothing**. `ClickAction` prefers Invoke, so the click is a silent no-op. Decided: no engine change; the test picks the item with the keyboard (first letter, then Enter), and `WINDOWS_TESTING.md` documents it.
- **Tooltips,** by hover, on both apps, with and without `expect: ToolTip`: the text is read from the popup's snapshot, then the pointer moves to the status label and `WaitUntilClosedAsync` sees it close.
- **The flyout** (WPF `Popup`, `expect: Name "Apply"`): text typed into its TextBox and a click on Apply reach the app, and the flyout closes.
- **Engine fix: detection now tries new windows first, and the in-window step rejects a surface that is the target or one of its ancestors.** The in-window step was wrong both times it fired live. The WPF ComboBox's items are nested in UIA under the ComboBox, so the "popup" was the ComboBox itself, which never closes. The WinForms tooltip is nested under the app window, so the "popup" was the whole main window. Running the window step first wasn't enough on its own: on a WPF ComboBox's first opening, its items appear in UIA before its window shows, so the in-window step still won that race. A popup never contains what opened it, so a parent that is the target or its ancestor now means "not a popup surface; keep polling". The walk compares Ids through the target's provider, so it only rejects when the target came from UIA3, like the candidates.
- **The in-window step is still not exercised live:** every popup in the test apps, Notepad and Calculator has its own window. The step stays, as the fallback for a popup with no window of its own (decided over removing it).

### Task 2.9.8: Window views live, plus samples

**What:** Integration tests:
- Notepad's Save As (or the dialog Task 2.9.4 settles on), driven through a window view;
- an element held from that dialog, clicked from the main `App` (Task 2.9.3);
- disposing a view leaves Notepad running (Task 2.9.1);
- a secondary window on a test app.

Plus `samples/ConsumerNotepadTests` demos for a window view and a popup, alongside the existing `ContextMenuDemoTests`.

**Done when:** all pass; Stage 2.9 complete.

As built:
- **`Integration/Notepad/WindowViewIntegrationTests.cs`**, on the Open dialog (Task 2.9.4's choice over Save As):
  - a view rooted at the dialog: its snapshot root is the `#32770`, Notepad's Document is out of its reach, typing into the file-name box round-trips, and Escape through the view closes the dialog;
  - a Cancel button found through the view and clicked with the static `ClickAction.ExecuteAsync` closes the dialog, and the main `App` carries on;
  - disposing a view leaves Notepad running and the main `App` working.
- **`Integration/ContextMenu/SecondaryWindowViewTests.cs`:** the test app's secondary window, picked out by `RequireNewWindow` (both titles contain "MenuHost"). The view can't see the main window's controls, and a context menu opened through it delivers its command.
- **Samples:** `WindowViewDemoTests` (the Open dialog through a view, and `await using` a view leaving Notepad running) and `PopupDemoTests` (Notepad's File menu through `OpenPopupAsync`), both listed in the sample README with `ContextMenuDemoTests`.
- **A latent sample bug fixed:** `WindowCloseDemoTests` closes "its own" Notepad, but Notepad is single-instance, so that closed the window the shared `NotepadFixture` drives. It had only ever run last. `WindowViewDemoTests` sorts after it and failed with `WindowActivationError`. It now has its own non-parallel collection, which xunit never runs alongside the shared one.
- **Engine: a "stable" check before synthesized pointer input.** The WPF ComboBox test was occasionally flaky in full-suite runs: on a dropdown's first opening, its items slide about 25px into place over about 50ms, and a click in that window missed the item and closed the dropdown without selecting. As in Playwright, `ElementInputHelpers.GetCenter` (behind every synthesized click, hover, double-click, drag start and scroll) now reads the element's bounds twice, one frame (16ms) apart, and throws `ElementNotInteractableError` ("still moving") if they differ. The resolver's actionability wait retries that like any other not-interactable failure; `app.Force` skips it. Native-pattern clicks don't go through `GetCenter`, so they don't pay for it. `IsUnobscuredAsync` reports a still-moving element as not unobscured.
- **Verified:** the full engine integration suite (146/146), the Notepad sample (58/58) and the VS Code sample (4/4) after the stable check, and the popup tests three runs in a row.

**Follow-up: wider popup coverage.** Added after Stage 2.9 to close the gaps a coverage review listed:
- **Test apps:** a WinForms message box, driven through a window view (`TestAppWindowViewTests`, alongside the secondary window). Submenus in both apps, each opened as a popup from inside the menu popup: the WPF one by a click, the native one by a hover, since UIA Invoke on a native submenu parent reports success without opening anything (the same silent no-op as the WinForms combo's items).
- **WinUI3, beyond menus:**
  - Notepad's Settings tooltip;
  - Calculator's unit ComboBox dropdown (a `Xaml_WindowedPopupClass` window) and its date picker's calendar flyout. Calculator relaunches in its last mode, so those tests switch back to Standard, and they wait for the navigation pane to close before clicking, since it takes clicks while sliding shut;
  - Paint's Rotate dropdown, and its "Resize and skew" dialog: an in-window XAML dialog with no window of its own, **the first live exercise of the in-window detection step**.
- **Electron (VS Code sample, `PopupDemoTests`):** the editor context menu, the title-bar File menu, the command center's quick pick and an activity-bar hover. All are drawn inside VS Code's window, so all are found through the in-window step.
- **Engine fixes:**
  - **`IsInVisibleWindow` checks the top-level window.** WinUI3 hosts content under a zero-size `InputSiteWindowClass` child that is never visible, so every element of Paint's dialog read as hidden and the in-window step never saw it. The check exists for a hidden WinForms `ContextMenuStrip` dropdown, which is itself a top-level window, so checking the top-level window keeps that case.
  - **The in-window step takes a popup-shaped match as the popup itself.** With `expect: ToolTip`, a VS Code hover's parent was a workbench container that also holds the hovered button, so the 2.9.7 ancestor check rejected it. A match of one of the popup-shaped control types (`Menu`, `List`, `ToolTip`, `Window`) is now the popup, as a window that matches `expect` itself is in the window step; anything else still steps up to its parent.
- **Findings, documented in `WINDOWS_TESTING.md` rather than fixed:**
  - **VS Code's title-bar menus misreport their items.** Chromium reports every item after the first as `IsOffscreen=true` while they're on screen. That was checked with raw UIA reads at +0s, +1s and +3s, and after hovering and arrowing through the menu. The editor context menu and the quick pick report correctly. Decided: the tests go through `Force`, and a submenu from those menus is a known limit, since detection skips offscreen candidates.
  - **VS Code drops a click sent in a menu's first moments,** about one run in three, via UIA Invoke or a real mouse click alike. Nothing in UI Automation (focus included) changes when it starts accepting clicks, so the sample waits 300ms before clicking inside a fresh menu.
  - **Paint's "Edit colors" dialog reports an endless list.** It first looked like a Paint freeze, but a step-by-step UIA walk found the cause: its "Custom colors" grid reports hundreds of thousands of "Empty" items (over 308,000 in 150s, without reaching the end), each answered instantly. Any search that must get past the grid times out with `0x80131505` and keeps Paint busy serving it, including a `FindAsync` for Cancel (after the grid in tree order) and popup detection's whole-window searches. A search that stops before the grid is instant, which is why it can't be reproduced by hand. Earlier runs that seemed to separate opening by Invoke from opening by mouse were an artifact: the mouse clicks had missed the button, so the dialog never opened. The engine part is only that a detection pass doesn't check the deadline between its UIA calls, which is why `OpenPopupAsync` took 176s to give up there. `PaintEditColorsDemoTests` (Notepad sample, in its own collection, launching its own Paint) opens the dialog, checks it through the color spectrum (before the grid) and closes it with Escape: 3 of 3 runs, about 1s each.
- **Still flaky:** the VS Code quick-pick test, about one run in five. When it fails, the click on "Open Quick Access" is dropped; it's the first action in a freshly prepared VS Code. The Calculator unit-combo test failed once in a full-suite run and never in isolation.

---

## Future Work: Visual Provider (OCR and Template Matching)

> **Unscheduled — not part of Phase 2.** These tasks were Stage 2.6 Tasks 2.6.1–2.6.4 before that stage was narrowed to idle wait and visual regression (see roadmap-spec.md "Future Work Ideas"). Kept here as the ready-made task breakdown if the Visual Provider is picked up; the checkboxes and `Task VP.n` numbering are outside the Phase 2 sequence, and nothing in v1 depends on them. Task VP.4 is the integration step that also needs a no-UIA test app, which does not exist yet.

---

### Task VP.1: `VisualProvider` skeleton

**What:** Implements `IElementProvider` (find-only, matching every other provider's contract). Screenshots the target window via the existing `ScreenshotAction`'s GDI+ `BitBlt` path — no separate capture code — then hands the bitmap to whichever strategy branch (OCR or template) the incoming `Locator.Strategy` selects. Returns `null` on not-found, same as every other provider; never throws.

**Creates:**
- `src/AutoMancer.Engine/Providers/VisualProvider.cs` — `FindElementAsync`/`FindElementsAsync`/`SnapshotTreeAsync` (snapshot returns an empty tree — there's no accessibility tree to walk, only ad hoc find-by-text/find-by-image); `ProviderName => "visual"`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors; `VisualProvider` compiles against `IElementProvider` with no strategy branches wired yet (Tasks VP.2–VP.3 add them).

---

### Task VP.2: OCR path — `automancer:text` strategy

**What:** `Windows.Media.Ocr.OcrEngine` (on-device, no external service, no extra package — available via the `windows10.x` TFM suffix per CLAUDE.md). Adds `automancer:text` as a locator value the `VisualProvider` recognizes: runs OCR against the captured bitmap, matches recognized word/line bounding boxes against the locator's text (exact or substring — substring by default, matching how `ToHaveText` already works elsewhere), and returns a synthetic `ElementHandle` whose `BoundingRect` is the matched text's screen rect and whose `NativeHandle` is the OCR result (opaque, per the `ElementHandle` contract).

**Creates:**
- `src/AutoMancer.Engine/Core/LocatorStrategy.cs` — add `AutoMancerText` (or reuse `Property`'s envelope pattern — pick whichever keeps `VisualProvider`'s dispatch simplest)
- `src/AutoMancer.Engine/Core/Locator.cs` — add `Locator ByText(string text)` (the `app.find(text: ...)` surface referenced in the roadmap is the SDK-layer shorthand for this in Phase 3; the engine-level entry point is this factory)
- `src/AutoMancer.Engine/Providers/VisualProvider.cs` — OCR branch using `OcrEngine.TryCreateFromUserProfileLanguages()` → `RecognizeAsync`

- [ ] **Implement and build**

```bash
dotnet build src/AutoMancer.Engine/AutoMancer.Engine.csproj
```

**Done when:** Engine builds with 0 errors. Live behavior verified in Task VP.4.

---

### Task VP.3: Template matching — `automancer:image` strategy

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

### Task VP.4: Wire `visual` into the default provider chain

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

## Constraints

- Every `.cs` file starts with `// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.`
- `net10.0-windows10.0.22621.0` only — no cross-platform guards
- `System.Text.Json` throughout — no Newtonsoft.Json
- No DI container — new classes wired manually at call sites, same as Phase 1
- `ElementHandle` stays opaque throughout this phase — only `Id` (string) and `NativeHandle` (object) are public. Every new action/provider that needs native pattern access goes through the existing `Operator`/`NativeHandle` seam, never a new public field
- Every new UIA-pattern action (`ToggleAction`, `ExpandCollapseAction`, `SelectionAction`, `GridAction`, `RangeValueAction`) follows `ScrollAction`/`SetFocusAction`'s established shape: no-op silently when the handle isn't a UIA element or the pattern isn't supported, never throw for "pattern not available"
- Nothing in this phase modifies an existing Phase 1 public method's signature — every task adds new surface, it doesn't change what's already shipped
- Nothing in this phase requires `AutoMancer.Daemon` to exist — Phase 3 wraps whatever this phase produces, not the other way around

---

## Quick-Reference: Definition of Done

Mirrors roadmap-spec.md's per-phase checklist; see there for the authoritative, currently-tracked version. Duplicated here per-task for convenience while working through this document:

- [x] Task 2.1.6 — spatial and property locators find elements the fixed strategy set can't reach
- [x] Task 2.1.9 — scoped find disambiguates identically-matched elements, and virtualized-list find locates unrealized items
- [x] Task 2.2.6 — `Expect()` and `WaitForAsync` share a wait-condition vocabulary
- [ ] Task 2.3.4 — native context menu fallback works when UIA can't see the popup
- [x] Task 2.4.2 — resource watch returns a plausible sample series against a live app
- [ ] Task 2.5.3 — accessibility audit reports a deliberately-unnamed control
- [ ] Task 2.6.2 — visual regression assertion fails on a drifted baseline
- [ ] Task 2.7.2–2.7.3 — DPI and Windows 10/11 compat confirmed
- [ ] Task 2.7.4 — all three example projects build and run
- [ ] Task 2.8.6 — toggle/select/grid/clipboard all verified against a live app
- [ ] Task 2.8.8 — a slider/progress bar's value round-trips through `RangeValueAction`
- [ ] Task 2.9.8 — window views drive a dialog without a separate `App`, and popups of several kinds open, act and close through one `Popup` handle (runs before Task 2.3.4)
