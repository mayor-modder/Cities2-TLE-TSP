# Traffic group lockstep runtime diagnostics design

## Problem

In lockstep mode, traffic-group followers visibly react to the leader but also
appear to continue their own signal cycles. Four game launches have already
failed to produce enough evidence to identify the remaining writer.

The installed TLE Extended 1.0.6 assembly matches the current branch. Archived
diagnostics from that build show the follower-level `TrafficLights` component
matching the leader across sampled transitions, while the visible behavior was
still reported as incorrect. Existing source tests prove that intended branches
exist, but they do not prove which runtime branch executed or whether lane and
rendered signal outputs remained synchronized afterward.

The next build must collect enough evidence in one launch to distinguish:

1. a follower incorrectly entering its independent state machine;
2. follower synchronization being skipped or refused;
3. a later writer changing the synchronized `TrafficLights` component;
4. lane-signal output diverging from the synchronized controller;
5. rendered signal objects diverging from the synchronized controller; or
6. controller and outputs agreeing while phase or object masks map the wrong
   physical movements.

This diagnostic change must not alter traffic-light behavior.

## Scope

The diagnostic build will trace lockstep traffic groups only while the existing
diagnostics option is enabled. Selecting any member of a traffic group will
capture the leader and every follower in that group. The user will not need to
select each member individually or repeat the test with different logging
settings.

The work will not:

- change serialized `TrafficGroup`, `TrafficGroupMember`, or save versions;
- change group coordination, movement mapping, or demand policy;
- add always-on logging;
- overwrite follower state after simulation as a speculative fix; or
- inspect or modify unrelated mods.

## Runtime trace model

Add a runtime-only `TrafficGroupLockstepDebugState` component to grouped
junctions while diagnostics are enabled. It is reconstructed during play and is
never serialized.

Each state record will include:

- simulation frame and traffic-light update-frame index;
- group, leader, and member entity identities;
- leader/follower role;
- coordinated and green-wave flags;
- movement-map presence, completeness, and relevant mapped phases;
- which update passes visited the junction;
- the independent-pass disposition:
  - leader,
  - follower held on a non-leader shard,
  - follower deferred to the synchronization pass,
  - or follower allowed into the independent state machine;
- the synchronization-pass disposition:
  - applied,
  - missing same-tick master,
  - invalid master,
  - missing or incomplete movement map,
  - inactive group,
  - missing local demand state,
  - or another explicit eligibility failure;
- follower controller state before the pass;
- same-tick master state used by the pass;
- follower controller state immediately after the pass;
- `CustomTrafficLights` timer before and after;
- deterministic counts and hashes for lane-signal outputs before and after;
- deterministic counts and hashes for rendered traffic-light objects before
  and after; and
- flags indicating whether the independent pass changed the follower and
  whether synchronization actually changed it.

Controller snapshots include state, current group, next group, timer, signal
group count, and custom timer. Hashes must use a deterministic repository-owned
algorithm rather than `GetHashCode`.

The debug component will be ensured and removed by the existing traffic-group
runtime maintenance path according to the diagnostics setting. Simulation jobs
will only write it when present, keeping normal gameplay overhead negligible.

## Whole-group JSONL snapshot

Extend the existing selected-junction JSONL diagnostics with a
`trafficGroupLockstep` section. When the selected junction belongs to a group,
the section will contain one record for the leader and every follower.

For each member, serialize:

- live `TrafficLights` and `CustomTrafficLights` state;
- live group and leader state;
- the complete `TrafficGroupLockstepDebugState`;
- update-frame ownership;
- movement-map status and leader-to-member phase mapping;
- every lane signal with entity, group masks, signal, flags, petitioner,
  blocker, priority, and default priority;
- every rendered `Game.Objects.TrafficLight` subobject with entity, group masks,
  and current visible state;
- current deterministic lane and rendered-object hashes; and
- a derived verdict and reason.

The verdict will classify each member as one of:

- `In sync`;
- `Independent state machine advanced`;
- `Synchronization did not run`;
- `Synchronization was refused: <reason>`;
- `Controller changed after synchronization`;
- `Lane outputs changed after synchronization`;
- `Rendered outputs changed after synchronization`;
- `Output masks do not represent the mapped phase`; or
- `Insufficient evidence: <missing field>`.

The same comparison will emit a debounced warning to the TLE log whenever a
non-healthy verdict first appears or changes. This ensures `Player.log` and the
mod log retain the essential failure even if the JSONL trace rotates.

Whole-group snapshots will use the existing diagnostics cadence and rotation.
They will not require a second option or a second launch.

## Comparison logic

Put the verdict calculation in a pure logic type so it can be tested without
Unity ECS. The comparison receives literal controller snapshots, pass outcomes,
mapping status, and before/after/current output hashes.

The comparison order is:

1. Report missing evidence explicitly.
2. Report an independent follower update if the independent pass changed a
   coordinated lockstep follower.
3. Report why the synchronization pass did not apply.
4. Compare the synchronized follower controller with the same-tick master after
   applying the recorded movement mapping.
5. Compare the post-sync controller with the live post-frame controller.
6. Compare post-sync lane and rendered-object hashes with live hashes.
7. Validate that live output masks contain the mapped current/next phase.
8. Otherwise report `In sync`.

Green-wave members are recorded but excluded from the strict lockstep verdict,
because their configured offsets are intentional.

## Error handling and limits

- Missing entities, components, buffers, or mappings produce explicit
  diagnostic reasons rather than exceptions.
- A deleted or stale member is reported and skipped without aborting the rest
  of the group snapshot.
- Entity identity is always recorded as index and version.
- Full member output is retained for the selected group; diagnostics are opt-in,
  so silently truncating the exact failing member would defeat the purpose.
- Repeated identical warnings are suppressed per member and verdict signature.
- Diagnostic failures must never change simulation state or stop the game.

## Tests

Write tests before implementation.

Pure tests will cover:

- independent follower advancement;
- missing same-tick master state;
- invalid master state;
- incomplete movement mapping;
- successful mapped synchronization;
- controller mutation after synchronization;
- lane-output mutation after synchronization;
- rendered-output mutation after synchronization;
- mask/mapped-phase disagreement;
- green-wave exclusion; and
- missing-evidence classification.

ECS/source integration tests will verify:

- the runtime component is not serialized;
- instrumentation is gated by the diagnostics setting;
- all three simulation passes record their disposition;
- before/after controller and output hashes are captured;
- selecting one member enumerates the whole group;
- rendered traffic-light objects and lane signals are included;
- verdict warnings are debounced; and
- no diagnostic branch writes gameplay state.

Run the focused pure and ECS test projects, then the full .NET test set and
Release build. The diagnostic playtest build will be versioned as the next patch
version and installed through the normal Release build into:

`C:\Users\matt\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\TrafficLightsEnhancementExtended`

The game must be closed during installation. After installation, verify the
installed DLL hash matches the build output before asking for the single
playtest.

## One-launch playtest

1. Launch the game with diagnostics enabled.
2. Load the existing reproduction save.
3. Select any junction in the affected lockstep group.
4. Leave the group visible long enough to observe at least two complete leader
   cycles and one follower drift event.
5. Exit normally.

The returned JSONL trace, TLE mod log, `Player.log`, and installed-version
record must be sufficient to locate the first boundary that diverged. The
subsequent gameplay fix will target only that proven writer, receive a failing
regression test first, and require a separate final gameplay verification.
