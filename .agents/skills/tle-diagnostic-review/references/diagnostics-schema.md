# TLE TSP Diagnostics Schema Notes

The JSONL trace is written by `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs` from selected-intersection diagnostics. Each line is one JSON object. Repeated identical selected-entity summaries are deduped by the mod before writing.

## Top-Level Fields

- `timestampUtc`: UTC write time.
- `simulationFrame`: game simulation frame.
- `selectedEntity`: selected intersection entity with `index` and `version`.
- `signalConfiguration`: saved signal configuration summary.
- `trafficGroup`: traffic group membership and coordination summary.
- `laneSignals`: selected intersection sublane signal masks and current/next/requested group activity.
- `summary`: compact in-game diagnostics summary.
- `trafficLights`: current signal state, current group, next group, timer, and signal group count.
- `request`: active TSP request, or `null`.
- `busApproach`: bus detector state, or `null`.
- `decision`: final selected/base/requested groups and decision reason, or `null`.

## Request Fields

- `kind`: `Early`, `Petitioner`, `Latched`, or similar request lifecycle state.
- `source`: usually `Track` or `Bus`; future traces may add `Bike`.
- `targetGroup`: requested 1-based signal group.
- `strength`: arbitration strength.
- `expiry`: latch expiry timer.
- `extendCurrentPhase`: whether current green can be extended.
- `signaledLane`, `approachLane`, `upstreamLane`: lane evidence for track/tram probes.
- `signaledProbe`, `approachProbe`, `upstreamProbe`: probe result names.
- `indexedTramLanes`, `fallback*`: index and fallback evidence for track/tram detection.

## Bus Fields

- `decision`: bus detector outcome, such as `Request emitted`, `No eligible bus sample`, or suppression reasons.
- `targetGroup`: requested bus target group when one is emitted.
- `hitCount`: number of bus samples contributing to the selected match.
- `lane`, `vehicle`: sampled bus lane and vehicle entity when exposed.
- `laneType`, `laneChange`, `speed`: bus sample details useful for suppression analysis.

## Decision Fields

- `reason`: final TSP selection reason.
- `baseGroup`: group selected before TSP intervention.
- `selectedGroup`: group selected after TSP intervention.
- `targetGroup`: requested group considered by the TSP decision.
- `source`: source of the selected request.
- `preemptionSuppressedByPedestrianPhase`: true when pedestrian protection explains a deferral.
- `preemptionSuppressedByVehicleFairness`: true when vehicle fairness explains a deferral.
- `pendingPedestrianFairness` / `pendingVehicleFairness`: fairness context.

## Healthy Patterns

- Request target is already current and summary says `Target already current`.
- Decision reason is `Selected target phase` and `selectedGroup == targetGroup`.
- Request moves from `Early` or `Petitioner` to `Latched`, then either target becomes current or the request clears after service.
- Bus diagnostics say `No eligible bus sample` while the active request source is `Track`.

## Suspicious Patterns

- Active request repeatedly expires without the target ever being selected.
- `selectedGroup != targetGroup` without pedestrian, vehicle fairness, group coordination, or another explicit reason.
- Request source says `Bus` but bus diagnostics report no eligible bus sample.
- A visible tram/bus/bike is approaching but the trace repeatedly shows no probe evidence and no request.
- `trafficGroup.isMember == true` explains paused local TSP; do not treat that as a bug without checking group intent.
- `Failed to write TSP diagnostics trace` appears in `Player.log`.

## Instrumentation Gaps

Current traces expose the active/winning request and bus detector details, not a complete list of every visible transit or bicycle candidate. If multiple vehicles are visible but the trace has no candidate array, say that the diagnostics cannot prove why each non-winning vehicle lost.
