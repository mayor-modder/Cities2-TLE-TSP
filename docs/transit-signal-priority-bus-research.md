# Transit Signal Priority for buses Research

This document records research and follow-up notes for extending Tram Signal
Priority (TSP) toward transit signal priority for buses.

## Current State

Transit Signal Priority for buses now exists as a separate off-by-default player control with
a soft MVP runtime path.

- `TspSource.PublicCar` exists as a reserved source.
- `m_AllowPublicCarRequests` exists in settings and serialization.
- Runtime normalization and UI toggling keep bus requests disabled unless the
  separate Transit Signal Priority for buses control is enabled.
- Pure decision tests cover bus request ordering and keep tram requests ahead of
  bus requests.

The tram path builds `TramApproachIndex` from rail public transport vehicles
using `PublicTransport`, `TrainNavigation`, and `TrainCurrentLane`. Fresh
request production scans signaled sublanes, resolves source lanes, and only
builds requests when the resolved approach lane is a tram track.

## Reusable Pieces

Bus priority can reuse much of the TSP pipeline:

- saved settings component shape
- latched request component
- request expiry policy
- selected-intersection diagnostics and trace structure
- signal group hold/override application
- exclusive pedestrian phase protection
- custom phase integration

The lane and signal group mapping also has useful inherited support. Bus-only
lanes are represented through `CarLaneFlags.PublicOnly`, and custom phase masks
track public-car lane groups separately from general car lanes.

## Remaining Pieces

Bus detection uses a road-vehicle approach index, not just public-only lane
detection. A bus in a mixed car lane can request that lane's signal group,
while a bus-only lane can use the public-car lane mask where custom phases
split it.

Likely ECS data sources to investigate:

- `PublicTransport`
- `PublicTransport.m_State` with `Boarding`, `Arriving`, and `RequireStop`
- `PassengerTransport`
- `CarCurrentLane`
- `CarNavigation`
- `CarNavigationLane`
- `Moving`
- `PrefabRef`
- `PublicTransportVehicleData.m_TransportType == Bus`
- `CurrentRoute`
- route stop entities with `BusStop` and `TransportStop`
- route/vehicle buffers such as `RouteWaypoint`, `RouteVehicle`,
  `RouteLane`, and `VehicleTiming`

`ExtraTypeHandle` exposes the road-vehicle state needed for bus detection and
diagnostics: `PassengerTransport`, `CarCurrentLane`,
`CarNavigation`, `CarNavigationLane`, `Moving`, and
`PublicTransportVehicleData`.

Pure policy is source-generalized enough for the soft bus MVP. Request
construction, request combination, phase scoring, latching, current-group hold,
and overrides can account for bus requests, while aggressive preemption remains
tram-only.

## Stop-Aware Suppression Policy

Bus priority should be stricter than tram priority around stops. A tram with
`Arriving` or `RequireStop` can still be worth detecting because tram stops are
often integrated with the track approach. A bus approaching a near-side stop may
board passengers before the signal, so requesting green before boarding would
hold cross traffic for no benefit.

Available ECS data from `Game.dll` reflection:

- `Game.Vehicles.PublicTransport` has `m_State`, `m_TargetRequest`,
  `m_DepartureFrame`, `m_PathElementTime`, `m_MaxBoardingDistance`, and
  `m_MinWaitingDistance`.
- `Game.Vehicles.PublicTransportFlags` includes `Boarding`, `Arriving`, and
  `RequireStop`.
- `Game.Prefabs.PublicTransportVehicleData.m_TransportType` identifies buses
  with `TransportType.Bus`.
- `Game.Vehicles.CarCurrentLane` exposes current lane, change lane, curve
  position, lane flags, lane position, distance, and change progress.
- `Game.Routes.TransportStop` carries stop flags/loading data, and bus stops can
  be identified by the marker component `Game.Routes.BusStop`.
- Route context is available through route components/buffers such as
  `CurrentRoute`, `RouteWaypoint`, `RouteVehicle`, `RouteLane`, and
  `VehicleTiming`.

Pure stop suppression is now captured by
`BusPrioritySuppressionPolicy.EvaluateStopSuppression(...)`:

Today the runtime always passes `BusStopRelation.Unknown`; near-side and
far-side stop classification is tracked in #35, and lane-change semantics are
tracked in #36. The known-stop cases below describe the intended policy once
that classifier exists, not behavior the current runtime can already observe.

- `Boarding` always suppresses bus priority.
- `Arriving` or `RequireStop` suppresses priority for a known near-side stop
  before the signal.
- `Arriving` or `RequireStop` does not suppress priority for a known far-side
  stop after the signal; helping the bus cross the junction can still be useful.
- `Arriving` with unknown stop relation suppresses conservatively until
  diagnostics can classify the stop.
- `RequireStop` alone does not suppress a moving bus on a dedicated bus-only
  approach. Live diagnostics showed this flag on buses at a junction that is
  not near any stop, so treating it as a near-side-stop signal blocked the
  easiest useful bus-priority case.
- `RequireStop` with unknown stop relation still suppresses mixed-lane buses
  and stopped bus-only samples until diagnostics can classify the stop.
- A queued bus with no stop flags is not stop-suppressed by this policy. Runtime
  detection may still require movement/position thresholds before creating a
  request, but queueing is not the same as boarding.

Runtime implementation should continue refining stop relation before expanding
bus request behavior:

- **Near-side stop:** suppress while `Arriving`, `RequireStop`, or `Boarding`.
- **Far-side stop:** allow approach priority unless the bus is actually
  `Boarding`.
- **Stopped behind queue:** do not suppress solely because the bus is stopped;
  use distance/curve thresholds and request expiry to decide whether it is close
  enough to benefit.
- **Unknown stop relation:** allow moving `RequireStop` buses only on dedicated
  bus-only approaches; suppress `Arriving`, mixed-lane `RequireStop`, and
  stopped `RequireStop` samples, then report the unknown relation in
  diagnostics.

## Diagnostics and Soft MVP

When the off-by-default TSP diagnostics option is enabled, `BusApproachIndex`
scans public-transport road vehicles with
`PublicTransportVehicleData.m_TransportType == Bus` and records
current/change-lane samples. This scan is intentionally independent of tram TSP
approach-index eligibility, so a selected bus-only candidate intersection can
still produce bus diagnostics even when no tram priority request is possible.
The selected junction diagnostics can report:

- indexed bus lane count
- whether a hit came from the signaled lane, resolved approach lane, or
  connected approach fallback
- bus-only versus mixed lane structure via `CarLaneFlags.PublicOnly`
- lane-change progress, speed, public-transport state, and vehicle lane flags

The Transit Signal Priority for buses MVP can create `TransitSignalPriorityRequest` values
when its separate player control is enabled. It is intentionally soft: bus
requests may hold an already-serving green or select their target group at
normal transition points, but trams outrank buses and buses do not use
tram-style aggressive minimum-green shortening. Diagnostics still need
playtesting against real saves before bus stop and lane-change semantics are
considered mature.

No separate bus aggressive-preemption suppression diagnostic is exposed. A bus
request can be outranked by tram priority, but buses do not attempt the
tram-only aggressive preemption path, so such a diagnostic would not represent a
distinct bus decision today.

## Edge Cases

Near-side stops are the biggest policy risk. A bus approaching a stop before the
signal should not request or hold green too early if it is about to board
passengers. The tram index suppresses boarding samples but allows
`Arriving`/`RequireStop`; buses may need stricter stop-aware handling.

Mixed lanes require vehicle-level detection. A lane marked for regular cars can
still carry a bus, and bus priority should follow the actual bus lane/current
route, not only the lane type.

Lane changes matter. `CarCurrentLane` can include both current and change-lane
state, and choosing the wrong lane near an intersection could select the wrong
signal group.

Congestion also matters. A stopped bus far behind a queue may not deserve
priority until it is close enough, latched, or otherwise confirmed to benefit
from priority.

## MVP Recommendation

Use and refine a soft bus-priority MVP:

- keep bus requests behind an explicit, off-by-default setting
- keep tram requests ahead of bus requests
- allow buses to hold an already-serving green
- allow buses to select the target group at normal transition points
- do not use tram-style aggressive minimum-green shortening for buses in the
  first version

That keeps bus priority useful while avoiding the most disruptive cases until
stop and lane-change behavior is better understood.

## Naming Decision

Keep **Transit Signal Priority** as the player-facing feature name, with
separate source controls for trams and buses.

The code can keep internal `TransitSignalPriority*` names because the saved
component shape and pure policy layer are intended to support more than one
transit source over time. Separate controls make the behavior easier to
explain, keep existing tram settings stable, and let buses stay disabled by
default while diagnostics and semantics mature.

Localization impact: keep new base strings in `Locale.json` first. Do not
rewrite non-English locale files by hand for this rename/split; let the normal
translation workflow handle new strings after the English UI is stable.

## Staged Plan

1. Add pure policy tests for `PublicCar` eligibility and source ordering.
2. Prototype bus approach diagnostics that report bus lane hits. (Done.)
3. Integrate soft bus fresh request production from car-lane bus samples. (Done
   for MVP.)
4. Add a separate bus settings/control surface while keeping the existing tram
   labels and saved settings stable. (Done.)
5. Refine stop-aware suppression, lane-change handling, mixed-lane regression
   cases, and grouped-intersection semantics through playtesting.

## Follow-Up Work

Suggested follow-up issues:

- Refine bus request production from car-lane bus samples.
- Refine stop-aware bus suppression rules with real-save examples.
- Playtest separate tram and bus controls with mixed and bus-only approaches.
