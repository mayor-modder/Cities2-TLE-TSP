# TSP Diagnostics Cost And Trace Audit

This document records the diagnostics audit for Transit Signal Priority (TSP).

## Summary

The selected-intersection diagnostics UI and JSONL trace writer are opt-in and
off by default. When enabled, they are useful for live troubleshooting, but they
also make selected-panel refresh broader than normal gameplay needs. The runtime
tram approach index is gated separately so disabled, follower-only, public-car
only, and stale/non-traffic-light settings do not trigger rail transit scans.

## UI Diagnostics Gating

The user-facing setting is `Settings.m_ShowTransitSignalPriorityDiagnostics`.
Defaults:

- `false` in settings defaults
- diagnostics panel is only built when the option is enabled
- selected-panel auto-refresh for diagnostics is only active when the option is
  enabled

When enabled, diagnostics are built for the selected main-panel entity. If the
selected intersection has no TSP settings component, the UI still builds a
disabled/default diagnostics view. That is useful when debugging why a selected
intersection has no request. The visible recent-event list and JSONL trace only
record meaningful TSP activity, plus one transition when activity ends.

## JSONL Trace Behavior

The trace file is a debugging artifact, not gameplay state.

- File name: `C2VM.TrafficLightsEnhancement.TspDiagnostics.jsonl`
- Location: `Application.persistentDataPath`
- Write path: selected-panel diagnostics refresh
- Rotation threshold: 5 MB
- Retention: newest 3 rotated files plus the active file
- Deduplication: per selected entity, based on the last summary signature
- Event filter: same meaningful-activity filter as the visible recent-event list

Trace writes are lock-protected, exception-handled, and bounded by rotation plus
retention. They are still synchronous UI-triggered file I/O, so diagnostics
should remain opt-in. A separate "write JSONL trace" setting is not needed yet;
the current trace follows the selected diagnostics event filter instead of
logging every selected-panel signal change.

## Runtime Debug Components

When an active TSP request is resolved, the simulation writes
`TransitSignalPriorityRuntimeDebugInfo` independently of the UI diagnostics
option. That component mostly captures values already computed during request
resolution, and it keeps selected-intersection debugging available without
rerunning runtime detection in the UI layer.

This is acceptable for now, but it should be profiled before increasing TSP
scope or enabling diagnostics by default.

## Tram Approach Index

`PatchedTrafficLightSystem.OnUpdate()` builds `TramApproachIndex` only when the
settings query contains at least one approach-index-eligible TSP setting. The
query is narrowed to non-temp, non-deleted traffic-light entities, and the pure
policy requires an enabled, track-request-capable setting that is not on a
grouped follower.

```text
enabled track-capable leader/standalone TSP setting -> scan rail transit lanes for the tick
```

Disabling TSP from the UI removes the settings component, which still avoids the
scan for users who have no TSP-enabled intersections. The narrower gate also
protects against stale disabled settings and future public-car-only settings.

## Follow-Up Work

Useful follow-ups:

- Profile `TransitSignalPriorityRuntimeDebugInfo` writes before adding bus
  priority or broader public transport priority.
- Consider a separate "write JSONL trace" option only if we need deliberate deep
  tracing for disabled/no-request selected intersections.
