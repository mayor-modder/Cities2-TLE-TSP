# Signal Phase Triage Checklist

This checklist is for reports such as "signals ignored my configured phases",
"phases advanced in the wrong order", "a configured movement stayed red", or
"dynamic timing skipped the phase I expected." It is maintainer-facing and
tracks issue #139.

## Scope

Use this checklist to separate:

- user configuration or a misunderstanding of custom phase timing
- topology that the selected mode does not support
- dynamic or smart phase selection doing what it is configured to do
- traffic group leader/follower synchronization
- Transit Signal Priority (TSP) interaction
- a likely runtime bug in initialization, custom phase selection, or signal
  object updates

Do not change behavior from a report alone. First collect enough evidence to
show which layer selected the observed phase and why. If the evidence cannot
explain that selection, file a narrower diagnostics issue before treating the
report as a gameplay bug.

## Reporter Evidence Packet

Ask for this packet before attempting a code fix.

- Game version, mod version or commit, and whether the save previously used
  upstream Traffic Lights Enhancement.
- A short reproduction path from a loaded save: selected junction, current
  tool mode, what phase was expected, what signal actually showed, and how long
  it was observed.
- Screenshots of the selected intersection with the TLE panel open:
  configured pattern, custom phase list, timing style, smart selection toggle,
  min/max duration, change metric, and the active phase.
- Screenshots of the custom phase editor for the specific movement that stayed
  red or went green unexpectedly. Include per-lane unlink state when used.
- Whether TLE diagnostics were enabled and the selected-intersection
  diagnostics rows for the same junction.
- The active and rotated JSONL diagnostics files when available. The files are
  written only while diagnostics are enabled and the selected panel refreshes.
- Whether the junction belongs to a traffic group, whether it is the leader,
  whether coordination or green wave is enabled, and whether phases were copied
  from another junction.
- Whether tram or bus TSP is enabled at the junction, and whether a tram or bus
  was approaching during the observed behavior.
- Whether the road layout changed after phases were configured, especially
  lane-count changes, track additions, bike lane changes, asymmetric junction
  edits, or replacement of connected road segments.

## Maintainer Decision Tree

1. Confirm the selected behavior mode.
   - `Vanilla`, split phasing, and protected-turn modes are predefined
     patterns. They do not preserve arbitrary custom phase order.
   - `Custom phases` uses the configured phase buffers and movement masks.
   - Traffic group members are forced through custom phase data so group
     coordination has enough state to run.

2. Confirm topology support.
   - If a predefined mode is missing or the options section is hidden, compare
     the selected-panel topology diagnostics with `GUIDE.md`.
   - Complex junctions, train/standalone tram crossings, and high edge counts
     can hide pattern options by design.
   - If the user expects unsupported predefined behavior, redirect them to
     custom phases rather than treating the report as a runtime bug.

3. Confirm configured movement masks.
   - Custom phase indexes are signal group numbers minus one. Phase 1 is
     signal group `G1`; bit `1 << 0` serves `G1`.
   - Edge masks serve default car, public-car, track, pedestrian, and bicycle
     movements. Per-lane masks override edge-level car, track, and pedestrian
     masks for unlinked lanes.
   - Bicycle service remains edge-level even when car, track, or pedestrian
     service is per-lane.
   - If a movement is not configured in the requested phase, the observed red
     is configuration, not a signal-selection bug.

4. Confirm the resolved runtime lane signals.
   - JSONL `laneSignals` shows the resolved `LaneSignal.m_GroupMask`,
     `yieldGroupMask`, active current/next/requested group booleans, and signal
     state for selected node sublanes.
   - If resolved lane signals do not include the configured group for the
     movement, suspect custom mask translation, topology remapping after a road
     edit, per-lane matching, master-lane merging, bicycle fallback, or
     pedestrian overlap handling.
   - If resolved lane signals include the configured group but the physical
     signal object stays red, investigate signal object masks or object update
     paths rather than custom phase data.

5. Confirm timing semantics.
   - Duration values are signal update ticks, not wall-clock seconds.
   - In dynamic mode, phases with minimum duration `0` are skippable unless
     current demand or priority is observed for that phase.
   - Dynamic mode may extend a busy phase until maximum duration or until the
     change metric, low-flow, and priority rules allow a change.
   - Fixed timed mode follows phase order more directly only when smart phase
     selection is disabled. With smart selection enabled, the selector can
     restart the current phase or choose another phase from measured demand.

6. Confirm traffic group interaction.
   - Coordinated followers do not run independent custom phase timing. They
     mirror the leader, or map the leader through green-wave phase offsets and
     signal delays.
   - Local TSP is suspended for every grouped junction, including the leader.
   - A follower showing a phase that differs from its local phase list may be
     following the group contract rather than ignoring configuration.
   - Phase copying between junctions is topology-sensitive. Treat copied phases
     on asymmetric or edited layouts as needing in-game evidence.

7. Confirm TSP interaction.
   - TSP does not generate custom phases. It can hold the current compatible
     phase or override the base next phase to the requested target group.
   - The decision trace records `baseGroup`, `selectedGroup`, request target,
     source, and pedestrian or vehicle fairness context when TSP applies.
   - Tram requests and bus-only-lane bus requests can aggressively preempt a
     conflicting group. Mixed-lane bus requests are soft and wait for normal
     transition points.
   - Exclusive pedestrian protection and vehicle fairness can defer a TSP
     override.

8. Decide whether this is a likely runtime bug.
   Treat the report as a candidate bug only when at least one of these is true:
   - the saved or visible configuration clearly serves the movement in a phase,
     but resolved lane signals do not include that phase
   - resolved lane signals include the current group, but lane or object signal
     state remains inconsistent through several signal updates
   - the selected group cannot be explained by manual selection, fixed order,
     smart selection, dynamic demand, linked phases, traffic group sync, TSP, or
     fairness policy
   - the behavior is reproducible after save/reload on the same junction and is
     not tied to an unsupported topology or stale road edit

## Current Evidence Map

Verified from code:

- Custom phase initialization validates edge and sub-lane masks, translates
  them into `LaneSignal` and `ExtraLaneSignal`, and sets signal group count
  from `CustomPhaseData` length.
- Runtime custom phase selection lives in `CustomStateMachine`. It handles
  manual group selection, fixed timed sequential selection, fixed timed smart
  selection, dynamic demand scanning, linked phases, phase timing, leader
  synchronization, and local TSP override.
- TSP diagnostics rows and JSONL traces include selected topology, expected UI
  option state, TSP request/debug data, TSP decision trace data, traffic group
  membership basics, resolved lane signal masks, and current `TrafficLights`
  state.
- JSONL `signalConfiguration` includes pattern, mode, smart-selection option,
  exclusive pedestrian state, and pedestrian phase mask.

Unknown from current diagnostics:

- Why the base custom phase selector chose its next group when TSP did not
  apply.
- Why the current custom phase ended or stayed active, including custom timer,
  min/max timing, target duration, metric result, low-flow counter, and
  low-priority counter.
- Which raw `EdgeGroupMask` and `SubLaneGroupMask` entries produced a resolved
  lane signal when a configured movement and runtime signal disagree.
- Which traffic group master-clock values and member phase offsets produced a
  coordinated follower's displayed phase.

Needs in-game evidence:

- Reports involving phase copy between different junction shapes, tram tracks,
  bike lanes, per-lane unlinked edits, asymmetric lane counts, or road edits
  after configuration.
- Reports where the JSONL trace shows resolved lane signals are correct but the
  visible world signal objects disagree.

## Follow-Up Issue Body Drafts

Use these if the current selected-panel and JSONL data cannot explain a report.
They are issue bodies, not proof that behavior is wrong.

### Add custom phase base-selection diagnostics

```markdown
*Written by Codex.*

## Problem

Reports that a signal ignored configured custom phases cannot currently prove
why `CustomStateMachine.GetNextSignalGroup(...)` chose the base next group
when TSP did not apply.

## Evidence Gap

Selected-panel diagnostics and JSONL traces show current/next group, TSP
request and decision data, resolved lane signal masks, and signal
configuration. They do not record the base custom phase selector path:
manual override, fixed sequential, fixed smart selection, dynamic positive
minimum phase, dynamic zero-minimum demand phase, dynamic fallback, linked
phase adjustment, or restart-current smart selection.

## Relevant Code

- `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`
- `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
- `docs/custom-phase-data-flow.md`
- `docs/dynamic-mode.md`

## Proposed Diagnostic Fields

- `baseSelection.mode`
- `baseSelection.currentGroup`
- `baseSelection.selectedBaseGroup`
- `baseSelection.reason`
- `baseSelection.smartSelectionEnabled`
- `baseSelection.linkedAdjustmentApplied`
- `baseSelection.restartCurrent`
- optional per-candidate summary for dynamic and smart selection

## Acceptance Criteria

- JSONL traces can distinguish base phase selection from TSP-selected phase
  overrides.
- The visible diagnostics panel gives a compact reason for the selected base
  group when diagnostics are enabled.
- No diagnostics work runs when TLE diagnostics are disabled.

## Suggested Labels

`agent-work`, `type:feature`, `priority:medium-term`, `area:ui`
```

### Add custom phase timing-transition diagnostics

```markdown
*Written by Codex.*

## Problem

Reports that phases end too early, stay green too long, or skip a configured
phase cannot currently prove why the active custom phase ended or remained
active.

## Evidence Gap

The custom phase panel already exposes live per-phase timing and demand values
such as `CustomTrafficLights.m_Timer`, minimum and maximum duration, dynamic
target duration, weighted wait, flow, low-flow counter, priority, change metric,
and `EndPhasePrematurely`. The JSONL trace and dedicated diagnostics do not
capture those values together with the missing transition context: metric
result, low-priority counter, max-priority comparison, and the final reason the
phase did or did not transition.

## Relevant Code

- `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`
- `TrafficLightsEnhancement/Components/CustomTrafficLights.cs`
- `TrafficLightsEnhancement/Components/CustomPhaseData.cs`
- `docs/dynamic-mode.md`

## Proposed Diagnostic Fields

- `phaseTiming.customTimer`
- `phaseTiming.minimumDuration`
- `phaseTiming.maximumDuration`
- `phaseTiming.targetDuration`
- `phaseTiming.flow`
- `phaseTiming.wait`
- `phaseTiming.priority`
- `phaseTiming.maxPriority`
- `phaseTiming.changeMetric`
- `phaseTiming.metricSaysChange`
- `phaseTiming.lowFlowTimer`
- `phaseTiming.lowPriorityTimer`
- `phaseTiming.endPhasePrematurely`
- `phaseTiming.transitionReason`

## Acceptance Criteria

- A JSONL trace can explain why an ongoing custom phase did or did not
  transition on a diagnostics refresh.
- The fields use the same tick units as runtime comparisons.
- The diagnostic snapshot is opt-in and does not change phase timing behavior.

## Suggested Labels

`agent-work`, `type:feature`, `priority:medium-term`, `area:ui`
```

### Add configured custom mask trace data

```markdown
*Written by Codex.*

## Problem

When a report says a configured movement stayed red or went green in the wrong
phase, the JSONL trace currently shows resolved lane signal masks but not the
raw configured custom phase masks that produced them.

## Evidence Gap

`laneSignals` can show whether a runtime lane serves the current, next, or
requested group. It cannot show whether the corresponding `EdgeGroupMask` or
`SubLaneGroupMask` configured that movement for the phase, whether per-lane
editing was active, or whether a road edit caused position/entity fallback to
choose a different mask.

## Relevant Code

- `TrafficLightsEnhancement/Systems/TrafficLightSystems/Initialisation/CustomPhaseProcessor.cs`
- `TrafficLightsEnhancement/Utils/CustomPhaseUtils.cs`
- `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
- `docs/custom-phase-data-flow.md`

## Proposed Diagnostic Fields

- selected junction custom phase count
- edge mask entries with entity, position, per-lane flag, and go/yield masks
- sub-lane mask entries with entity, position, and go/yield masks
- for each traced lane signal, the matched edge mask and sub-lane mask source
  when known
- whether the mask came from exact entity matching or position fallback when
  that can be captured safely

## Acceptance Criteria

- A JSONL trace can compare configured masks with resolved `LaneSignal` masks
  for the selected junction.
- Trace output remains bounded and opt-in.
- The diagnostics do not expose save data beyond the selected junction needed
  for troubleshooting.

## Suggested Labels

`agent-work`, `type:feature`, `priority:medium-term`, `area:ui`
```

### Add traffic group phase-selection trace data

```markdown
*Written by Codex.*

## Problem

For grouped intersections, a follower can appear to ignore its local phase
configuration because it mirrors the leader or maps the leader through green
wave offsets. Current diagnostics identify membership but do not explain the
group phase mapping.

## Evidence Gap

The JSONL `trafficGroup` object records membership, leader status,
coordination, green-wave enabled state, signal delay, and distance to leader.
It does not record group master phase, master next phase, master state, master
timer, master custom timer, member phase offset, member cycle timer, or the
mapped follower current/next group that `SyncSignalGroupWithLeader(...)`
applied.

## Relevant Code

- `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`
- `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`
- `TrafficLightsEnhancement/Components/TrafficGroup.cs`
- `TrafficLightsEnhancement/Components/TrafficGroupMember.cs`
- `docs/traffic-groups.md`

## Proposed Diagnostic Fields

- `trafficGroup.memberPhaseOffset`
- `trafficGroup.memberCycleTimer`
- `trafficGroup.masterPhase`
- `trafficGroup.masterNextPhase`
- `trafficGroup.masterState`
- `trafficGroup.masterTimer`
- `trafficGroup.masterCustomTimer`
- `trafficGroup.mappedCurrentGroup`
- `trafficGroup.mappedNextGroup`
- `trafficGroup.mappingReason`

## Acceptance Criteria

- A grouped-junction JSONL trace can explain whether the observed group came
  from lockstep sync, green-wave offset mapping, or independent leader timing.
- The panel states clearly that local TSP remains suspended while grouped.
- Diagnostics stay read-only and do not alter group coordination.

## Suggested Labels

`agent-work`, `type:feature`, `priority:medium-term`, `area:ui`
```
