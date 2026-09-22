# Changelog

All notable changes to this package are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

While the version stays `0.x`, the public API may change in any release.

## [Unreleased]

### Added
- `ActionDefinition` and `ActionDefinitionAsset` in namespace `Agenerela` — the
  developer-authored action vocabulary (#4). `ActionDefinition` is the plain serializable type
  the registry, schema and few-shot builders consume; `ActionDefinitionAsset` is the
  `ScriptableObject` a developer creates from **Create → Agenerela → Action** (DR-011).
  `ActionDefinition.ValidateId` rejects an empty id, uppercase (compared culture-invariantly)
  and any whitespace, and the asset warns about a malformed id while it is being edited.
  EditMode tests for it are still owed by #4.
- `TargetRegistry` in namespace `Agenerela` — what an agent is allowed to refer to, held as
  ids the schema can offer and the guards can check: `Register`, `Unregister`, `TryGet`,
  `Contains`, `Ids` and `Count`, with EditMode tests. Follow-up work — reserving
  `no_target`, safer lookups and the missing test cases — is tracked in #29.
- `ILLMProvider` and its supporting types (`ProviderCapabilities`, `SchemaDialect`,
  `RateLimit`, `DecisionRequest`, `ProviderResult`) in `Agenerela.Providers` — the contract
  every backend satisfies, defined before any of them is written so that Ollama, cloud APIs
  and in-process inference are implementations rather than framework edits. No
  implementation yet; `DecisionRequest.Schema` lands with `DecisionSchema`.
- `Documentation~/providers.md`: how each planned backend answers the contract, what a
  second cloud vendor would need, and which parts are deliberately still open.

### Changed
- The package depends on `com.unity.nuget.newtonsoft-json` 3.2.2, which Package Manager
  installs automatically. The runtime and editor assemblies reference `Newtonsoft.Json.dll`
  explicitly and no other precompiled DLL (DR-009).

## [0.1.0] - 2026-09-03

### Added
- Package skeleton: runtime, editor and editor-test assembly definitions.
- `AgenerelaInfo` with package name and version constants.
- Smoke tests asserting the assemblies resolve and are visible to the test runner.
