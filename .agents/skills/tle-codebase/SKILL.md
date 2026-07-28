---
name: tle-codebase
description: "Use when starting work in the TLE Extended repository, orienting to Cities: Skylines II Traffic Lights Enhancement code, choosing which project area owns a behavior, or deciding which repo docs to read before editing."
---

# TLE Codebase

## Overview

TLE Extended is a compatible extended fork of Traffic Lights Enhancement for Cities: Skylines II. It preserves upstream TLE save/intersection compatibility while adding Transit Signal Priority, diagnostics, documentation, and maintainability work.

## First Pass

Read the smallest useful set before editing:

- `README.md`: project status, compatibility stance, and user-facing capabilities.
- `BUILD.md`: local mod build expectations and required tools.
- `GUIDE.md`: player-facing behavior, options, TSP diagnostics labels, and current limitations.
- `ROADMAP.md`: near-term direction and known non-goals.
- `docs/tsp-architecture.md`: TSP ownership map and data flow.
- `docs/save-format-contract.md`: serialized payloads, versions, migrations, and compatibility rules.
- `docs/localization-workflow.md`: localization source of truth and key rules.

## Repository Map

| Area | Path | Notes |
| --- | --- | --- |
| Main mod | `TrafficLightsEnhancement/` | net48 Cities II mod assembly. Assembly/root namespace remain `C2VM.TrafficLightsEnhancement` for compatibility. |
| Pure logic | `TrafficLightsEnhancement.Logic/` | netstandard2.0 C# policy layer with no Unity dependencies. Prefer changes here for testable TSP decisions. |
| In-game UI | `TrafficLightsEnhancement/UI/` | React/TypeScript UI built by webpack and installed during mod build. |
| Shared lane system | `CommonLibraries/LaneSystem/` | Repository-owned copy of the required Lane System sources. Preserve its assembly, namespaces, component types, and serialization schemas. |
| Unit tests | `TrafficLightsEnhancement.Tests/` | Pure logic and compatibility tests. |
| ECS tests | `TrafficLightsEnhancement.Ecs.Tests/` | Runtime/ECS-oriented tests and regression coverage. |
| Serialization tests | `TrafficLightsEnhancement.Serialization.Tests/` | Save format, migration, and component serialization coverage. |
| Docs | `docs/` | Maintainer contracts and architecture notes. Update when behavior or serialized contracts change. |

## Core Invariants

- Treat the fork as a drop-in replacement for upstream Traffic Lights Enhancement while that compatibility goal remains active.
- Do not rename the assembly, root namespace, mod id, or save-facing identifiers casually; compatibility depends on `C2VM.TrafficLightsEnhancement`.
- TSP is additive per intersection. Existing TLE intersections without TSP settings should continue behaving as non-TSP intersections.
- Diagnostics are opt-in. Keep expensive or noisy diagnostics behind explicit settings.
- Signal groups in ECS/game data are 1-based; pure policy code often uses 0-based indexes. Find the bridge before changing group math.
- User-facing behavior changes should normally update `GUIDE.md`; architecture or save changes should update the relevant `docs/` contract.

## Choosing A Sub-Skill

| Task | Use |
| --- | --- |
| C#, Unity ECS, traffic-light systems, TSP policy, serialization | `tle-csharp-ecs` |
| React UI, C# UI bindings, localization keys, option text | `tle-ui-localization` |
| Selecting tests, build commands, packaging/release checks | `tle-testing-release` |
| Reviewing PRs/branches, coordinating reviewer agents, verifying review fixes | `tle-code-review` |

## Working Style

Before implementing a feature, bugfix, or GitHub issue, create or switch to a dedicated task branch and preferably a separate git worktree. Do not edit `main` directly for issue/feature/fix work. If the tree is dirty, preserve the user's work and ask before moving or mixing unrelated changes.

Start with the owning layer, then expand only when the behavior crosses boundaries. For example, a TSP policy tweak usually starts in `TrafficLightsEnhancement.Logic/Tsp`, then updates ECS integration, UI diagnostics, and docs only if the contract changes.
