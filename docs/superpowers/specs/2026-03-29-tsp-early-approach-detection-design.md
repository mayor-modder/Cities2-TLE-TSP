# TSP Early Approach Detection Design

## Goal

Make Transit Signal Priority noticeably more effective by detecting approaching transit vehicles slightly earlier than the current lane-signal petitioner path, so lights can begin responding before a tram or bus has already slowed at the intersection.

## Scope

This design changes only local TSP request detection for standalone intersections.

It does not:
- re-enable TSP on grouped intersections
- redesign the TSP request lifecycle or phase override logic
- add new user-facing settings in this slice
- change non-TSP traffic-light behavior

## Problem Summary

The current TSP runtime only builds a fresh request once a sublane's `LaneSignal.m_Petitioner` is already set. In practice this means detection happens very late, often when a tram is already at the stop line or already slowing for the signal. That makes TSP harder to notice and reduces its ability to smooth transit movement.

This is especially visible on tram corridors where the vehicle has a dedicated right-of-way and should ideally receive a green slightly before arrival.

## Design

### Overview

Add an early-approach detection path that runs before the existing petitioner-based request builder.

The new detection path should:
- look for an eligible transit vehicle on an approach lane to the selected junction
- require that the vehicle is moving above a small minimum speed threshold
- produce the same request shape used by the current TSP runtime

If early detection finds no valid vehicle, the existing petitioner-based logic remains the fallback path.

### Supported Vehicles

Early detection applies to the same vehicle classes already supported by TSP:
- trams and track-based transit on track lanes
- buses on public-only car lanes

Bus detection remains restricted to public-only lanes. No general mixed-traffic bus detection is added.

### Movement Rule

Early detection must only trigger for vehicles that have already left a stop and are moving toward the junction.

For version 1, this is enforced with a simple speed threshold rather than explicit stop-geometry analysis. This keeps the feature localized and predictable while avoiding false triggers from vehicles dwelling at a stop near the intersection.

### Detection Priority

Request building should prefer sources in this order:
1. early moving-vehicle detection
2. existing lane-signal petitioner detection
3. existing latched request refresh path

This preserves the current TSP lifecycle while giving the system a chance to react earlier.

### Request Semantics

Early-detected requests should feed into the current request lifecycle unchanged:
- same target-signal-group selection rules
- same request horizon and decay behavior
- same green-hold and phase-advance override logic
- same grouped-intersection runtime guardrails

This means the feature improves *when* TSP sees a vehicle, not *how* TSP behaves once a request exists.

## Architecture

### Runtime Changes

The main work belongs in the TSP runtime request builder.

Add a small helper that attempts to detect an eligible approaching transit vehicle from the available lane/entity context before relying on `LaneSignal.m_Petitioner`.

The existing petitioner-based logic should remain as a separate fallback helper. `TryBuildFreshRequest` should then orchestrate both helpers in a clear order.

### Logic Boundaries

Keep pure-policy logic in the logic project where practical, but do not force ECS-heavy vehicle detection into the pure library. The movement-based early detection is fundamentally simulation/ECS work and should remain in the simulation runtime layer.

The logic project can continue owning request eligibility decisions by lane type and user settings.

## Data Flow

1. TSP runtime checks whether the junction is runtime-eligible.
2. Runtime tries to build an early request from an approaching moving transit vehicle.
3. If no early request is available, runtime falls back to the existing petitioner-based request builder.
4. Fresh request, if any, is merged with the existing latched request through the current preemption policy.
5. Downstream TSP override logic continues unchanged.

## Error Handling and Guardrails

- If approach detection cannot confidently identify an eligible moving transit vehicle, it must return no request and let the existing petitioner path handle the junction.
- If the detected vehicle cannot be mapped to a valid target signal group, no early request should be produced.
- Stopped vehicles must not trigger early TSP.
- Grouped intersections remain runtime-ineligible for local TSP, even if early detection finds a valid vehicle.

## Testing

Add tests for:
- moving tram on an approach lane builds an early request
- moving bus on a public-only approach lane builds an early request
- stopped transit near the junction does not build an early request
- existing petitioner-based detection still works when early detection does not apply
- grouped intersections still reject local runtime TSP

If full ECS integration tests are too heavy for this slice, factor any non-ECS decision logic into small helpers that can be covered with focused unit tests.

## Recommendation

Ship this as a conservative detection improvement with no new settings first.

If the feel is good in-game, future follow-up options could include:
- exposing the early-detection threshold as a tuning parameter
- differentiating bus and tram thresholds
- refining stop-awareness for near-side transit stops
