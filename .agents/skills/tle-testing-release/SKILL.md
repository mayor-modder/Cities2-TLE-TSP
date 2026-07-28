---
name: tle-testing-release
description: "Use when choosing verification commands, running tests, preparing builds, checking packaging risk, or deciding release-readiness for TLE Extended Cities: Skylines II mod changes."
---

# TLE Testing Release

## Overview

Choose verification by risk and ownership. Prefer focused tests while developing, then expand to build or packaging checks when a change touches shared behavior, serialization, UI payloads, or release-facing assets.

## Command Map

| Change | Command |
| --- | --- |
| Pure TSP logic | `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj` |
| ECS/runtime regressions | `dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj` |
| Save serialization or migration | `dotnet test TrafficLightsEnhancement.Serialization.Tests/TrafficLightsEnhancement.Serialization.Tests.csproj` |
| UI panel logic | `npm test --prefix TrafficLightsEnhancement/UI` |
| UI bundle | `npm run build --prefix TrafficLightsEnhancement/UI` |
| Full restore | `dotnet restore Cities2-TrafficLightsEnhancement.sln` |
| Full build | `dotnet build Cities2-TrafficLightsEnhancement.sln --configuration Release` |

## Build Notes

- `BUILD.md` is the source of truth for local mod build setup.
- Release builds require Node.js 20, .NET 8 SDK, and the in-game Modding Toolchain.
- The main mod targets `net48` and imports `CSII_TOOLPATH` `Mod.props`/`Mod.targets`.
- `dotnet build --configuration Release` runs the Cities II mod post-processor and copies the mod to the local mod directory when files are not locked by the game.
- If the modding toolchain is unavailable, explain which narrower tests were still run instead of implying full build coverage.

## Verification Selection

- For pure policy changes, run the matching pure test project first.
- For ECS integration, add `TrafficLightsEnhancement.Ecs.Tests`.
- For serialized payloads, run serialization tests and update `docs/save-format-contract.md`.
- For UI text, options, bindings, or diagnostics, run UI tests and inspect `Locale.json` coverage.
- For player-visible behavior, update and sanity-check `GUIDE.md`.
- For broad cross-layer changes, run all three .NET test projects plus UI tests before claiming completion.

## Fresh Checkout Verification

Before declaring a branch or pull request ready, push the final head and verify that exact remote commit from a fresh clone. Run the required restore/build checks there instead of relying only on an existing workspace, cached outputs, nested repositories, or locally available Git objects.

## Installed Build Verification

Before asking the user to playtest:

1. Confirm Cities: Skylines II is not running.
2. Run the normal deploying Release build without `DisablePostProcessors=true`.
3. Read the installed `C2VM.TrafficLightsEnhancement.dll` and confirm its informational version contains the commit used to build it, normally `git rev-parse HEAD`.
4. Hash-match directly copied dependencies such as `C2VM.CommonLibraries.LaneSystem.dll` between the build output and installed mod folder.
5. Confirm the installed `.mjs`, `.css`, native libraries, and other expected package files were produced by the current build.

Do not require the installed main DLL to hash-match the raw `bin` DLL: the Cities II mod post-processor changes the deployed assembly. The installed informational commit version is the authoritative source check for that assembly.

## Playtest Evidence

Record the exact installed commit covered by each gameplay test. Runtime, UI, dependency, build-system, or packaging changes after that commit make the earlier gameplay evidence stale for the affected surface; disclose that immediately, reinstall the new head, and request only the necessary retest. Documentation-only and CI-only commits do not invalidate gameplay evidence, but record that they were not included in the installed assembly.

## Release Readiness

Before public packaging, verify compatibility with the current Cities: Skylines II version, run a release build, check local mod install output, and review README/GUIDE/ROADMAP for stale claims. The repository is currently treated as source-built local mod work, not public Paradox Mods distribution.
