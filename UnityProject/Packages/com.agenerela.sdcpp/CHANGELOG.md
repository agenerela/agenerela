# Changelog

All notable changes to this package are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **The package** (DR-028, #359). `SdCppImageProvider`, an `IImageProvider` and `IModelLifecycle`
  that runs stable-diffusion.cpp in-process on a worker thread: makes pictures, repaints or edits
  them, cancels, and frees the model's memory. `SdCppModel` recipes with a file picker and checked
  downloads, `SdCppSettings`, the pinned release in `SdCppRelease`, and Window → Agenerela → Local
  Image Models to install a native build. The "Paint a portrait" sample.
