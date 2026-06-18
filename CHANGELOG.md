# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
