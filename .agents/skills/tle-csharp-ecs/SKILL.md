---
name: tle-csharp-ecs
description: "Use when changing TLE Extended C# code, Unity ECS systems, traffic-light simulation, Transit Signal Priority policy, serialized components, migrations, or Cities: Skylines II mod build behavior."
---

# TLE CSharp ECS

## Overview

Use the narrowest layer that can own the behavior. Keep pure policy in `TrafficLightsEnhancement.Logic`, game-state collection and ECS writes in `TrafficLightsEnhancement`, and serialized compatibility rules documented before changing payloads.

## Layer Boundaries

| Layer | Main paths | Rules |
| --- | --- | --- |
| Pure TSP logic | `TrafficLightsEnhancement.Logic/Tsp` | No Unity dependencies. Prefer deterministic value types and xUnit coverage in `TrafficLightsEnhancement.Tests/Tsp`. |
| Saved ECS data | `TrafficLightsEnhancement/Components` | Preserve field order, versions, normalization, and `ISerializable` reads. Update `docs/save-format-contract.md` when payloads change. |
| Runtime ECS systems | `TrafficLightsEnhancement/Systems` | Gather game state, maintain transient components, and apply decisions to traffic lights. Keep diagnostics gated. |
| Traffic-light simulation | `Systems/TrafficLightSystems/Simulation` | Owns patched normal/custom state machines, approach indexing, TSP request production, and signal selection. |
| Initialization | `Systems/TrafficLightSystems/Initialisation` | Builds lane and phase state for configured intersections. Avoid save-breaking assumptions. |
| Shared lane system | `CommonLibraries/LaneSystem` | Treat as shared infrastructure; check its serialization schema before editing. |

## TSP Contracts

- Read `docs/tsp-architecture.md` before changing request production, latching, source priority, preemption, or diagnostics.
- Track/tram requests outrank bus/public-car requests. Bus priority is intentionally softer and should not gain tram-style aggressive preemption without an explicit design.
- Grouped intersections do not run local TSP, including the leader. Group-wide TSP needs explicit leader/member semantics before implementation.
- Exclusive pedestrian protection is shared policy; do not bypass it from one state machine only.
- `TransitSignalPriorityRequest`, runtime debug info, decision traces, and JSONL diagnostics are transient, not save data.
- Request horizon `120` is a legacy value normalized to `10`; changing this affects saved TSP compatibility.

## Serialization Rules

Before changing any serialized component, buffer, version, enum value, or normalization rule:

1. Read `docs/save-format-contract.md`.
2. Identify whether the change is global migration version, component payload version, or runtime-only.
3. Add or update coverage in `TrafficLightsEnhancement.Serialization.Tests`.
4. Update migration/repair code if older payloads need compatibility.
5. Update the save-format contract in the same change.

## Common Mistakes

- Mixing pure policy and Unity/ECS access. Move policy into `TrafficLightsEnhancement.Logic` when it can be tested without game state.
- Forgetting 1-based ECS signal groups versus 0-based pure phase indexes.
- Letting diagnostics or panel-only scans run when diagnostics are disabled.
- Updating UI-facing diagnostic fields without checking `UISystem.UIBIndings.cs`, React rendering, and Node tests.
- Treating runtime debug components as save data.

## Verification

Use `tle-testing-release` to pick commands. For most C# policy changes, start with focused `dotnet test` on `TrafficLightsEnhancement.Tests`. For ECS or serialization changes, include the matching test project and consider a full solution build when toolchain references are available.
