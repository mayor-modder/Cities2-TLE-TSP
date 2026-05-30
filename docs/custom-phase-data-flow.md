# Custom Phase Data Flow

This document maps how custom phases are created, edited, serialized, and
applied to lane signals. It complements [`dynamic-mode.md`](dynamic-mode.md),
which explains the phase selection policy once the data reaches simulation.

## Data Model

Custom phase mode is represented by one component plus three buffers on the
junction entity:

- `CustomTrafficLights`: selected pattern, dynamic/fixed mode, timer, manual
  signal group, smart-selection option, and exclusive pedestrian metadata.
- `CustomPhaseData`: one buffer element per phase. Stores timing, runtime
  counters, demand metrics, linked-phase/end-phase flags, dynamic-mode weights,
  phase-change metric, smoothing, and per-phase delay fields.
- `EdgeGroupMask`: one buffer element per connected edge. Stores which phase
  indexes serve each car, public-car, track, pedestrian, and bicycle movement.
- `SubLaneGroupMask`: optional per-sub-lane override used when an edge mask is
  marked `PerLaneSignal`.

The important invariant is that `CustomPhaseData` buffer indexes are signal
group indexes minus one. Phase index `0` corresponds to signal group `1`, and
bit `1 << index` in the edge/sub-lane masks means that movement is served by
that phase.

## Entering Custom Phase Mode

The main panel exposes `CustomPhases` as a selectable pattern. The React UI
sends the selected pattern through the existing main panel bindings.

On the C# side, `UISystem.UIBIndings.SetPattern(...)` handles selected-node
changes:

- traffic group members are forced to `CustomPhase`
- entering custom phase mode sets `CustomTrafficLights` to dynamic mode
- the selected entity is given `CustomPhaseData`, `EdgeGroupMask`, and
  `SubLaneGroupMask` buffers if they do not already exist
- an empty phase buffer gets one default `CustomPhaseData`
- edge info is refreshed and phase `0` becomes the active editing phase

Traffic-group member editing uses `CallUpdateMemberPattern(...)`, which follows
the same buffer-creation boundary for the addressed junction entity.

## Editing Phases

The custom phase panel is rendered by:

- `UI/src/mods/components/custom-phase-tool/main-panel/index.tsx`
- `UI/src/mods/components/custom-phase-tool/main-panel/item.tsx`
- `UI/src/mods/components/custom-phase-tool/main-panel/sub-panel.tsx`
- `UI/src/mods/components/custom-phase-tool/edge-panel.tsx`
- `UI/src/mods/components/custom-phase-tool/sublane-panel.tsx`

The UI sends JSON payloads through bindings declared in `UI/src/bindings.ts`.
The main C# handlers are in `UISystem.UIBIndings.cs`:

- `CallAddCustomPhase`: appends a default `CustomPhaseData` and selects it.
- `CallRemoveCustomPhase`: removes a phase and shifts every later mask bit down
  by repeatedly calling `CustomPhaseUtils.SwapBit(...)`.
- `CallSwapCustomPhase`: swaps two phase entries and swaps the same bit
  positions in edge and sub-lane masks.
- `CallSetActiveCustomPhaseIndex`: controls editing, viewing, and manual signal
  group state.
- `CallUpdateCustomPhaseData`: updates mode/options or one `CustomPhaseData`
  entry through `CustomPhaseDataUpdate.TryApply(...)`.
- `CallUpdateEdgeGroupMask` and `CallUpdateSubLaneGroupMask`: replace movement
  bitmasks after the floating lane widgets are clicked.
- `CallApplyPhaseTemplate` and `CallApplyUserPreset`: apply timing presets to
  every phase in the selected junction.

Timing edits preserve `minimum <= maximum`. When minimum or maximum duration
changes on a traffic group member, the group cycle length is recalculated.

## Movement Masks

`EdgePanel` shows the default edge-level movement masks. Clicking a movement
cycles its bit for the active phase:

- car and public-car turns cycle stop -> go -> yield -> stop
- track, bicycle, and pedestrian movements toggle stop/go

If the user unlinks an edge for per-lane editing, `EdgeGroupMask.Options`
receives `PerLaneSignal`. `SubLanePanel` then edits `SubLaneGroupMask` entries
instead of the edge-wide mask. Linking the edge again clears `PerLaneSignal`.
`SubLaneGroupMask` does not have a bicycle field, so bicycle service remains an
edge-level `EdgeGroupMask.m_Bicycle` mask even when car, track, or pedestrian
service is edited per sub-lane. See
[`bicycle-signal-research.md`](bicycle-signal-research.md) for the current
bicycle signal map and open risks.

Generation and validation depend on positions as well as entity IDs.
`CustomPhaseUtils.ValidateBuffer(...)` keeps masks aligned to the current
connected edges/sub-lanes after a road edit. It first tries exact entity matches
and then falls back to loose position matching before removing stale entries.

## Initialization And Lane Signals

`PatchedTrafficLightInitializationSystem` rebuilds traffic light lane signals
for nodes that need initialization.

For `CustomPhase` pattern nodes with all three custom buffers available, it:

1. Validates edge and sub-lane masks with `CustomPhaseUtils.ValidateBuffer(...)`.
2. Calls `CustomPhaseProcessor.ProcessLanes(...)`.
3. Sets the signal group count to `customPhaseDataBuffer.Length`.

`CustomPhaseProcessor` translates configured masks into `LaneSignal` and
`ExtraLaneSignal` components:

- source edge/sub-lane resolution maps node lanes back to their configured edge
  or per-lane mask
- car lanes choose car or public-car turn masks based on public-only flags
- track lanes use track turn masks
- crosswalks use pedestrian masks
- bicycle/secondary lanes use the edge bicycle mask, or fall back to the
  straight car mask when the bicycle mask is empty
- master lanes merge the masks of their slave lanes

The processor finally calls `PatchedTrafficLightSystem.UpdateLaneSignal(...)`
so the vanilla signal data sees the generated group masks.

## Runtime Consumption

`PatchedTrafficLightSystem` dispatches custom phase junctions into
`CustomStateMachine.UpdateTrafficLightState(...)`.

`CustomStateMachine` is responsible for:

- updating per-phase flow, wait, and occupied-lane counters
- enforcing minimum and maximum durations
- applying dynamic-mode metrics or fixed-timed sequencing
- respecting linked-phase behavior
- honoring manual signal group selection
- synchronizing coordinated traffic group followers from their leader
- applying local TSP overrides after the base custom-phase choice is known

`CustomTrafficLights.m_Timer` is the timer used by the state machine. The UI
labels duration fields like seconds, but the runtime compares the values against
signal update ticks directly.

## Serialization

The custom phase data model is persisted through `ISerializable` components and
buffers:

- `CustomTrafficLights` writes the selected pattern, pedestrian phase metadata,
  timer, manual group, mode, and options.
- `CustomPhaseData` writes V2 phase timing, counters, options, delay fields,
  weights, dynamic metrics, and flow/wait state.
- `EdgeGroupMask` writes V2 edge masks, bicycle/pedestrian masks, and edge
  delays.
- `SubLaneGroupMask` writes V1 sub-lane masks.

`TLEDataMigrationSystem` validates custom phase buffers after load. It clamps
invalid enum values, removes orphaned custom phase buffers when their owning
traffic-light component failed to load, validates traffic group leader/member
phase availability, and can repair inherited/legacy save data.

## Special Interactions

Linked phases are stored in `CustomPhaseData.Options.LinkedWithNextPhase`.
Adding, removing, or reordering phases must keep these flags on the intended
phase entries while also moving the movement mask bits. Linked selection rules
are documented in [`dynamic-mode.md`](dynamic-mode.md).

Vehicle weights live in `CustomPhaseData` and affect dynamic-mode waiting
pressure only. They do not alter which lanes are served by a phase; the served
lanes come from `EdgeGroupMask` and `SubLaneGroupMask`.

Bicycle lane support is currently a custom-phase edge-mask path. Generated
custom phases can author `EdgeGroupMask.m_Bicycle`, initialization applies it
to non-car `SecondaryLane` lanes, and dynamic mode counts petitioning
`SecondaryLane` lanes as bicycle demand. The code also persists bicycle delay
fields and `CustomPhaseData.Options.PrioritiseBicycle`, but no runtime consumer
for those priority/delay semantics has been confirmed yet.

Exclusive pedestrian phase is stored as a pattern flag on `CustomTrafficLights`.
Predefined initialization can add an exclusive pedestrian group, while custom
phase pedestrian service is controlled by the configured pedestrian masks. TSP
uses the exclusive-pedestrian state to avoid preempting away from an active
pedestrian phase when that protection applies.

TSP does not generate custom phases. It observes the resolved signal groups and,
for eligible custom phase junctions, can extend the current phase or override
the next selected phase inside `CustomStateMachine`. Grouped intersections
ignore local TSP requests, including the group leader, because group
coordination and green-wave timing are authoritative while a junction is in a
traffic group.

## Safe Refactor Boundaries

Good cleanup targets:

- keep `CustomPhaseDataUpdate.TryApply(...)` as the single shared mapping for
  selected-junction and member-junction phase edits
- extract movement-mask bit updates into pure helpers before changing edge or
  sub-lane UI behavior
- add tests around add/remove/swap mask alignment before changing phase
  ordering

Risky cleanup targets:

- changing phase index semantics, because indexes are persisted and encoded in
  every movement mask
- changing `ValidateBuffer(...)` matching order, because it protects saved
  configurations after road edits
- changing duration units, because existing saves and UI labels already rely on
  raw signal update ticks even though the label says seconds
- reordering or removing custom phases without moving every edge movement mask,
  including `EdgeGroupMask.m_Bicycle`
