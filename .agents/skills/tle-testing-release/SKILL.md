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

## Release Readiness

Before public packaging, verify compatibility with the current Cities: Skylines II version, run a release build, check local mod install output, and review README/GUIDE/ROADMAP for stale claims. The repository is currently treated as source-built local mod work, not public Paradox Mods distribution.
