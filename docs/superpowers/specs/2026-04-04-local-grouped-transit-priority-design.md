# Local Grouped Transit Priority Design

> Supersedes the grouped-intersection runtime guard direction from `docs/superpowers/specs/2026-03-28-grouped-junction-tsp-guard-design.md`.

## Summary

This design enables Transit Signal Priority (TSP) on intersections that belong to traffic groups, but keeps propagation local. A grouped tram or bus-lane request should help the requesting junction and a short run of downstream group members, not the entire corridor.

The goal is to support two common use cases with one model:

- boulevard-style compound crossings where two nearby signals should behave like one transit-priority crossing
- longer tram or BRT corridors where priority should travel ahead of the vehicle without turning distant intersections red at the same time

## Terminology

- **Transit Signal Priority (TSP):** The existing feature that lets eligible transit vehicles request an earlier serving phase or a short extension of the current green.
- **Grouped intersection:** Any intersection with `TrafficGroupMember`.
- **Group propagation:** Additional TSP influence from one grouped intersection to nearby downstream group members.
- **Local propagation window:** A fixed forward distance within a traffic group where a grouped transit request is allowed to influence other members.
- **Transit vehicle:** For this design, either a tram/track vehicle or a bus/public transport vehicle on a `PublicOnly` car lane. Buses on bus lanes are treated like BRT and should behave like trams for propagation.

## Problem Statement

The current grouped-TSP code path does not meet the desired gameplay behavior:

- grouped intersections are currently treated as runtime-ineligible for TSP
- the selected-intersection UI disables TSP for grouped intersections
- the existing group runtime state model uses one aggregate state per group, which is too coarse for long corridors
- a whole-group model would risk flipping large coordinated avenues in response to one tram or bus

At the same time, users already build both:

- small grouped compounds that should behave like one intersection for transit priority
- long coordinated green-wave corridors where transit priority should stay near the vehicle

The runtime must support both without requiring separate group types.

## Product Decision

Grouped intersections may use TSP.

Local TSP remains a per-junction feature controlled by the existing `TransitSignalPrioritySettings`. Grouped propagation is an additional opt-in layer controlled by:

- the per-group toggle `TrafficGroup.m_TspPropagationEnabled`
- the per-junction toggle `TransitSignalPrioritySettings.m_AllowGroupPropagation`

If either toggle is off, the grouped intersection may still use local TSP for itself, but it must not propagate that request to other group members.

This preserves the existing corridor/group workflow:

- users can leave a whole corridor grouped and coordinated
- users can turn grouped propagation on only for groups where it makes sense
- the runtime still limits the effect to a local downstream window

## Runtime Rules

### 1. Grouped intersections are locally eligible

The grouped-membership guard in `TspPolicy` must be removed.

If a grouped intersection has TSP enabled and detects an eligible tram or bus-lane vehicle, it should be allowed to build a normal local request for its own junction. Group membership alone must no longer disable the request.

### 2. Propagation is local, not whole-group

When a grouped intersection produces a local request and propagation is allowed at both the junction and group levels, the request may be copied only to downstream members inside the local propagation window.

Propagation must never mean:

- all members in the group adopt the same request
- every intersection in the corridor switches red or green together
- the entire group becomes blocked by a transit vehicle far away

### 3. Propagation is ahead-only

"Ahead" is based on traffic-group member order, not raw map radius.

For V1, the runtime should:

- sort members by `TrafficGroupMember.m_GroupIndex`
- treat the requesting member as the origin
- walk forward only to higher-index members
- stop once cumulative member-to-member distance exceeds the propagation window

This gives a stable notion of "downstream" even on curved or offset boulevards and avoids accidentally catching nearby side streets or the opposite carriageway just because they are close in straight-line distance.

### 4. The window is fixed in code for V1

The propagation distance should be a fixed default constant in code for the first version. It should not be a per-group UI setting yet.

This keeps the UI small while we verify the basic runtime behavior. Tuning the exact default distance can happen in code and tests first.

### 5. Strongest local request wins

If multiple propagated or local requests overlap on the same affected intersection, V1 should prefer the strongest request.

If strengths are equal, the implementation should use a deterministic tiebreaker. The recommended order is:

1. nearest upstream origin within the window
2. lower `m_GroupIndex` origin

The purpose of the tiebreaker is stability, not additional game behavior.

## Data Flow

### Request Origin

Each grouped intersection still detects its own local TSP request using the existing lane-based request builder.

That origin request should include enough runtime information to support propagation:

- origin junction entity
- origin group entity
- origin member index
- target signal group
- source type (`Track` or `PublicCar`)
- strength
- expiry timer
- whether the current phase can be extended

### Propagation Calculation

The traffic-group runtime should gather current local requests from grouped members, then project those requests forward through the ordered member list until the fixed cumulative distance limit is reached.

For each affected member, the runtime should choose the strongest applicable request and write a transient per-member propagated request.

### Consumption

During signal simulation, a grouped junction should choose between:

- its own local request
- any propagated grouped request written for that same junction

The winner becomes the active request that drives the existing TSP phase override / extension logic.

This keeps TSP as a bias layer on top of the current signal-selection model instead of inventing a second timing engine.

## State Model

The current `TrafficGroupTspState` component stores one aggregate request on the group entity. That model is too coarse for local propagation because one corridor may need different requests in different places at the same time.

V1 should replace the effective runtime propagation model with a transient per-junction propagated-request component.

Recommended shape:

- keep `TransitSignalPriorityRequest` for local junction requests
- add a new transient component for grouped propagated requests written to individual junction entities
- stop relying on a single request stored on the group entity for runtime control

This lets two trams in different parts of the same corridor influence only their nearby future intersections without interfering across the whole group.

## UI Behavior

### Selected Intersection Panel

Grouped intersections should no longer have the TSP section disabled only because they belong to a traffic group.

The panel should:

- allow `Enable TSP`
- allow `Allow Tram Requests`
- allow `Allow Bus Lane Requests`
- continue showing `PropagateTransitRequestsToGroup` only for grouped intersections

If a grouped intersection belongs to a group where group propagation is turned off, the intersection may still use local TSP. In that case, the panel should not describe TSP as unavailable. At most, it may show an informational message that grouped propagation also requires enabling the group-level option.

### Traffic Group Panel

Keep the existing per-group toggle `m_TspPropagationEnabled`.

Its meaning changes from "whole-group propagation" to:

`Allow local grouped transit priority propagation within a fixed downstream distance window.`

No extra per-group radius control is added in V1.

## Diagnostics

Diagnostics should reflect local propagation rather than group-wide aggregation.

Useful runtime diagnostics for V1:

- when a grouped junction starts or clears a local request
- when a propagated request is written or cleared for a member
- whether a final TSP decision came from a local request or a grouped propagated request
- the origin member and target signal group for propagated requests

Group-level "aggregate request started" logging should not remain the primary diagnostic model because it suggests a whole-group behavior that V1 no longer uses.

## Save and Migration Behavior

Persistent `TransitSignalPrioritySettings` and `TrafficGroup` serialization can remain intact.

This design does not require a save migration for user-facing settings if we:

- keep using `TrafficGroup.m_TspPropagationEnabled`
- keep using `TransitSignalPrioritySettings.m_AllowGroupPropagation`
- store the propagation distance as a code constant rather than a serialized field

Any new propagated-request component should be transient runtime state only.

## Scope

### In Scope

- remove grouped-membership runtime ineligibility for local TSP
- enable TSP controls for grouped intersections in the selected-intersection panel
- keep grouped propagation opt-in per group
- propagate requests only to downstream members inside a fixed cumulative-distance window
- treat trams and buses on bus-only lanes equivalently for grouped propagation
- choose the strongest applicable request per affected junction
- consume propagated requests per junction during signal simulation
- update diagnostics to match the local propagation model

### Out of Scope

- per-group propagation radius UI
- corridor pathfinding beyond traffic-group member order
- backward propagation
- whole-group simultaneous preemption
- a separate "compound intersection" group type
- dynamic weighting beyond the current strength-first rule

## Risks

- `m_GroupIndex` reflects membership order, not true road topology. For oddly ordered groups, "ahead" may not match the player's intended corridor direction.
- If the fixed distance is too large, the system may still feel corridor-wide. If it is too small, boulevard compounds may not stay green through the second light.
- Local TSP plus coordinated timing can still create recovery behavior that feels imperfect, especially on dense corridors with many transit vehicles.
- Replacing the old single group-state model will require careful cleanup so stale propagated runtime components do not linger.

## Testing Strategy

### Pure Logic Tests

- grouped intersection is runtime-eligible when TSP is enabled
- propagation window includes downstream members until cumulative distance exceeds the limit
- propagation never includes upstream members
- overlapping requests on the same target member choose the strongest request
- equal-strength requests use the deterministic tiebreaker
- buses on `PublicOnly` lanes and trams both produce propagatable requests

### Runtime / ECS Tests

- grouped member with TSP enabled produces a local request for itself
- grouped member with both propagation toggles enabled writes propagated requests only to downstream members inside the window
- disabling group propagation clears propagated runtime state but leaves local TSP active
- distant members in the same corridor do not receive the propagated request
- two separate local request zones in the same group can coexist without a single whole-group request taking over

### UI Tests

- grouped intersections show interactive TSP controls instead of the old unavailable-state messaging
- grouped intersections still expose the per-junction propagation checkbox
- traffic-group UI continues to expose the per-group propagation toggle with updated wording

## Recommendation

Implement grouped TSP as local downstream propagation layered on top of the existing per-junction TSP model. Treat group membership as an opportunity for nearby progression, not as a reason to disable TSP and not as a license to control an entire corridor at once.
