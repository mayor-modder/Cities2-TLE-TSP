# Traffic Groups

Traffic groups are inherited Traffic Lights Enhancement behavior for coordinating
multiple signalized junctions. TLE Extended keeps this save/component model for
drop-in compatibility, and Transit Signal Priority builds around it rather than
rewriting it.

This document is maintainer-facing. It describes the current implementation,
not an idealized design.

## Source Map

- `TrafficLightsEnhancement/Components/TrafficGroup.cs`
  stores group-wide coordination settings and runtime master-clock fields.
- `TrafficLightsEnhancement/Components/TrafficGroupMember.cs`
  marks a junction as a member of a group and stores leader, index, offset, and
  per-member timing data.
- `TrafficLightsEnhancement/Components/TrafficGroupName.cs`
  stores the group name in fixed `ulong` fields while serializing as a string.
- `TrafficLightsEnhancement/Components/TrafficGroupTspDebugState.cs`
  is reserved for group-level TSP diagnostics.
- `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`
  owns group creation, membership changes, leader selection, green-wave timing,
  master-clock capture, and follower coordination.
- `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
  exposes traffic group data and mutation triggers to the React UI.
- `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`
  and `PatchedTrafficLightSystem.cs` use the group state when updating signal
  phases.

Related docs:

- `docs/save-format-contract.md`
- `docs/serialization-and-migration-audit.md`
- `docs/dynamic-mode.md`
- `docs/tsp-architecture.md`

## Data Model

`TrafficGroup` is a component on a synthetic group entity, not on the junction
itself. It stores:

- whether coordination is enabled
- whether green wave timing is enabled
- green-wave speed, offset, and maximum coordination distance
- creation time and cycle length
- runtime-only master signal state copied from the leader each frame

Only the persisted settings are serialized. The master-clock fields are
runtime-only and are rebuilt from the leader.

`TrafficGroupMember` is a component on each junction node in the group. It
stores:

- the group entity
- the current leader entity
- the member index
- distance/offset/signal-delay values used by green-wave timing
- a per-member cycle timer
- `m_IsGroupLeader`, which makes one junction authoritative for the group

The first junction added to an empty group becomes the leader. If the leader is
removed, `TrafficGroupSystem.AssignNewLeader(...)` promotes another member and
updates every member's `m_LeaderEntity`.

`TrafficGroupName` stores up to 64 characters internally and serializes as one
string. It is unversioned in the save format.

## UI Flow

The Traffic Groups panel is built in `UISystem.UIBIndings.cs` when
`m_MainPanelState == MainPanelState.TrafficGroups`.

The backend sends each group as `UITypes.ItemTrafficGroup`, including:

- group entity identity and name
- member count
- coordination and green-wave settings
- current junction membership/leader status
- current junction delay/offset data
- member list with phase counts and available pattern options
- cycle length

The same binding file handles UI triggers for:

- creating and deleting groups
- adding/removing the selected junction
- renaming groups
- setting the leader
- toggling coordination and green wave
- editing speed, offset, cycle length, and signal delay
- calculating delays
- selecting group members from the panel

When a selected junction belongs to a traffic group, the main panel marks TSP as
non-editable and explains that local TSP is suspended while the group controls
coordination. This applies to both followers and the current leader. Saved TSP
settings remain on the junction and can resume if the junction is removed from
the group.

## Runtime Behavior

`TrafficGroupSystem.OnUpdate()` runs for coordinated groups. Each tick it:

1. Advances the group cycle timer.
2. Captures the leader's current signal group, next signal group, state, timer,
   signal group count, and custom timer into the group master-clock fields.
3. Applies coordination to followers.

When green wave is disabled, followers use the job-level
`SyncSignalGroupWithLeader(...)` path and copy the leader phase/state/timers
directly.

When green wave is enabled, followers use offset-based timing. Signal delays and
phase offsets are used to stagger member cycle positions while staying tied to
the leader's cycle.

Adding a junction to a group also ensures the junction has the core TLE custom
traffic-light data it needs:

- `CustomTrafficLights`
- `CustomPhaseData`
- `EdgeGroupMask`
- `SubLaneGroupMask`

If the junction is already in a custom traffic mode that is not dynamic or fixed
timed, the group system switches it to dynamic mode.

## TSP Interaction

Traffic groups and local TSP are intentionally incompatible controls today.
When a junction has `TrafficGroupMember`, local runtime TSP is suspended for
that junction, including the group leader.

`TransitSignalPriorityRuntime.IsRuntimeEligibleJunction(...)` returns false for
all group members. The approach-index eligibility job in
`PatchedTrafficLightSystem` uses the same rule so grouped intersections do not
cause unnecessary TSP approach-index work.

This is conservative but important:

- Group coordination and green-wave timing remain authoritative once a user
  places intersections in a group.
- A leader-local TSP request could disrupt the timing contract the user chose
  for the whole group.
- Routing member requests through a group leader would need explicit conflict,
  pedestrian fairness, and diagnostics semantics.
- Group-level TSP propagation is not currently active; old serialized
  scaffolding is read for compatibility where needed, but not used for behavior.

Future group-wide TSP should define whether and how requests from members are
promoted to a leader, how diagnostics explain that promotion, and how pedestrian
fairness works across the whole group. Until then, local TSP settings are
preserved but inactive while the junction belongs to a group.

## Save Compatibility

Traffic group data is part of the inherited TLE save surface. TLE Extended
should preserve it when a user switches from upstream TLE.

Current persisted components:

- `TrafficGroup`: versioned group settings. The runtime master-clock fields are
  not serialized.
- `TrafficGroupMember`: versioned member data. V1 data defaults
  `m_MemberCycleTimer`; V2 stores it explicitly.
- `TrafficGroupName`: unversioned group name string.

The migration and cleanup path validates group references and removes or resets
invalid group data rather than changing the format casually. See
`docs/save-format-contract.md` and `docs/serialization-and-migration-audit.md`
before touching these components.

Compatibility rules for future work:

- Do not remove or rename these components without an explicit save-format
  migration.
- Do not make member-local or leader-local TSP behavior authoritative while a
  junction is grouped without a documented group semantics change.
- Keep group membership and leader fields stable for users moving from upstream
  TLE to TLE Extended.
- Treat group-wide TSP or grouped bus priority as new behavior that needs
  opt-in controls, diagnostics, and tests.

## Known Maintenance Notes

- `TrafficGroupSystem.cs` is large and mixes group lifecycle, green-wave math,
  leader synchronization, UI-serving helpers, and maintenance operations.
- Several operations scan all group members through entity queries. This is
  acceptable for current scale, but future performance work should preserve
  save compatibility first.
- Group behavior is hard to reason about without in-game context. Prefer adding
  pure tests and small docs before changing runtime behavior.
