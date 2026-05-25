# Serialization And Migration Audit

This document records the current save-format and migration audit for TLE
Extended. It covers inherited TLE data plus the TSP additions.

## Compatibility Posture

TLE Extended is intended to be a drop-in replacement for Traffic Lights
Enhancement. Existing TLE intersections should load without user action, and TLE
Extended should preserve the inherited internal mod ID while the fork remains in
this compatibility mode.

TSP adds a saved per-intersection component,
`TransitSignalPrioritySettings`. It is additive: intersections without that
component behave as non-TSP intersections. TSP settings are versioned and
normalized on load.

## Current Saved TSP Fields

`TransitSignalPrioritySettings` persists:

- `m_Enabled`
- `m_AllowTrackRequests`
- `m_AllowPublicCarRequests`
- `m_RequestHorizonTicks`
- `m_MaxGreenExtensionTicks`

The runtime currently normalizes the feature to explicit source flags:

- track requests are allowed
- public-car requests back the player-facing soft bus-priority control and
  remain disabled unless that control is enabled
- request horizon is clamped through policy, with legacy `120` remapped to `10`
- max green extension is clamped through policy

Future changes to public-car/bus priority should treat these fields as
schema-sensitive. Changing their serialized meaning may need a save version bump
rather than simply removing normalization.

## Inherited Save Components Needing Explicit Coverage

The audit found inherited `ISerializable` layouts that should be documented and
round-trip tested before compatibility-affecting edits:

- `CustomTrafficLights`
- `TrafficGroup`
- `TrafficGroupMember`
- `EdgeGroupMask`
- `SubLaneGroupMask`
- `CustomPhaseData`
- `SignalDelayData`

`TrafficLightsEnhancement.Serialization.Tests` now covers current serializer
round trips for these inherited components, plus practical legacy payload
fixtures for older `CustomTrafficLights`, `TrafficGroup`, `TrafficGroupMember`,
`EdgeGroupMask`, `CustomPhaseData`, and `GroupMask.Signal` layouts.

## Fixed: SignalDelayData Layout

`SignalDelayData.Serialize(...)` writes:

```text
TLEDataVersion.V1
m_Edge
m_OpenDelay
m_CloseDelay
m_IsEnabled
```

`SignalDelayData.Deserialize(...)` previously read the leading payload version as
the serialized entity index, shifting the rest of the stream. The current reader
now consumes the payload version first, then reads `m_Edge` through
`reader.Read(out Entity)`. The regression is covered by
`Signal_delay_data_round_trips_current_payload`.

## Migration Version Sequencing

`TLEDataMigrationSystem.OnUpdate()` uses the pure `TleMigrationPlan` helper to
choose versioned migration steps before running the regular validation pass.
The expected path is:

| Loaded version | Versioned migration steps |
| --- | --- |
| `0` or missing defaults | `SignalDelayData`, `TrafficGroupMembers`, `CustomTrafficLights` |
| `1` | `TrafficGroupMembers`, `CustomTrafficLights` |
| `2` | `CustomTrafficLights` |
| `3` | `CustomTrafficLights` |
| `4` | `CustomTrafficLights` |
| `5` or newer | none; validation only |

The custom traffic-light migration is intentionally part of the versioned plan
for every pre-current save, but it runs only once per load. This preserves the
old broad validation behavior without the duplicate `MigrateCustomTrafficLights`
call that occurred for versions `2` through `4`.

## Versioning Ambiguity

`TLEDataVersion` is used for more than one concern:

- global migration state
- component payload versions
- special-case component fields

For example, some components write `TLEDataVersion.Current`, while
`TrafficGroup` writes an older payload version and conditionally reads a newer
field. Future save changes should document whether a new version is a global
migration step, a component payload step, or both.

## Fixed: Migration Issue State Ownership

Migration issue state is owned by `UISystem.AffectedIntersections`. The
migration system records affected entities through
`UISystem.AddToAffectedIntersections(...)`, UI bindings expose the same list via
`GetAffectedEntities`, and the migration issues modal dismisses entries through
`RemoveAffectedEntity(...)`.

The unused `MigrationIssuesService` static list was removed so there is only one
owner for affected-intersection state.

## Follow-Up Work

Track these as follow-up issues before touching inherited save behavior:

- Add serializer round-trip tests for inherited save components, starting with
  `SignalDelayData`. Done in `TrafficLightsEnhancement.Serialization.Tests`.
- Clarify and test migration version sequencing for loaded versions 0 through 5.
  Done in `TleMigrationPlanTests`.
- Document a save-format contract that lists each saved component and buffer,
  its payload version, runtime-only fields, downgrade expectations, and
  upstream-TLE compatibility assumptions. Done in
  [`save-format-contract.md`](save-format-contract.md).
- Remove or wire up `MigrationIssuesService` after confirming ownership of
  migration issue state. Done; affected-intersection state is owned by
  `UISystem`.
