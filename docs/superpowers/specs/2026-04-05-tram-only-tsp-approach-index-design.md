# Tram-Only TSP Approach Index Design

> Replaces the track early-detection portion of `docs/superpowers/specs/2026-04-04-tsp-no-grace-early-approach-design.md` after in-game diagnostics showed that the current lane-object strategy cannot detect approaching trams on the repro intersection.

## Goal

Make local Transit Signal Priority request trams early enough that a simple tram crossing can begin switching before the tram reaches the stop line, without introducing a per-junction transit scan that scales poorly in larger cities.

## Scope

This design changes only local TSP request detection for tram and track-based requests.

It does:
- replace lane-object-based track early detection with a tram-entity approach index
- preserve the current no-grace local request decay and short effective request horizon behavior
- preserve petitioner-based fallback when no early tram request is available
- keep the current downstream target-group and phase-override logic unchanged
- keep the debugging surface in the TSP panel, updated to reflect the new track detection model
- prefer the simplest safe collection model in this slice, even if future optimization later parallelizes it

It does not:
- redesign bus early detection in this slice
- add new user-facing settings
- redesign grouped propagation
- change signal change-state durations
- remove petitioner fallback for trams

## Reality Check

The recent in-game debugging pass established three facts:

1. The traffic-light simulation already runs on an interval of four simulation frames in `PatchedTrafficLightSystem`, so this feature does not need to update on every game tick to stay current.
2. The current track early-detection plan is invalid for the repro intersection. The panel showed `Fresh petitioner request` together with `track[signal=no-objects, approach=no-objects, upstream=none]`, which means the track path fails before any tram entity or threshold logic is even evaluated.
3. Because the current approach fails at the data-source level, threshold tuning and release-latch tuning cannot solve the "tram still stops first" problem.

This means the next solution must change the data source used for track early detection, not just retune the current strategy.

## Approaches Considered

### 1. Keep the lane-object strategy and tune thresholds

This is the smallest code change, but it is no longer credible. The latest diagnostics showed that the relevant track lanes expose no `LaneObject` buffers at all in the repro. That makes the current architecture incapable of detecting the tram early, regardless of thresholds.

Recommendation: reject.

### 2. Scan all trams separately for each TSP junction

This would likely work functionally, because it can ignore missing lane-object buffers and inspect tram runtime state directly. However, it scales as the number of trams multiplied by the number of TSP junctions evaluated in that update. In a tram-heavy city, that becomes the wrong performance shape.

Recommendation: reject.

### 3. Build one tram approach index per traffic-light update and let junctions do cheap lookups

This changes the track early-detection source from lane objects to actual tram runtime data, while keeping the performance shape bounded. The cost becomes one pass over tram entities per traffic-light update, plus cheap lane-key lookups while evaluating junctions.

Recommendation: choose this approach.

## Product Decisions

- This slice is tram-only for early detection. Bus early detection is treated as untrusted and remains out of scope until there is real evidence for a reliable bus data source.
- Trams should continue to use petitioner fallback if no qualifying early approach is found.
- Performance should scale with `number of trams + number of TSP junction lane lookups`, not `number of trams * number of TSP junctions`.
- Existing local TSP lifecycle behavior that already matches the user's intent, especially no extra post-clear release grace and the short effective request horizon, should stay in place.
- The first implementation should prefer a synchronous prepass in `PatchedTrafficLightSystem.OnUpdate` over a second collection job, because that keeps job safety and data ownership simple while the new architecture is being proven.

## Design

### 1. Build a temporary tram approach index once per traffic-light update

At the start of each `PatchedTrafficLightSystem` simulation update, collect a temporary snapshot of active tram approach positions from tram runtime entities rather than from lane buffers.

The collector should:
- query tram entities that have the runtime data already needed for approach detection
- read `TrainCurrentLane`, `TrainNavigation`, and `PublicTransport`
- ignore stopped or suppressed trams
- record the front and rear lane positions for qualifying trams

The collector query should target entities with:
- `TrainCurrentLane`
- `TrainNavigation`
- `PublicTransport`

That query may include non-tram rail vehicles. Tram filtering should therefore happen during collection by checking whether the front lane, rear lane, or both resolve to a tram track lane through the existing track lane metadata (`TrackLaneData.m_TrackTypes`).

The index should be keyed by lane entity and contain enough information to answer one question efficiently:

> "Is there a moving tram on this lane far enough along the lane to justify an early TSP request?"

The index lifetime is one traffic-light update only. It should be rebuilt on the same cadence as the traffic-light system rather than on every simulation tick.

### 2. Use an explicit lane-keyed best-sample map

The index shape should be explicit in this slice:
- key: `Entity` lane
- value: highest normalized curve position observed on that lane during the current traffic-light update

This should be implemented as a temporary `NativeParallelHashMap<Entity, float>` that supports read access from the traffic-light update job. The intended shape for the first implementation is a single-value lane map rather than a multimap.

A tram may contribute up to two lane samples in the same update:
- one for `TrainCurrentLane.m_Front.m_Lane`
- one for `TrainCurrentLane.m_Rear.m_Lane`

If multiple trams contribute samples for the same lane, the stored value should keep the highest curve position. That gives the lookup exactly the value the TSP threshold check cares about, while keeping the collection compact.

### 3. Use a lane-keyed tram index instead of `LaneObject` for tracks

Track early detection in `TransitSignalPriorityRuntime` should stop probing `LaneObject` buffers entirely.

For each eligible track-controlled approach, the runtime should resolve:
- the signaled lane entity
- the approach/source lane entity from `ExtraLaneSignal.m_SourceSubLane` when present
- the immediate upstream tram lane when one exists

The runtime should then check the tram index for those lane keys instead of trying to read `LaneObject` buffers from the lanes themselves.

If `ExtraLaneSignal.m_SourceSubLane` is absent, the existing fallback remains valid:
- the approach lane resolves to the signaled lane
- the approach lookup therefore reuses the signaled lane key rather than inventing a separate fallback path

The early request decision should be based on the best qualifying sample found on:
- the approach lane first
- the immediate upstream lane second

The signaled lane should remain useful as a diagnostic sanity check, but the real early-trigger behavior should be driven by the physical approach lane and the immediate upstream lane.

### 4. Keep early track evaluation lane-local and threshold-based

The current threshold model remains a reasonable starting point once the data source is corrected:
- the approach lane should trigger with a relatively early threshold
- the upstream lane should require the tram to be near the end of that upstream segment before it can refresh the request

This preserves the intent of the earlier design:
- actual approach lane can start the switch early
- upstream lane is allowed to help, but only close to handoff

The thresholds should remain implementation constants in this slice. User-facing tuning is a separate future feature.

### 5. Leave buses on petitioner fallback only

This slice should stop pretending bus early detection is validated.

For now:
- trams use the new tram approach index plus petitioner fallback
- buses use petitioner fallback only

That keeps the rewrite honest and prevents this slice from expanding into a second unverified subsystem.

### 6. Keep the panel diagnostics, but update them to match the new architecture

The recent debugging instrumentation was useful and should stay.

The track-specific debug output should continue to report the three lane perspectives:
- signaled lane
- approach lane
- upstream lane

But the meanings should change from lane-object probe results to tram-index lookup results. The new states should reflect things like:
- no tram samples for that lane
- tram sample found but below threshold
- tram sample matched on approach lane
- tram sample matched on upstream lane

The debug contract should make those outcomes explicit rather than implicit. The expected track-facing states in this slice are:
- `NoTramSamples`
- `BelowThreshold`
- `MatchOnApproachLane`
- `MatchOnUpstreamLane`

This keeps the next debugging loop grounded in real runtime evidence instead of inference.

## Architecture

### Collection Step

Add a tram collection step that runs once per traffic-light update before junction evaluation consumes TSP requests.

The preferred first implementation is deliberately conservative:
- cache a dedicated rail-transit query on `PatchedTrafficLightSystem`
- allocate a temporary lane-keyed native map with `Allocator.TempJob`
- fill that map synchronously on the main thread inside `PatchedTrafficLightSystem.OnUpdate`
- then schedule `UpdateTrafficLightsJob` with the map passed in as a `[ReadOnly]` field
- dispose the temporary map by chaining disposal to the returned job dependency

This avoids adding a second writer job and avoids concurrent merge logic while the feature is still being validated. It also makes the "highest curve position wins" rule straightforward to implement.

The collection must still be safe to read from the parallel traffic-light update job, which is why the map allocator and disposal path must match job usage.

### Runtime Consumption

`TransitSignalPriorityRuntime` should be reshaped so that track early detection reads from the temporary tram approach index, while the bus path remains petitioner-only.

That means the runtime request builder clearly separates:
1. track early detection from tram runtime index
2. petitioner fallback

The current lane-object logic for buses can remain only where it is still actually used and verified. It should no longer be the basis of track detection.

## Data Flow

1. `PatchedTrafficLightSystem.OnUpdate` starts a traffic-light simulation update.
2. A synchronous tram collection step scans the rail-transit query once, filters for tram track samples, and builds a temporary lane-keyed approach index for that update.
3. `UpdateTrafficLightsJob` evaluates TSP-enabled junctions in parallel.
4. `TransitSignalPriorityRuntime` resolves the signaled lane, approach lane, and immediate upstream lane for each eligible track approach.
5. The runtime checks the temporary tram approach index for those lane keys.
6. If a qualifying early tram sample is found, the runtime emits a fresh local track request.
7. If not, the existing petitioner fallback remains in effect.
8. Existing preemption, target-group selection, and phase-override logic continues unchanged.

## Performance Characteristics

This design intentionally avoids both of the bad performance shapes:
- no "scan all trams for each junction"
- no "scan all vehicles every simulation tick"

Instead, the cost becomes:
- one pass over tram entities per traffic-light system update
- plus a small number of lane-key lookups per eligible TSP track lane during junction evaluation

Because the traffic-light system already updates on a fixed interval of four simulation frames, this collection work is naturally bounded to the same cadence rather than running continuously.

## Guardrails

- Only moving, non-suppressed trams should enter the approach index.
- Null lane entities must be ignored.
- An update with zero qualifying trams must produce an empty index and still behave correctly.
- If a lane has no tram samples in the index, the runtime must return no early request rather than guessing.
- Petitioner fallback must remain intact so the feature degrades safely instead of disappearing.
- Bus early detection should not be expanded in this slice.
- The temporary native collection used for the tram approach index must be disposed when the update dependency completes.
- If the track rewrite makes `TransitApproachEntityResolver` unused, it should be removed as cleanup in this slice rather than left behind as dead code.

## Testing

Add or update tests for:
- approach-lane tram sample produces an early local track request
- upstream-lane tram sample produces an early request only when it has crossed the stricter upstream threshold
- no tram samples falls back to petitioner detection
- bus requests remain petitioner-only in this slice
- no-grace local request decay and short effective request horizon behavior remain unchanged

If ECS-heavy collection logic is awkward to unit-test directly, factor the non-ECS parts into helpers that accept:
- lane keys
- sampled curve positions
- threshold configuration

Then cover the selection logic there and keep the ECS-specific collection code thin.

## In-Game Verification

The clean tram-loop repro should verify the following:
- `Debug Source` becomes `Fresh early request` before the tram reaches the stop line
- the debug state for tracks reports a tram-index match instead of `no-objects`
- the tram no longer needs to stop at the light in the simple crossing repro
- the request still clears normally after the tram passes, without recreating the "never releases" regression

## Risks

- Adding a new temporary lane-keyed collection into the traffic-light simulation update increases implementation complexity and job wiring risk.
- If the lane entities stored in `TrainCurrentLane` still do not align with the approach/upstream lane entities resolved by TSP, the index will still miss; however, the updated diagnostics should make that mismatch visible quickly.
- If the collector stores too much per-tram data, the temporary allocation could become larger than necessary. The data shape should stay minimal and lane-focused.
- If petitioner fallback and early detection refresh each other incorrectly, request lifecycle bugs could reappear; the existing local request tests should protect against that.
- `ReleaseGraceTicks = 6` is already behaviorally dead for the user-desired path; removing the leftover constant is optional cleanup, not part of this design's required behavior.

## Recommendation

Ship the next TSP redesign as a tram-only architectural correction:
- replace track lane-object probing with a temporary tram approach index built from tram runtime data
- keep buses on petitioner fallback only
- preserve existing no-grace and short-horizon request lifecycle behavior
- keep the improved debug panel so the next in-game repro can validate the new path directly

This is the most grounded design because it directly addresses the actual failure mode proven by the latest diagnostics: the current track early-detection data source is absent, so the architecture must change before the timing can improve.
