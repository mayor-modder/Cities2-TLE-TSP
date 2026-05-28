---
name: tle-diagnostic-review
description: "Use when reviewing live or archived TLE Extended diagnostics, especially Transit Signal Priority JSONL traces, selected-intersection behavior, bus/tram/bike priority arbitration, crash follow-up, Player.log correlation, or questions like why a TSP-enabled light selected, extended, deferred, or ignored a phase."
---

# TLE Diagnostic Review

## Overview

Use this skill to interpret TLE Extended diagnostic evidence while testing the mod in Cities: Skylines II. Load `tle-codebase` first, and load `tle-csharp-ecs` when the review points toward traffic-light simulation, TSP policy, or ECS state.

Default stance: live-first, mostly quiet, evidence-backed. Watch what the selected intersection is doing, explain meaningful transitions, and avoid repeating every row the user can already see in-game.

## Diagnostic Sources

Check sources in this order:

1. Active TSP JSONL trace:
   `C:\Users\<user>\AppData\LocalLow\Colossal Order\Cities Skylines II\C2VM.TrafficLightsEnhancement.TspDiagnostics.jsonl`
2. Rotated TSP JSONL traces:
   `C2VM.TrafficLightsEnhancement.TspDiagnostics.*.jsonl`
3. Game logs for crashes, exceptions, load failures, and write failures:
   `Player.log` and `Player-prev.log`
4. Installed local mod folder when build/install freshness matters:
   `Mods\C2VM.TrafficLightsEnhancement`
5. Settings or presets only when configuration state matters:
   `ModsSettings\C2VM.TrafficLightsEnhancement` and `ModsData\C2VM.TrafficLightsEnhancement`

TLE writes only one dedicated diagnostics trace: the TSP JSONL file. Other TLE log messages go through `Mod.log` and appear in the normal game logs.

## Live Review Workflow

1. Confirm diagnostics are being written. If the active JSONL is missing or stale, check that the in-game TSP diagnostics option is enabled and inspect `Player.log` for TLE load/write warnings.
2. Tail the active JSONL while the user selects intersections.
3. Speak up on meaningful events:
   - selected entity changes
   - first request observed for an intersection
   - target selected, current phase extended, request deferred, request suppressed, or request expired
   - no diagnostics after selection
   - repeated no-probe state while the user sees transit approaching
   - target group never selected
   - bus/tram/bike source mismatch
   - exceptions, crashes, or `Failed to write TSP diagnostics trace`
4. Stay quiet on repetitive records unless the user asks for raw readout.
5. Give short per-intersection verdicts:
   - `Healthy`: requests and decisions match the visible behavior.
   - `Suspicious`: evidence suggests a missed probe, wrong target, stale latch, unexplained deferral, or source mismatch.
   - `Blocked by missing data`: current diagnostics do not expose enough candidates or vehicle identities to prove the behavior.
   - `Crash-related`: game logs show nearby exceptions, load failures, or write failures.

Use `scripts/tle_diagnostics_review.py` for compact live or archived summaries:

```powershell
python .agents\skills\tle-diagnostic-review\scripts\tle_diagnostics_review.py --watch 45
python .agents\skills\tle-diagnostic-review\scripts\tle_diagnostics_review.py --tail 300 --with-player-log
python .agents\skills\tle-diagnostic-review\scripts\tle_diagnostics_review.py --jsonl C:\path\to\trace.jsonl --tail 1000
```

## Post-Session And Crash Review

For post-session analysis, load the active trace and newest rotated files, then correlate with `Player.log` and `Player-prev.log`.

Report:

- last selected entities and timestamps
- last request, decision, signal transition, and bus/tram/bike evidence per selected entity
- repeated suspicious patterns
- crash-adjacent exceptions or TLE warnings
- whether the trace stopped before, during, or after the suspected failure

If the game crashed, avoid diagnosing TSP behavior from the final JSONL line alone. Check whether `Player.log` contains a managed exception, asset/load failure, mod startup failure, or diagnostics write warning near the trace stop time.

## Arbitration Story

Tell both sides of multi-vehicle behavior:

- why the winning request won
- why each visible non-winning candidate did not win, when diagnostics expose candidate data
- what remains unknown when the JSONL only exposes the selected/winning request

Current traces expose selected-entity state, signal configuration, traffic-group status, lane signal masks, one active request, bus approach diagnostics, and final decision trace. Future traces may add candidate arrays for bikes or multi-vehicle arbitration. Treat arrays named like `candidates`, `vehicles`, `requests`, `contenders`, `bikeApproach`, or source-specific sections as evidence to summarize rather than ignore.

Do not pretend candidate-level evidence exists. Say `instrumentation gap` when the user can see multiple vehicles but the trace only contains the winning request.

## Interpretation Notes

Read `references/diagnostics-schema.md` when you need field meanings or anomaly heuristics.

Important patterns:

- `Target already current` is healthy when the current group already serves the request.
- `Selected target phase` is healthy when `selectedGroup` equals `targetGroup`.
- `Waiting to preempt` is not automatically bad; look for a later transition to `Changing to target`, `Selected target phase`, an explicit fairness deferral, or expiry.
- `Latched` requests can be normal after a vehicle sample disappears briefly. Repeated latch expiry without action is suspicious.
- `No eligible bus sample` is normal for tram-only/track situations.
- Traffic-group membership pauses local TSP; confirm `trafficGroup.isMember` before calling missing priority a bug.
- Exclusive pedestrian or vehicle fairness flags can explain a delayed or suppressed preemption.

## Evidence Discipline

Separate:

- verified trace facts
- inferences from trace patterns
- game-log facts
- instrumentation gaps

When a finding may require code changes, point to the relevant source area and suggest loading `tle-csharp-ecs`, `tle-ui-localization`, or `tle-testing-release` before editing.
