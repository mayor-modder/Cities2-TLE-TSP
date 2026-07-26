# Traffic group custom phase initialization design

## Problem

Adding a junction to a traffic group currently creates empty
`CustomPhaseData`, `EdgeGroupMask`, and `SubLaneGroupMask` buffers. Opening the
new member's custom phase editor adds only one empty phase and does not
synchronously establish a complete topology-local phase configuration.

The visible **Copy phases to all members** action then becomes an accidental
initializer. It copies the leader's phase records and translates movement masks
with best-effort edge and lane matching. That translation is not proven safe
for rotated, asymmetric, tram, bicycle, or otherwise different junctions. It
also overwrites members that were already configured correctly.

Live diagnostics reproduced both failure modes:

- copied vehicle masks could combine multiple phases and leave a physical
  signal green continuously;
- copied or empty member phases could contain no physical approach, causing the
  runtime movement map to reject synchronization.

The runtime's fail-closed behavior is correct. The unsafe behavior is the
creation and editing workflow.

## Goals

- Make every group member's movement permissions local to that junction.
- Make the custom phase editor usable immediately for a newly added member.
- Give a new member the leader's phase count and timing values without copying
  the leader's movement masks.
- Prevent a batch action from overwriting configured movement permissions.
- Clearly distinguish a configured member from one that still needs phase
  setup.
- Preserve save compatibility and the existing runtime movement-map contract.

## Non-goals

- Automatically infer the user's intended movement permissions.
- Change traffic-group serialization.
- Change lockstep or green-wave phase synchronization.
- Add group-wide TSP.
- Build a general-purpose geometry cloning system.

## Considered approaches

### 1. Keep full copying and strengthen geometry validation

This would retain the existing action but refuse targets unless their topology
appears equivalent. It remains risky because structural similarity does not
prove equivalent signal intent, and the repository has no in-game topology
harness covering all supported road and track layouts.

### 2. Preview translated movement masks before copying

This would make the risk visible but would add a second phase-editing surface
and substantial UI complexity. It still encourages treating local movement
permissions as portable data.

### 3. Copy timing structure only and configure movements locally

This is the selected approach. Phase count and timing are coordination data;
lane, track, bicycle, and pedestrian permissions are junction-specific data.
The existing custom phase editor remains the single movement-editing surface.

## Design

### New-member initialization

The member-add path and the member-editor-open path call the same idempotent
initializer:

1. Ensure `CustomTrafficLights`, `CustomPhaseData`, `EdgeGroupMask`, and
   `SubLaneGroupMask` exist.
2. Synchronize the member's phase count with the leader when the leader has
   custom phases. New phase records copy only the leader's minimum and maximum
   duration and otherwise use normal `CustomPhaseData` defaults.
3. Preserve any existing member phase records and movement masks.
4. Materialize missing `EdgeGroupMask` entries from the member's own connected
   edges before publishing the UI binding.
5. Leave every newly materialized movement permission stopped. The player
   explicitly selects the movements served by each phase.
6. Mark the entity `Updated` so normal traffic-light initialization rebuilds
   lane signals and rendered signal masks.

No leader `EdgeGroupMask` or `SubLaneGroupMask` value is copied.

### Traffic groups UI

- Remove **Copy phases to all members** from the production traffic-groups UI.
- Keep a **Custom phase editor** action on every member.
- Expose the existing duration-only backend operation as **Match phase timings
  to leader**.
- Show **Needs phase setup** for a follower when its movement mapping is absent
  or incomplete.
- Do not label such a follower as synchronized.
- Keep the current leader/follower timing fields read-only where coordination
  owns them.

The status is advisory. Runtime synchronization continues to fail closed when a
complete map is unavailable.

### Existing copy and propagation code

The UI triggers and backend operations for full single-target and batch phase
copying will be removed. The edge/lane geometry translation helpers will also
be removed after their callers are migrated.

`PropagatePatternToMembers(...)` currently invokes the same geometry
translation when the leader's pattern changes. It will instead set the member
pattern and run the topology-local initializer. It must never copy or clear a
member's movement masks.

### Missing-phase migration

The load-time dialog will no longer recommend copying movement configuration
from the leader. Affected members will receive phase count and minimum/maximum
durations from the leader plus topology-local, stopped movement masks. The
dialog will direct the player to configure each affected member in the custom
phase editor.

### Safety and error handling

- Initialization is idempotent: reopening an editor cannot erase configured
  movement masks or shrink an existing phase list.
- A missing or invalid leader falls back to one local phase.
- A phase-count mismatch adds missing local phase records but does not delete
  extra member phases automatically.
- Missing connected-edge data leaves the member marked as needing setup and
  does not report successful synchronization.
- No serialized component layout changes.

## Data flow

```text
Add member
  -> create group membership
Open existing member editor
  -> retain existing group membership
Either entry point
  -> ensure required buffers
  -> open that member's custom phase editor
  -> ensure local phase structure
       -> copy leader phase count and min/max durations only
       -> materialize the member's own connected-edge masks
  -> render local movement controls
  -> player assigns movements
  -> mark Updated
  -> initialization rebuilds LaneSignal and rendered traffic-light masks
  -> movement-map diagnostics report complete or needs setup
```

## Testing

### ECS/source regression tests

- Group member editor initialization copies leader phase count and timing
  fields without calling edge/sub-lane copy helpers.
- Leader pattern propagation preserves member movement masks.
- Existing member movement buffers are not cleared or overwritten.
- Missing edge masks are materialized from the target member's topology.
- The traffic-groups UI no longer invokes `CallCopyPhasesToAllMembers`.
- The duration-only action is registered and exposed.
- Incomplete followers are not labeled synchronized.
- The migration path uses topology-local initialization and no longer calls a
  full-copy helper.

### UI tests

- A new member displays the custom phase editor action and setup status.
- The copy-to-all button is absent.
- The timing-only action uses localized sentence-case text.

### Verification

- Run the ECS test project.
- Run UI tests and the UI production build.
- Run pure, ECS, serialization, and UI suites because the change crosses
  runtime, migration, and UI boundaries.
- Run a Release build only after the game is closed.
- Playtest adding a fresh member, configuring each phase locally, reopening the
  editor without data loss, and confirming complete movement mapping before
  declaring the in-game behavior fixed.
