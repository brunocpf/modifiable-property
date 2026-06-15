# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
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
