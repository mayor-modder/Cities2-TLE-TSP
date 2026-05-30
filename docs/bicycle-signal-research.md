# Bicycle Signal Research

This note maps the current bicycle signal behavior so future bicycle-lane work
can start from code evidence instead of rediscovering the inherited system.

## Current Model

`Verified from code`: Bicycle signal authoring is edge-level. The saved
movement mask is `EdgeGroupMask.m_Bicycle`, a `GroupMask.Signal` whose
`m_GoGroupMask` uses the same `1 << phaseIndex` bit contract as car, transit,
track, and pedestrian masks.

`Verified from code`: `SubLaneGroupMask` has no bicycle field. When an edge is
unlinked for per-lane editing, car, track, and pedestrian movements can be
configured per sublane, but bicycle service remains controlled by the
edge-level bicycle mask.

`Verified from code`: Bicycle demand data lives in `CustomPhaseData`:
`m_BicycleLaneOccupied`, `m_BicycleWeight`, and serialized bicycle delay fields.
`m_BicycleWeight` contributes to `WeightedLaneOccupied()`. The
`PrioritiseBicycle` option bit is serialized and tested, but no current runtime
or UI consumer was found.

## Discovery And Generation

`Verified from code`: `NodeUtils` counts bicycle lanes when the node sublane or
resolved source sublane has `Game.Net.SecondaryLane`.

`Verified from code`: `LaneConnectorGenerator` can classify a connection as
`VehicleGroup.Bike` when the road prefab car-lane data includes
`RoadTypes.Bicycle`.

`Verified from code`: `IntelligentPhaseGenerator` writes bicycle go masks for
generated custom phases. Simple one-edge-per-phase layouts serve bicycle lanes
with that edge's phase, split/turn generation serves bicycle lanes with the
straight phase, and connection-based generation sets the bicycle mask when a
connection includes `VehicleGroup.Bike`.

`Needs in-game evidence`: The code treats `SecondaryLane` as the bicycle-lane
proxy in multiple places. That should be tested on bicycle-only, mixed car+bike,
and any secondary non-bicycle layouts we can construct in-game.

## Initialization And Runtime

`Verified from code`: `CustomPhaseProcessor.ProcessLanes(...)` is the bridge
from saved bicycle masks to live `LaneSignal` data. For each connected edge it
uses `EdgeGroupMask.m_Bicycle.m_GoGroupMask` for nearby non-car
`SecondaryLane` sublanes. If the bicycle mask is empty, it falls back to that
edge's straight car go mask.

`Verified from code`: Once initialized, bicycle lanes are ordinary lane
signals. `PatchedTrafficLightSystem.UpdateLaneSignal(...)` applies go, yield,
or stop based on each lane's group masks and the current/next signal group.
Bicycle masks are currently stop/go only; bicycle yield masks exist
structurally on `GroupMask.Signal` but are not authored or consumed as bicycle
yield behavior.

`Verified from code`: `CustomStateMachine.CalculatePriority(...)` increments
`m_BicycleLaneOccupied` for petitioning `SecondaryLane` sublanes. Dynamic mode
then includes `m_BicycleLaneOccupied * m_BicycleWeight` in weighted waiting.
Traffic group leaders aggregate member bicycle occupancy before making their
own phase decision; followers mirror the leader phase.

`Verified from code`: TSP does not create bicycle requests. A TSP-selected bus
or tram phase may still serve bicycle lanes when their lane signal masks
overlap the same group. Grouped intersections reject local TSP before bicycle
or transit details matter.

`Needs in-game evidence`: Vanilla and predefined-pattern intersections do not
use `EdgeGroupMask.m_Bicycle`. Their bicycle behavior depends on the base game
or inherited TLE lane initialization, not on the custom bicycle mask path
described here.

## UI And Binding Surface

`Verified from code`: C# UI bindings send bicycle counts, masks, occupancy,
weights, and delay fields to React.

`Verified from code`: `edge-panel.tsx` builds a `bicycleLane` model and can
write `m_Bicycle.m_GoGroupMask` through `CallUpdateEdgeGroupMaskForJunction`.

`Risk`: `lane.tsx` does not currently render a `bicycleLane` control. It renders
the `all` button only for pedestrian lane types, while bicycle lanes also use
the `all` state. This likely makes bicycle lane toggles invisible in the
custom-phase edge panel.

`Risk`: The traffic group member signal editor can update `m_Bicycle`, but the
rendered signal rows calculate `hasBicycleLanes` without rendering a bicycle
button.

`Risk`: `bicycleLaneOccupied` is in the C# and TypeScript payloads, but the
statistics foldout shows cars, buses, trams, and pedestrians only.

`Risk`: Bicycle delay fields are serialized and some bindings can update them,
but the visible custom-phase delay foldout edits edge-wide open/close delay.
No current runtime path was found that applies movement-specific bicycle
`GroupMask.Signal` delays.

## Likely Bugs

### Bicycle Masks Do Not Move With Phase Reorder/Remove

Tracked as
[`#97`](https://github.com/mayor-modder/Cities2-TrafficLightsEnhancement-Extended/issues/97).

`Verified from code`: `CustomPhaseUtils.SwapBit(ref EdgeGroupMask...)` swaps
car, public-car, track, and pedestrian masks, but it does not swap
`m_Bicycle`. `CallSwapCustomPhase` and `CallRemoveCustomPhase` both rely on
that helper. After a phase is reordered or removed, bicycle service can remain
attached to the old phase bit while other movement masks move.

Suggested fix:

```csharp
SignalSwapBit(ref phase.m_Bicycle, index1, index2);
```

Suggested tests:

- `CustomPhaseUtils.SwapBit(ref EdgeGroupMask...)` swaps bicycle go bits.
- `CallSwapCustomPhase` keeps bicycle masks attached to the intended phase.
- `CallRemoveCustomPhase` shifts bicycle masks down with the remaining phases.

### Bicycle Controls May Be Hidden

Tracked as
[`#98`](https://github.com/mayor-modder/Cities2-TrafficLightsEnhancement-Extended/issues/98).

`Verified from code`: `edge-panel.tsx` creates a bicycle lane column, but
`lane.tsx` has no bicycle rendering path. This should be confirmed with a UI
test before changing behavior.

Suggested tests:

- A `bicycleLane` renders a visible stop/go button.
- Clicking that button toggles `m_Bicycle.m_GoGroupMask`.
- Traffic group member signal editing renders bicycle controls when
  `m_BicycleLaneCount > 0`.

## Mystery-Code Follow-Ups

These do not block documentation, but they are worth tracking if the bicycle
work becomes a full feature pass.

### `PrioritiseBicycle`

`Unknown`: `CustomPhaseData.Options.PrioritiseBicycle` is serialized and covered
by tests, but no non-test consumer was found. Current bicycle priority appears
to flow through `m_BicycleLaneOccupied` and `m_BicycleWeight`.

Issue draft:

```markdown
*Written by Codex.*

## Mystery

`CustomPhaseData.Options.PrioritiseBicycle` is serialized and tested, but no
current runtime or UI consumer was found.

## Location

- File: `TrafficLightsEnhancement/Components/CustomPhaseData.cs`
- Method/area: `CustomPhaseData.Options`

## What We Know

- `PrioritiseBicycle = 1 << 5` exists as a V2 option.
- Serialization tests round-trip it.
- Repository search found no non-test consumer.
- Bicycle demand currently flows through `m_BicycleLaneOccupied` and
  `m_BicycleWeight`.

## Evidence Checked

- Code: `CustomPhaseData`, `CustomStateMachine`, UI bindings, UI components.
- Tests: serialization and dynamic weight update tests.
- Docs: custom phase data flow, dynamic mode, save-format contract, roadmap.
- Diagnostics/in-game: no in-game evidence yet.
- Git history: blame points to a vague inherited commit and does not explain
  intent.

## Why It Matters

Future maintainers may assume this option changes bicycle priority, but current
behavior appears weight-driven instead.

## What Would Confirm It

- Source or in-game evidence showing a planned or legacy consumer.
- A decision to document it as reserved compatibility state, remove it through a
  migration, or implement runtime/UI semantics.
```

### `SecondaryLane` As Bicycle Proxy

`Unknown`: The current custom phase path treats non-car `SecondaryLane` lanes
as bicycle lanes and falls back to straight car masks when no bicycle mask is
configured. That may be exactly how Cities II represents bike lanes at
signaled road junctions, but we do not yet have in-game evidence across enough
road layouts.

Issue draft:

```markdown
*Written by Codex.*

## Mystery

Custom phase bicycle behavior treats non-car `SecondaryLane` lanes as bicycle
lanes and falls back to straight car masks when the bicycle mask is empty.

## Location

- File:
  `TrafficLightsEnhancement/Systems/TrafficLightSystems/Initialisation/CustomPhaseProcessor.cs`
- Method/area: custom phase lane signal processing
- Related:
  `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`

## What We Know

- Initialization applies `EdgeGroupMask.m_Bicycle.m_GoGroupMask` to non-car
  `SecondaryLane` sublanes near an edge.
- If the bicycle mask is zero, it uses the straight car go mask.
- Dynamic demand increments bicycle occupancy for petitioning `SecondaryLane`
  sublanes.
- Tests do not yet lock this behavior.

## Evidence Checked

- Code: initialization, dynamic state machine, `NodeUtils`, UI edge panel.
- Tests: no focused bicycle lane signal or occupancy tests found.
- Docs: custom phase data flow, dynamic mode.
- Diagnostics/in-game: no in-game evidence yet.
- Git history: blame points to broad inherited commits without useful intent.

## Why It Matters

If `SecondaryLane` covers non-bicycle lanes in relevant contexts, custom phases
may misclassify demand or apply bicycle masks too broadly.

## What Would Confirm It

- In-game inspection across bicycle-only, mixed, and secondary non-bicycle lane
  cases.
- Focused ECS/source regression tests for mask application and demand counting.
```

## Next Test Matrix

1. Serialization: keep existing V2 bicycle round-trip/default tests; document
   `PrioritiseBicycle` as reserved if it remains unused.
2. UI rendering: verify `bicycleLane` renders and toggles stop/go in the custom
   phase edge panel.
3. UI traffic groups: verify group member signal editing renders bicycle
   controls and writes `m_Bicycle`.
4. Initialization: a configured bicycle mask for phase N gives matching
   secondary bicycle lanes `LaneSignal.m_GroupMask == 1 << N`.
5. Initialization fallback: an empty bicycle mask falls back to straight car
   service.
6. Dynamic demand: a bicycle-lane petitioner increments
   `m_BicycleLaneOccupied` and affects weighted waiting through
   `m_BicycleWeight`.
7. Traffic groups: member bicycle occupancy aggregates into the group leader.
8. In-game smoke: test bicycle-only, mixed car+bike, and custom phase with
   bicycle mask disabled.
