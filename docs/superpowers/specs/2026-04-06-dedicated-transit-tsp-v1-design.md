# Dedicated Transit TSP v1 Design

Date: 2026-04-06

## Summary

Traffic signal priority v1 is a dedicated-transit feature, not a generic road-priority system.
It supports:

- Trams on tram/track lanes
- Actual buses on bus-only lanes

Both sources share the same request lifecycle and the same aggressive built-in signal preemption behavior that now works for trams. The feature remains a single Transit Signal Priority capability in the UI, with separate per-junction toggles for tram requests and bus requests. The master TSP toggle stays default-off. Only newly created or previously unset settings start with both source toggles enabled.

## Goals

- Preserve the currently working tram priority behavior
- Add bus priority for actual buses using bus-only lanes
- Keep the implementation focused on dedicated transit lanes
- Remove or quarantine unrelated generic TSP code where it does not support the dedicated-transit model

## Non-Goals

- General road-transit priority for arbitrary public vehicles
- Queue-aware bus detection through blocked traffic
- Broad coordination-driven propagation as part of transit priority v1
- Support for non-dedicated mixed traffic lanes

## Product Scope

Transit Signal Priority v1 means that dedicated transit approaches can interrupt the normal built-in traffic light state machine so the requested transit phase arrives in time to be useful.

Supported request sources:

- Tram/track requests from tram track approaches
- Bus requests from actual buses on bus-only car lanes

Unsupported request sources:

- Cargo or other non-bus vehicles using a bus-only lane
- Ordinary car lanes
- Mixed traffic transit service on non-dedicated lanes

Known acceptable limitation for v1:

- If a bus is blocked behind other vehicles and cannot reach the detection zone, it may fail to trigger priority. This is acceptable for the first release.

Explicit bus v1 request boundary:

- Bus v1 may ship without a true road-lane early-detection path.
- The minimum accepted bus trigger is petitioner-based request generation on bus-only lanes, plus validation that the requesting vehicle is an actual bus.
- If a dedicated-lane early-detection path for buses proves straightforward, it is a bonus rather than a release requirement.
- Bus v1 is successful if validated bus requests can still produce useful aggressive preemption at representative bus-only-lane junctions.

## Architecture

### 1. Lane Eligibility

Each signal-controlled sublane resolves to an approach lane. A lane is eligible for dedicated transit TSP only if it is:

- A tram track lane, or
- A bus-only car lane

This keeps the feature tied to dedicated transit infrastructure rather than broad vehicle classes.

### 2. Vehicle And Source Validation

Lane eligibility does not automatically create a request.

Requests are created only when the runtime can validate the correct transit source:

- Tram requests require tram presence through the existing tram approach index and track probe path
- Bus requests require an actual bus vehicle on a bus-only lane

This replaces the earlier broad `PublicCar` meaning with a narrower, vehicle-validated bus model.

Compatibility rule for v1:

- Persisted field names, runtime enum names, and serialized identifiers such as `m_AllowPublicCarRequests` and `TspSource.PublicCar` stay unchanged in this phase.
- v1 changes behavior and meaning, not storage names.
- Renaming or migrating those identifiers is explicitly deferred until after the dedicated tram/bus path is stable.

### 3. Shared Request Lifecycle

Once validated, tram and bus requests use the same lifecycle:

- Early request when dedicated approach detection succeeds
- Petitioner fallback when the lane signal has a petitioner
- Immediate stale-request clearing when a fresh request no longer exists
- Shared target-group selection
- Shared aggressive preemption into the requested signal group when the normal built-in controller would switch too late

The aggressive built-in preemption path remains the core mechanism because it is the first approach that produced real working priority at the known tram repro.

### 4. Shared UI Model

The feature stays under one Transit Signal Priority section with:

- `Enable Transit Signal Priority`
- `Allow Tram and Track Requests`
- `Allow Bus Lane Requests`

The master TSP toggle remains default-off. Newly created or previously unset TSP settings start with both tram and bus source toggles enabled. Re-enabling the master TSP toggle must not silently reset a user-disabled source toggle. Users may disable either source independently per junction.

## Cleanup Direction

### Keep

- Tram approach indexing
- Connected-edge and upstream track probing that supports the working tram path
- Petitioner fallback
- Request refresh and immediate stale clearing
- Aggressive built-in preemption for dedicated transit requests
- Unified TSP settings and debug surface where it serves the dedicated-transit model

### Remove Or Simplify

- Dead locals and unused branches
- Redundant road-transit early-detection stubs that currently cannot produce a request
- Generic `PublicCar` request behavior where it implies any public-only lane traffic may request priority
- Tests that only protect behaviors outside the dedicated-transit model

### Quarantine For Later

Code related to broader generic TSP ideas that may still be interesting later, but should not remain half-active in the working v1 path, should be preserved on a backup branch instead of kept as live ambiguity in the local repo.

This especially applies to:

- Broader propagation and coordination-oriented transit request behavior
- Non-dedicated road-transit priority ideas
- Any old code paths that cannot be justified by the dedicated tram/bus v1 model

Grouped-junction end state for this phase:

- Grouped and propagated TSP behavior is not part of dedicated transit v1.
- Active grouped request generation and grouped propagation paths should be hard-disabled or removed from the live runtime in this phase unless a specific piece is still required by the dedicated tram/bus path.
- Grouped-TSP tests should be updated to match that end state instead of preserving runtime eligibility as an active expectation.
- If any grouped code is worth keeping for future work, preserve it on a backup branch rather than as live ambiguity in the shipped path.
- The existing grouped-propagation setting remains serialized for compatibility in this phase, but it becomes inactive in the live v1 runtime and should be removed from the active UI surface if that can be done without introducing migration risk.

## Bus v1 Design Boundary

Bus support in v1 should mirror the tram experience as closely as practical without inventing new complexity.

Behavior:

- Actual bus on a bus-only lane can request priority
- That request can use the same practical preemption path as trams
- Non-bus vehicles on the same lane must not request priority
- Petitioner-based validated bus requests are sufficient for v1 even if buses do not yet have a dedicated early-detection path

Implementation bias:

- Reuse the proven dedicated-lane TSP pipeline where possible
- Avoid reviving broad generic `PublicCar` semantics
- Extend validation to distinguish buses from other bus-lane occupants

## Verification

Required verification for the feature work that follows this spec:

- Existing tram repro still works with immediate useful preemption
- Actual bus on a bus-only lane can trigger priority at a representative junction
- Cargo or other non-bus vehicles in a bus-only lane do not trigger priority
- TSP settings still expose one feature with separate tram and bus toggles
- No removed code paths were still required by the dedicated tram/bus model
- Settings compatibility is preserved even though `PublicCar` behavior is narrowed to actual buses on bus-only lanes
- Grouped propagation is no longer treated as an active runtime path for this release

## Risks

- Bus entity identification may not line up cleanly with the current lane-based scan model
- Existing `PublicCar` code may be more entangled with unrelated systems than it first appears
- Over-pruning generic TSP code could accidentally remove helpers still needed by the working tram path

## Recommended Implementation Direction

1. Audit the active dedicated-transit path and identify truly dead or out-of-scope generic TSP code.
2. Simplify the request model from broad `PublicCar` semantics toward explicit bus-on-bus-lane validation.
3. Keep the proven aggressive preemption path as the common runtime behavior for both trams and buses.
4. Add focused verification for actual bus triggers and non-bus rejection.
5. Preserve exploratory or future-facing generic TSP ideas on a backup branch rather than in the live v1 path.
