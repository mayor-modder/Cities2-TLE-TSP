# Custom phase identity mapping design

## Problem

Traffic groups currently reconstruct every leader-to-member phase map from
physical movement signatures, including groups where the player configured
custom phases independently at each junction.

Live testing with a diverging diamond interchange reproduced a configured
two-phase group where:

- leader `2048834:1` was in phase 2;
- follower `2048835:1` remained in phase 1;
- the selected-junction diagnostic reported
  `Movement mapping unavailable; follower held; leader G2 -> G-`;
- the mapping log reported `AmbiguousExactMatch` for the leader and
  `NoOverlappingPhase` for the follower.

The physical matcher uses undirected approach axes and movement overlap. That
is useful when phases are generated automatically, but it cannot infer the
player's intended correspondence between independently configured custom
phases. In a diverging diamond, corresponding phases at separate junctions can
serve physically different or non-overlapping movements.

The runtime's fail-closed behavior is correct once the map is absent. The
incorrect behavior is requiring geometry inference for an explicit custom
phase contract.

## Goals

- Treat custom phase numbers as the synchronization contract.
- Map leader phase `N` to member phase `N` for custom-phase members.
- Always map a group leader's phases to themselves.
- Keep unconfigured or incomplete custom phases fail-closed.
- Preserve movement-mask ownership at each junction.
- Preserve physical movement matching for non-custom patterns.
- Preserve traffic-group serialization and save compatibility.

## Non-goals

- Copy movement permissions between junctions.
- Add a manual phase-mapping UI.
- Change green-wave offsets or timing behavior.
- Weaken validation for empty custom phases.
- Redesign vanilla traffic-group demand mapping.

## Considered approaches

### 1. Pattern-aware identity mapping

For a custom-phase leader and custom-phase member, validate the local phase
records and build an identity map. The leader always receives an identity map
to itself. Other pattern combinations continue through the physical movement
matcher.

This is the selected approach. The custom editor already exposes numbered
phases and requires the player to assign local movements to each phase.

### 2. Explicit per-member phase-mapping UI

The traffic-groups panel could let the player map every leader phase to a
member phase manually. This would support intentionally reordered phases, but
it would add serialized configuration, migration work, validation, and another
setup surface. It is unnecessary when custom phase numbers already provide the
required contract.

### 3. Strengthen geometry inference

The physical matcher could encode directed approaches or more lane geometry.
That might reduce ambiguity for similar junctions, but it still cannot infer
arbitrary player intent across different layouts. It would not establish a
reliable contract for diverging diamonds or other asymmetric custom groups.

## Design

### Identity-map policy

Add a pure-logic identity-map operation alongside the existing physical
movement mapper. It will:

1. validate the leader phase signatures;
2. validate the member phase signatures;
3. reject a member with fewer phases than the leader;
4. map each leader phase index to the same member phase index;
5. construct a complete `TrafficGroupPhaseMap` without comparing physical
   movement overlap.

Validation retains the current one-based sequence and non-empty-approach
requirements. A newly initialized phase with no configured movement therefore
remains incomplete and cannot be labeled synchronized.

### Runtime selection

`TrafficGroupSystem.RefreshMovementMappings(...)` will select the mapping
strategy per member:

- if the member is the group leader, use identity mapping;
- otherwise, if both leader and member use `CustomPhase`, use identity
  mapping;
- otherwise, use `TrafficGroupMovementMappingPolicy.TryBuild(...)` and its
  physical signature matching.

This gives the leader a usable same-tick master state even when two of its
custom phases have identical physical signatures. A configured custom follower
then consumes the same numbered phase from that master state.

### Safety

- Identity mapping does not read, copy, clear, or modify movement masks.
- Every custom phase must still expose at least one supported physical
  approach before synchronization is allowed.
- Extra member phases remain preserved; only leader-owned phase numbers are
  synchronized.
- Missing, invalid, or empty local phase data continues to remove the runtime
  mapping component and hold the follower.
- No serialized component layout changes.

### Diagnostics

The existing selected-junction diagnostics require no new payload. A healthy
custom group will report `Identity mapping`; an incomplete custom member will
continue to report `Movement mapping unavailable; follower held`.

## Testing

Pure-logic regression coverage will prove that identity mapping:

- accepts two configured custom phase sets with no physical overlap;
- accepts duplicate physical signatures while preserving phase order;
- rejects an empty leader or member phase;
- rejects a member with fewer phases than the leader.

ECS/source coverage will prove that runtime strategy selection:

- uses identity mapping for the leader;
- uses identity mapping when both sides use custom phases;
- retains physical mapping for other pattern combinations.

Complete verification will include the pure, ECS, serialization, and UI test
suites. A Release build and installed-artifact verification will run only after
Cities: Skylines II is closed. The targeted playtest will reopen the same
diverging diamond and confirm that leader phase 2 advances the follower to
local phase 2 without changing any movement permissions.
