# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- **Pure core** (`BrunoCPF.Modifiable.Core`): closure-free, non-reactive building blocks that can be
  compared, copied, saved and described.
  - `Modifier` (`Add`, `Multiply`, `Override`, `AtLeast`, `AtMost`, with priority and an optional
    `StackGroup`) and `ModifierStack.Evaluate` / `ModifierStack.Explain`. `StackPolicy` (`Stack`,
    `HighestOnly`, `LowestOnly`) resolves stack groups; `ModifierBreakdown` lists every step,
    including suppressed modifiers.
  - `DeltaFilter` (`Scale`, `Add`, `CapMagnitude`, `Block`, limited by `DeltaDirection`) and
    `DeltaPipeline.Apply`, which returns a `DeltaResult` with the requested, filtered and applied
    amounts and the ids of the filters that fired.
  - `ModifierMath.Apply` and `DeltaPipeline.ApplyFilter` for single operations.
- `IScalableValueMath<T>` (`ToDouble` / `FromDouble`), implemented by the built-in `int`, `long`,
  `float` and `double` math; `ValueMath.GetScalableValueMath<T>()`.
- `RoundingMode` (`TowardZero`, `Floor`, `Ceiling`, `Nearest`) for `IntValueMath` / `LongValueMath`.
  Integral results round toward zero by default and saturate at the type's limits.
- `ModifiableProperty` now accepts data entries: `AddOrUpdateModifier(Modifier)`,
  `PushModifier(Modifier)`, `AddOrUpdateFilter(DeltaFilter)`, `PushFilter(DeltaFilter)`. They share
  one id space and one priority order with function-based entries. New members: `DataModifiers`,
  `Stacking`, `Explain()` and `AppliedDeltas` (a `DeltaResult` per change, including which filters
  fired).

### Changed
- `ModifiableProperty` is rebuilt on the pure core: the base value is stored directly instead of
  folded through a `Scan` pipeline, and filters and modifiers are evaluated in one pass. The public
  API and emission order are unchanged (`Base` updates before `ProcessedDeltas` emits).
- The SDK project pins `LangVersion` 9.0 to match the C# version Unity compiles the shared source
  with.

### Fixed
- Omitted bounds are now unbounded. For value types, `ValueBounds<TValue>` used `default` for a
  missing side and always applied it, so `new ValueBounds<int>()` clamped everything to 0, a property
  created without bounds always read 0, and `min: 1` without `max` clamped to `[1, 0]`.
  - `ValueBounds<TValue>` tracks each side with `HasMin` / `HasMax`. New: `Unbounded`, `AtLeast(min)`,
    `AtMost(max)`. A minimum greater than the maximum now throws `ArgumentException`.
  - `ModifiableProperty`'s `min` / `max` constructor parameters are now `TValue?` (default `null`,
    meaning unbounded). This is source-compatible for callers passing values, but `new(value, null)`
    is now ambiguous; name the argument (`bounds: null`).
  - New `ModifiableProperty(TValue initialValue)` constructor for an unbounded property.
    `new ModifiableProperty<int, object>(100)` previously didn't compile (ambiguous overloads).
  - `ValueBounds<TValue>` is no longer a positional record, so its `Deconstruct` method is gone.
- Modifiers and filters with equal priority are now applied in insertion order. Previously they were
  ordered with the unstable `List<T>.Sort`, so equal-priority, non-commutative entries (e.g. one
  adds, one multiplies) could resolve in a different order than they were added. Entries are now
  inserted at the end of their priority group without re-sorting the whole list. Replacing an entry
  with the same ID keeps its position while its priority is unchanged; removing and re-adding moves
  it to the end of its priority group. `initialModifiers` / `initialFilters` are stably sorted by
  priority as well.

## [0.3.1] - 2026-06-17
### Fixed
- Removal/cleanup on a disposed `ModifiableProperty<TValue, TContext>` no longer throws
  `ObjectDisposedException`. `RemoveModifier`, `RemoveFilter`, `ClearFilters`, and the `IDisposable`
  handles returned by `PushModifier` / `PushFilter` are now no-ops after `Dispose()`. This makes the
  disposable-handle pattern safe during teardown, where the owning property may already have been
  disposed by dependency-disposal ordering (e.g. a DI scope tearing down a stats system before the
  effect that pushed a modifier onto it). Adds/mutations after `Dispose()` still throw — that remains
  genuine misuse.
- `Dispose()` is now idempotent.

## [0.3.0] - 2026-06-17
### Added
- `ModifiableProperty<TValue, TContext>.AsReadOnly()` returns an
  `IReadOnlyModifiableProperty<TValue, TContext>` view that forwards reads (`CurrentValue`, `Base`,
  `ProcessedDeltas`) and the value stream, but exposes no mutating members and **cannot be cast back
  to the writable `ModifiableProperty`**. This gives genuine read-only encapsulation — like R3's
  `ReadOnlyReactiveProperty<T>` — instead of read-only only by interface convention (a consumer could
  previously downcast a property returned as `IReadOnlyModifiableProperty` and mutate it).

## [0.2.0] - 2026-06-15
### Added
- SDK-style .NET class library (`src/BrunoCPF.Modifiable`) targeting `netstandard2.1`, so the
  package can be consumed from Godot 4 (.NET), plain .NET, and NuGet. It compiles the same
  source Unity consumes — no duplicated code.
- Engine-free NUnit test suite (`tests/`) runnable via `dotnet test` with no Unity/Godot.
- Solution file (`BrunoCPF.Modifiable.slnx`).
- GitHub Actions: CI (build + test on push/PR) and tag-driven publishing to NuGet.org via
  Trusted Publishing / OIDC — no stored API key (push a `v*` tag; version derived from the tag).
- NuGet package metadata: README on the package page and shipped XML docs for IntelliSense.

### Changed
- **UPM package moved into the `unity/` subfolder.** Git install URLs now need the `?path=unity`
  suffix (e.g. `...modifiable-property.git?path=unity`).
- **`ProcessedDeltas` and `Modifiers` now return R3 `Observable<T>`** instead of
  `System.IObservable<T>`, for consistency with the rest of the (R3-based) API and direct access
  to R3 operators / `Subscribe(Action)` without a `.ToObservable()` conversion.

### Removed
- Hidden `Tests~` Unity test folder (never compiled by Unity); replaced by the engine-free NUnit suite.

### Fixed
- Guarded the `IsExternalInit` polyfill behind `#if !NET5_0_OR_GREATER` so it can't clash with the
  BCL type if the code is compiled for net5.0+.

## [0.1.0] - 2024-XX-XX
### Added
- Initial release with modifiable properties, filters, modifiers, bounds, and math helpers.
- Basic usage sample and test scaffolding.
