# Transit Signal Priority Design

## Goal

Add Transit Signal Priority (TSP) to the existing TLE fork as a first-class traffic-light feature for private/local use, while preserving the current traffic-group, green-wave, and saved-junction behavior as much as practical.

## Repo Context

This design targets `bruceyboy24804/Cities2-TrafficLightsEnhancement`, not the `slyh` fork inspected earlier in the session.

The key architectural facts in this fork are:

- Junction-local custom phases and dynamic/fixed-timed phase selection live in `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`.
- Coordinated traffic-light groups are implemented already via `TrafficGroup` and `TrafficGroupMember`.
- Green-wave behavior, member phase offsets, per-member signal delays, and leader/follower propagation live in `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`.
- Followers can already mirror leader phase state in `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`.
- Tram/track lanes are already represented separately from regular car lanes, and bus/public lanes are already represented separately from regular car lanes.

## Confirmed Requirements

- Extend the existing TLE codebase rather than building a second mod.
- Preserve current TLE functionality where practical.
- Preserve existing TLE-configured intersections and saved-data behavior where practical.
- TSP must be enableable per traffic light.
- TSP must integrate with the existing coordinated/grouped/green-wave traffic-group system.
- Do not create a second coordination/grouping model.
- Dedicated tram track lanes must be supported.
- First version may require buses to be on bus/public lanes in order to trigger TSP.

## Non-Goals For V1

- Mixed-traffic bus detection.
- Emergency-vehicle style hard preemption.
- A second corridor or route management system.
- Replacing the existing green-wave or traffic-group implementation.
- Public-distribution packaging concerns.

## Existing Behavior Summary

### Local Traffic-Light Control

Per-junction traffic-light behavior is driven by `CustomTrafficLights`, `CustomPhaseData`, edge/sub-lane group masks, and the custom state machine.

The state machine already:

- tracks current per-phase occupancy and flow,
- distinguishes car, public-car, track, pedestrian, and bicycle demand,
- supports dynamic and fixed-timed control,
- supports linked adjacent phases inside a junction,
- and can end phases early or select the best next phase from observed demand.

This means TSP should bias an existing phase-selection engine rather than replace it.

### Group Coordination / Green Wave

This fork already has a leader/follower group model:

- `TrafficGroup` stores whether the group is coordinated and whether green wave is enabled.
- `TrafficGroupMember` stores leader/follower membership plus phase offset and signal delay.
- `TrafficGroupSystem` computes group timing and green-wave delays.
- `CustomStateMachine` can force follower junctions to mirror leader phase state.

This is the correct architectural seam for corridor-wide TSP. The leader should make the corridor-level choice; followers should continue using the existing synchronization and delay model.

## Recommended Approach

Add TSP as a request layer on top of the existing local custom-phase and group-coordination systems.

### Why This Approach

- It reuses the current traffic-group and green-wave model instead of introducing competing coordination logic.
- It preserves the meaning of existing custom phases and group membership.
- It supports standalone junction TSP and coordinated corridor TSP with the same request model.
- It keeps compatibility risk lower than embedding a large amount of new behavior into phase definitions themselves.

## TSP Model

### Persistent Settings

Add a dedicated persistent per-junction TSP settings component rather than overloading `CustomTrafficLights`.

Reasoning:

- `CustomTrafficLights` already carries mode/pattern/options state and a long serialization history.
- A separate settings component is safer for save compatibility because existing junctions simply do not have the component until TSP is enabled.
- This also makes it easy to keep TSP off by default.

Suggested fields:

- `m_Enabled`
- `m_AllowTrackRequests`
- `m_AllowPublicCarRequests`
- `m_RequestHorizonTicks`
- `m_MaxGreenExtensionTicks`
- `m_AllowGroupPropagation`

V1 should default to:

- enabled: false
- track requests: true
- public-car requests: true
- group propagation: true when the junction is in a coordinated group, otherwise irrelevant

### Runtime State

Add a transient runtime request component or buffer that is recomputed in simulation and not treated as long-term authored configuration.

Suggested data:

- requested phase index
- request source type (`Track`, `PublicCar`)
- request strength
- request expiry / cooldown
- whether the request is local or aggregated from a grouped follower

## Request Detection

### Eligible Vehicles In V1

- Trams and other relevant track-based transit on dedicated track lanes.
- Buses only when they are using `PublicCar` lanes.

This matches the repo’s current lane classification and avoids mixed-traffic bus detection in the first implementation.

### Detection Strategy

TSP detection should operate during the same simulation pass that already computes occupancy and priority. It should inspect approaching demand on eligible lanes and map that demand onto serving phases using the existing edge/sub-lane masks.

The important detail is that TSP should be phase-aware, not just lane-aware. A request is only useful if it can be translated into one or more target signal groups already defined by the current junction.

## Phase Selection Integration

TSP should influence the state machine in two limited ways:

- early green for a transit-serving phase,
- short extension of an already-active transit-serving phase.

It should not:

- create new phases,
- change group membership,
- rewrite masks,
- or bypass safety sequencing.

The local state machine should apply TSP bias before its normal “best next phase” selection is finalized. If no valid TSP request exists, the current selection logic remains unchanged.

## Coordinated Group Integration

### Leader/Follower Rule

If a junction is a coordinated follower, it should not independently pick a conflicting corridor phase. Instead:

- the follower produces a local TSP request,
- the request is aggregated upward to the group leader,
- the leader biases its next phase decision,
- followers continue to sync from the leader using the current phase-offset and signal-delay behavior.

### Green-Wave Rule

Green wave remains the base timing model. TSP is an override/bias layer.

For V1:

- prefer leader-phase choice changes over retiming the entire corridor,
- keep green-wave speed/offset semantics intact,
- and only use existing per-member delay/offset infrastructure to distribute the chosen phase downstream.

This keeps the corridor logic understandable and avoids turning TSP into a second timing engine.

## UI Design

### Per Traffic Light

Add a compact TSP section to the existing traffic-light UI:

- `Enable TSP`
- `Allow Tram / Track Requests`
- `Allow Bus Lane Requests`
- `Request Horizon`
- `Max Green Extension`

Only show the tuning fields when TSP is enabled.

### Per Traffic Group

Add one group-level option to the existing traffic-group panel:

- `Allow Coordinated TSP Propagation`

Do not create a second “TSP group” screen or alternative corridor editor.

### Custom Phase UI

Avoid asking the user to build a second TSP-to-phase mapping manually if it can be inferred from existing masks. It is acceptable to surface informative labels or badges showing that a phase currently serves track/public lanes.

## Save Compatibility

Compatibility strategy:

- keep all new settings disabled by default,
- use a separate serialized component for persistent TSP settings,
- use transient runtime components for active requests,
- and preserve existing `TrafficGroup`, `TrafficGroupMember`, `CustomTrafficLights`, and `CustomPhaseData` semantics.

Existing saves should continue to load without TSP data present.

## Risks

### Medium Risk

- Mapping a live transit request to the correct target phase at complex junctions.
- Ensuring group leaders aggregate follower requests without over-favoring one member.
- Avoiding oscillation between green-wave timing and local TSP pressure.

### High Risk

- Anything that rewrites existing phase masks or group membership automatically.
- Mixed-traffic bus detection in V1.
- Hard preemption that skips safety sequencing.

## Verification Strategy

Automated coverage in this repo is currently absent, so implementation should add at least a small pure-logic test surface for:

- request-to-phase scoring,
- leader aggregation,
- extension vs early-green decision rules.

Manual in-game verification should cover:

- standalone tram junction with dedicated track lanes,
- standalone bus-lane junction,
- coordinated corridor with green wave and a follower-originated transit request,
- save/load of old saves with TSP disabled,
- save/load of new TSP-enabled saves.

## Recommendation

Implement TSP as a new per-junction settings + runtime request layer that plugs into:

- the existing custom-phase state machine for local decisions,
- and the existing `TrafficGroup` leader/follower model for corridor decisions.

This gives the requested “use the current grouped/green-wave system” behavior without introducing a second coordination model.
