# Green-wave playtest checklist

This maintainer checklist defines a repeatable in-game playtest pass for
traffic groups, lockstep coordination, and green-wave coordination. It tracks
issue #148 and should be used before changing runtime traffic-group behavior.

Use this checklist to collect evidence, not to prove the feature is ready from
one successful run. Treat every result as tied to the tested save, topology,
and mod commit.

## Scope

This checklist covers:

- two or more signalized junctions in one traffic group
- lockstep coordination
- green-wave coordination
- custom phases on the leader and followers
- save/load round trip behavior
- leader removal and reassignment
- TSP settings preserved while grouped and restored after removal

It does not cover adaptive coordination, group-wide TSP, save-format changes,
or new runtime behavior. Those need separate design and implementation issues.

## Related references

- [Traffic group notes](traffic-groups.md)
- [Signal phase triage checklist](signal-phase-triage-checklist.md)
- [Custom phase data flow](custom-phase-data-flow.md)
- [TSP architecture notes](tsp-architecture.md)
- [Save-format contract](save-format-contract.md)

## Run rules

- Test on a copied save, not a primary city save.
- Record the game version, mod commit, map, road orientation, and whether the
  save previously used upstream Traffic Lights Enhancement.
- Enable TLE diagnostics when collecting a bug report or investigating an
  unexpected result. Leave diagnostics off for a quick smoke pass.
- Use the same camera position and simulation speed for paired lockstep and
  green-wave observations.
- Let each setup run for at least three complete leader cycles before judging
  phase order or offset behavior.
- Capture screenshots or short clips when the observed follower phase differs
  from the expected phase.
- Save and reload a copy before marking any scenario as stable.
- Keep TSP observations separate from group timing observations. Local TSP
  should be suspended while a junction belongs to a group.

## Evidence packet

Record this once per playtest pass.

```text
Tester label:
Date:
Game version:
Mod commit:
Save/map:
Road orientation:
Traffic group name:
Group member count:
Leader junction description:
Follower junction descriptions:
Diagnostics enabled:
JSONL trace captured:
Screenshots or clips captured:
New Player.log warnings/errors:
Overall result: Pass / Fail / Unknown
Notes:
```

Use neutral tester labels such as "maintainer retesting" or "local retesting"
when copying results into public issues or pull requests.

## Scenario matrix

Run the smallest useful set first. If time is short, prioritize `G0`, `G1`,
`G2`, `G5`, and `G6`.

| ID | Purpose | Setup | Expected behavior | Record |
| --- | --- | --- | --- | --- |
| G0 | Baseline standalone timing | Select each junction before grouping. Configure the intended mode and, if relevant, custom phases. | Each junction behaves according to its own selected mode before group coordination is introduced. | Mode, phase count, active group order, visible signal behavior, diagnostics rows if enabled. |
| G1 | Create a two-junction group | Add two signalized junctions to one traffic group. Keep green wave disabled. | One junction is leader. The follower uses group coordination instead of independent timing. | Leader identity, follower identity, group cycle length, follower phase count, whether TSP controls become read-only or suspended. |
| G2 | Lockstep coordination | Run the group with coordination enabled and green wave disabled. | Follower current phase, next phase, signal state, and timer follow the leader's timing contract. | Three leader cycles, any visible phase mismatch, diagnostics traffic-group rows, JSONL traffic-group data if available. |
| G3 | Green-wave coordination | Enable green wave and set a non-zero signal delay or offset for a follower. | The follower is staggered from the leader according to stored group timing instead of direct lockstep mirroring. | Speed, offset, signal delay, member phase offset, member cycle timer, visible stagger, whether the stagger repeats across cycles. |
| G4 | Three-plus-member group | Add at least one more follower with a different distance from the leader. | Each follower remains tied to the leader cycle and keeps its own stored delay or offset. | Member order, distance to leader, delay, offset, whether nearer and farther followers differ as expected. |
| G5 | Custom phases on leader and followers | Use custom phases on the leader and at least one follower. Copy phases if that is part of the report being tested. | Group coordination uses custom phase data without changing saved movement masks unexpectedly. | Leader phase list, follower phase list, copied phase source, active phase order, movement that proves the custom mask still works. |
| G6 | Save/load round trip | Save the test city, exit to menu or desktop, reload the copied save, and reselect the group. | Group membership, leader, green-wave settings, cycle length, member delays, offsets, and custom phase data are preserved. | Before/after values for group settings, leader, member count, phase count, delay, offset, and visible first-cycle behavior after reload. |
| G7 | Leader removal and reassignment | Remove the leader from the group or delete the leader junction in a copied save. | Another member becomes leader or the group is cleaned up without stale membership references. | New leader identity, member count, warnings/errors, whether followers continue coordinated timing or leave the group cleanly. |
| G8 | TSP preservation while grouped | Enable tram or bus TSP on a standalone junction, add it to the group, then remove it from the group. | TSP is suspended while grouped, including for the leader, and the saved TSP setting resumes after removal. | TSP enabled before grouping, TSP read-only or suspended while grouped, TSP setting after removal, whether any TSP request affects grouped timing. |
| G9 | Road edit after grouping | On a copied save, change lane count, add tracks, add bike lanes, or edit connected roads near a group member. | Either saved masks remain aligned or the mismatch is documented as topology-sensitive evidence for a follow-up. | Exact road edit, before/after movement masks, visible wrong movement if any, whether save/load changes the result. |

## Per-scenario observation template

Copy this block once per scenario.

```text
Scenario ID:
Setup summary:
Expected behavior:
Observed behavior:
Result: Pass / Fail / Unknown
Leader current/next phase:
Follower current/next phase:
Green wave enabled:
Group cycle length:
Member signal delay:
Member phase offset:
Member cycle timer:
TSP state:
Custom phase details:
Save/load tested:
Screenshots/clips:
JSONL trace lines:
Player.log warnings/errors:
Follow-up needed:
Notes:
```

## Interpreting results

Treat `Pass` as evidence for the tested topology only. A two-junction road-only
group does not prove asymmetric lane counts, tram tracks, bike lanes, or copied
custom phases are safe.

Treat `Fail` as a candidate issue only when the setup and expected behavior are
clear enough for another maintainer to repeat. Include the scenario block, the
evidence packet, and the relevant screenshots, clips, diagnostics, or trace
lines.

Treat `Unknown` as the normal result when the current diagnostics cannot show
which group timing value produced the observed phase. Prefer a diagnostics
follow-up before changing runtime behavior.

Use paired comparisons:

- `G2` versus `G3`: lockstep behavior compared with green-wave offset behavior.
- `G5` versus `G0`: custom phase behavior before and after grouping.
- `G6` versus the pre-save scenario: save/load persistence.
- `G8` before, during, and after grouping: TSP suspension and restoration.

## Follow-up guidance

Create or update a narrower issue when the playtest identifies one of these
cases:

- Group timing is stable, but the selected UI does not explain the current
  leader/follower role, delay, offset, or TSP suspension.
- Green-wave offsets are repeatable but confusing enough to need better
  read-only diagnostics.
- Custom phase copying fails only for a specific topology such as asymmetric
  lane counts, tram tracks, bike lanes, or pedestrian crossings.
- Save/load changes group membership, leader selection, cycle length, delay,
  offset, or custom phase data.
- TSP affects grouped timing, or saved TSP settings do not resume after a
  junction leaves the group.

Before proposing runtime behavior changes, link the issue to the completed
scenario IDs and state which observations were verified in-game, inferred from
current behavior, or still unknown.
