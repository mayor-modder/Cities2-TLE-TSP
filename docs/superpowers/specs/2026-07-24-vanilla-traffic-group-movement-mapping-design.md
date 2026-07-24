# Vanilla traffic-group movement mapping design

## Goal

Make coordinated vanilla traffic groups control equivalent physical movements at every member intersection, share demand without starving a phase, and run without Unity temporary-job-memory lifetime violations.

## Confirmed failures

The failed live test used group `62119:17`, leader `778429:1`, and follower `778427:1`.

- At simulation frame `69714899`, leader, follower, and group master all reported `G1`, `Ongoing`, timer `81`.
- The physical corridor signal was green at the leader and red at the follower.
- Edge `735246:1` is shared by the junctions. It is outbound at the leader and an inbound three-car-lane plus one-track-lane approach at the follower.
- The installed synchronization path copied the one-based phase number without establishing that the number represented the same movement at both junctions.
- The new group-demand merge discarded every lower-priority member mask when another member had a higher priority. This can keep the current leader phase selected indefinitely.
- The new multi-pass implementation allocated four `Allocator.TempJob` hash maps. The previous launch had no `JobTempAlloc` or invalid-pointer reports; the installed launch produced repeated lifetime and invalid-pointer errors.

## Provenance

Fresh comparison with `upstream-tle/master` at `7e584bc9` separates the inherited defects from the Extended regression:

- Upstream calls `ShouldFollowLeader` only inside the custom-phase branch, so vanilla followers do not use its job-level synchronization path.
- Upstream lockstep uses `WrapPhase(group.m_MasterPhase, followerPhaseCount)`, which copies a local phase number without mapping equivalent physical movements.
- Extended commit `171d6fd` routed vanilla groups through a new multi-pass coordination path, but added the starvation-prone cross-member priority merge and the unsafe `Allocator.TempJob` lifetime.

The corrected implementation must therefore repair the inherited vanilla synchronization and movement-mapping assumptions while removing the regressions introduced by the first Extended fix.

## Compatibility constraints

- Preserve the `C2VM.TrafficLightsEnhancement` assembly, namespace, mod id, and save-facing component layouts.
- Do not add serialized fields or require a save migration.
- Keep local TSP suspended for every grouped intersection, including the leader.
- Preserve existing custom-phase and green-wave behavior except where they consume the shared movement map.
- Do not overwrite a follower's configured phase definitions merely to make its local phase numbers match the leader.

## Considered approaches

### Automatic transient movement mapping

Build a runtime-only map from each leader phase to the follower phase that controls the most equivalent physical approaches. Use the map for lockstep synchronization and inverse demand remapping.

This preserves local junction configuration, requires no player adjustment, and supports junctions whose phase numbers differ. It is the selected approach.

### Manual phase swapping

Expose a player control that assigns a follower phase offset. This is simpler internally but makes correctness depend on player diagnosis, adds UI and localization work, and cannot represent every non-cyclic mapping.

### Copy leader phase definitions into followers

Renumber follower movements by copying the leader's configuration with direction matching. This can work for similar layouts, but it can overwrite intentional follower behavior and is unsafe when topology differs.

## Runtime movement map

Add a transient, non-serialized per-member phase map. Each entry maps one leader signal group to one follower signal group.

The map is derived from live lane-signal and lane-geometry data:

1. For every signal group, build a movement signature from the incoming motor-vehicle and track approaches active in that group.
2. Treat opposite travel directions on the same road axis as equivalent for corridor coordination.
3. Match leader and follower phases by maximum signature overlap. Prefer exact axis and lane-type matches; use deterministic tie-breaking.
4. Record a mapping only when every required leader phase has a valid follower phase.

The map is rebuilt when a member is added, leadership or pattern changes, lane topology is updated, or the cached signal-group counts no longer match live data. It is reconstructed after loading because it is not save data.

If a complete mapping cannot be established, the runtime must not claim raw-number lockstep. The affected follower runs its local controller independently and diagnostics report that no movement mapping is available. This fail-closed behavior is safer than displaying contradictory physical signals while reporting synchronization.

## Lockstep data flow

For a mapped vanilla group:

1. Collect each member's locally winning vanilla demand before resetting its lane-signal request fields.
2. Convert follower demand into leader phase space using the inverse movement map.
3. Merge the locally winning requested masks from all members without allowing one member's absolute priority to erase another member's winning phase.
4. Advance the leader once using the merged group demand.
5. Publish the leader's same-tick state.
6. Translate the leader's current and next phases through each follower's movement map.
7. Copy state and timers to the mapped follower and refresh its lane signals and traffic-light objects.

Within one intersection, existing vanilla priority arbitration remains unchanged. Across intersections, every member's local winner participates, so persistent traffic at one member cannot permanently hide another member's queued phase.

Custom-phase followers retain their current custom priority and flow calculations. Green-wave timing continues to apply its delay after movement mapping rather than treating phase numbering as movement identity.

## Job memory and scheduling

Remove the separate active-group discovery map and job. A grouped member already identifies its group and leader, and the leader's live traffic-light data supplies the phase count.

Keep collection, leader update, and follower synchronization strictly dependency-ordered. Coordination containers must use an allocator valid for however many rendered frames the chain needs on a large city; they must not use `Allocator.TempJob` when disposal may occur more than four frames later.

Prefer purpose-specific job data and declared read/write access over new `NativeDisableContainerSafetyRestriction` fields. Dispose all coordination containers from the final follower dependency.

## Diagnostics

Existing selected-junction diagnostics remain opt-in. For a mapped follower, the phase-mapping row reports both the leader phase and translated follower phase. For an unmapped follower, it reports that movement mapping is unavailable rather than displaying a misleading identity mapping.

No new user-facing settings or localization keys are required.

## Test-first coverage

Pure policy tests will cover:

- swapped two-phase movement signatures map leader `G1` to follower `G2`;
- aligned signatures retain identity mapping;
- ambiguous or incomplete signatures reject mapping;
- follower demand remaps through the inverse movement map;
- differently prioritized member winners remain represented after group merge;
- phase selection advances to another requested group instead of starving it.

ECS/source-contract tests will cover:

- vanilla lockstep uses the movement map for current and next phases;
- an unmapped follower does not use raw-number synchronization;
- the active-group discovery pass and map are removed;
- coordination maps do not use `Allocator.TempJob`;
- local TSP remains suspended for grouped leaders and followers.

Regression verification includes the pure, ECS, and serialization test projects plus a non-deploying Release build while the game is running.

## Live acceptance test

After the game is closed and a verified Release build is installed:

1. Recreate or re-enable the same two-junction vanilla group.
2. Confirm the diagnostic mapping translates the leader's corridor phase to the follower's equivalent corridor phase.
3. Observe at least two complete phase changes.
4. At each sampled frame, verify equivalent corridor movements display the same signal state and leader/follower timers match.
5. Confirm both phases receive service under queued demand.
6. Confirm the fresh `Player.log` contains no new coordination-related `JobTempAlloc` or invalid-pointer errors.

The fix is not complete until this game-side test passes.
