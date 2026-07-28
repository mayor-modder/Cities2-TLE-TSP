# Group-wide vanilla demand coordination design

## Problem

Coordinated traffic-group followers currently copy the leader's signal state before the custom/vanilla dispatch. This makes vanilla followers visibly match the leader, but it also prevents them from running the vanilla demand-selection path that consumes their lane requests. The leader still chooses phases from its own junction's lane signals only.

Live testing in Copeland demonstrated the resulting failure. The leader and follower both remained on vanilla group 1 while the follower's traffic was unable to influence the next selection and a queue formed between the junctions. At the paused frame, the leader reported `Ongoing / G1 / timer 150 / no next group`; the follower reported `Ongoing / G1 / timer 149 / next G1`. The latter mismatch also shows that the current phase wrapper converts the zero-valued "no next group" sentinel into group 1.

The desired behavior is demand-responsive vanilla coordination: every member contributes traffic demand, the leader makes one decision for the group, and followers apply that newly selected state in the same simulation tick.

## Goals

- Include demand from every coordinated base-state-machine junction when the group leader selects a vanilla phase.
- Consume and reset each member's lane request bookkeeping once per signal update.
- Apply the leader's newly calculated state to followers in the same simulation tick.
- Preserve zero-valued optional phase fields instead of wrapping them to group 1.
- Fall back to independent vanilla behavior when group aggregation or master-state data is missing or invalid.
- Keep traffic-group save data and existing custom-phase behavior compatible.

## Non-goals

- Group-wide TSP. Local TSP remains suspended for every grouped junction.
- Changes to serialized `TrafficGroup`, `TrafficGroupMember`, or custom phase payloads.
- New traffic-group UI controls or visual redesign.
- Green-wave policy changes beyond using safe optional-phase mapping where the same sentinel contract applies.
- Solving arbitrary phase correspondence between junctions with incompatible movement layouts. Existing one-based phase wrapping remains the compatibility rule.

## Runtime architecture

The implementation will split coordinated base-state-machine processing into three ordered stages.

### 1. Collect member demand

A collection job will run before traffic-light state selection for coordinated group members that use the base state machine, including vanilla and predefined patterns.

For each member it will:

- enumerate that junction's lane signals using the same lane discovery rules as the current traffic-light job;
- calculate the junction-local highest priority, requested phase mask, extendable phase mask, negative/suppressed phase mask, petitioner, and blocker relationships;
- reset lane-signal petitioner and priority fields exactly once, preserving the vanilla consumption contract;
- map the local phase masks into the leader's one-based phase space using the existing wrapping compatibility rule; and
- append a runtime-only demand summary keyed by traffic-group entity.

The summary is transient job memory. It is not an ECS save component and is never serialized.

Local blocker bookkeeping remains local to the junction. Only the phase-selection data needed by the group leader is aggregated.

### 2. Update independent junctions and group leaders

The main traffic-light update will retain existing behavior for ungrouped junctions and custom-phase junctions.

For a coordinated group leader using the base state machine, the vanilla selector will combine all demand summaries for that group. It will preserve the current vanilla priority semantics:

- the highest priority level wins;
- equal-priority phase masks are combined;
- extendable masks are combined only for winning demand;
- negative/suppressed masks are honored when no positive petitioner exists; and
- the existing cyclic preference and fairness rules select among eligible phases.

After updating a coordinated leader, the job will publish its newly calculated traffic-light state to a runtime-only master-state map keyed by group entity. This master record contains current phase, optional next phase, state, base timer, custom timer, and signal-group count.

### 3. Synchronize followers

A dependent follower job will run after the leader update. Coordinated base-state-machine followers will read the new master-state map and apply the leader state during the same simulation tick.

Follower phase mapping will distinguish required and optional values:

- required current phases use the existing one-based wrap rule;
- an optional phase value of zero remains zero;
- nonzero optional phases use the existing one-based wrap rule.

The follower job then refreshes lane signals and traffic-light objects from the synchronized state.

If no valid demand aggregate or master state exists, the affected junction must not keep copying stale data. It uses its already-collected local demand summary to run an independent base-state-machine update for that tick. This is the safety path for invalid group references, missing leaders, unsupported phase counts, or incomplete runtime maps.

## Pure policy boundary

The demand data and phase-selection math will be represented in `TrafficLightsEnhancement.Logic/TrafficGroups` without Unity dependencies where practical.

The pure layer will own:

- merging two or more demand summaries by vanilla priority semantics;
- remapping one-based phase masks between member and leader phase counts;
- preserving zero for optional one-based phase values; and
- selecting or exposing the winning masks needed by the existing runtime selector.

Unity/ECS code will remain responsible for reading and resetting `LaneSignal` components, maintaining petitioner/blocker entities, scheduling jobs, and applying `TrafficLights` state.

## Compatibility and failure handling

- No serialized field, version, enum, or component layout changes.
- Existing custom-phase followers remain on the current custom synchronization path; this change does not alter their selection or timing policy.
- Ungrouped vanilla and predefined-pattern intersections retain their current update path.
- Grouped base-state-machine junctions with incompatible or missing phase counts fall back to independent operation instead of freezing.
- Runtime allocations are sized from the current traffic-light/group member counts and disposed through the scheduled job dependency.
- TSP remains disabled for grouped intersections, including the leader.

## Testing

Implementation will follow test-driven development.

Pure tests will cover:

- higher-priority follower demand overriding lower-priority leader demand;
- equal-priority member demand combining eligible phase masks;
- phase-mask remapping between different phase counts;
- zero-valued optional phases remaining zero;
- nonzero optional phases wrapping normally; and
- invalid phase counts producing a safe non-aggregated result.

ECS/source-contract tests will cover:

- demand collection preceding leader state selection;
- each grouped base-state-machine member consuming its local lane requests once;
- coordinated leaders reading the group-wide aggregate;
- follower synchronization running after leader updates;
- missing master-state data taking the independent fallback path; and
- ungrouped/custom-phase dispatch remaining intact.

Non-deploying test projects may run while Cities: Skylines II remains open. The Release build is deferred because it post-processes and copies files directly into the live local mod directory.

## In-game verification

When the implementation and non-deploying tests are ready, Cities: Skylines II must be closed before the Release build/install.

The live test will recreate a two-junction coordinated group with:

- vanilla pattern on both junctions;
- green wave disabled;
- zero signal delay and phase offset; and
- diagnostics enabled.

Verification requires at least three complete leader cycles. For each cycle, capture leader and follower state, current/next phase, timers, visible lane signals, and whether queues at both junctions receive service. Success requires same-tick follower state, no zero-to-G1 sentinel conversion, and no follower approach remaining unserved solely because the leader lacks matching local demand.

If the group causes a growing queue, the simulation will be paused immediately and the group removed before normal play resumes.
