# Performance Repro Matrix

This maintainer checklist defines a small, repeatable playtest matrix for
investigating traffic-light performance reports in large Cities: Skylines II
cities. Use it before treating a single report as a confirmed regression.

The matrix separates three costs that are easy to mix together:

- baseline city simulation cost with TLE Extended installed,
- runtime traffic-light cost from many customized junctions, traffic groups,
  custom phases, and TSP,
- optional diagnostics cost from selected-panel refresh and JSONL trace output.

## Run Rules

- Test on a copied save, not the maintainer's primary city save.
- Use the same camera position, zoom, overlays, and graphics settings for every
  run in the set.
- Let the city warm up for at least 60 in-game seconds after load, after
  changing simulation speed, and after opening or closing the TLE panel.
- Capture observations over a steady two to three minute window per simulation
  speed.
- Record ranges instead of a single lucky frame: FPS range, whether simulation
  speed holds, and whether short stalls repeat.
- Capture new `Player.log` warnings or errors separately from old startup noise.
- When diagnostics are enabled, also note whether the selected-intersection
  diagnostics panel is open and whether the JSONL trace file grows during the
  observation window.

## City And Junction Buckets

Use the closest available save. Exact population is less important than using
the same save across paired runs.

| Bucket | Target | Notes |
| --- | --- | --- |
| Large | 100k to 200k population | Normal large-city repro size. |
| Extra large | 200k to 400k population | Preferred for regression confirmation. |
| Stress | 400k+ population, or the largest available city | Optional; use only after the smaller matrix points to a real issue. |

For customized junction count, use the number of intersections whose TLE state
has been changed from vanilla. Count traffic-group members and custom-phase
junctions separately in the observation notes.

## Core Matrix

Run the scenarios in order until the report is explained. If time is short,
prioritize `P0`, `P2`, `P4`, `P6`, and `P7`.

| ID | Purpose | City size | Customized junctions | Traffic groups | Custom phases | TSP | Diagnostics | Selected-panel diagnostics | Simulation speeds | Main observations |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| P0 | Baseline with TLE Extended installed | Extra large | 0, or unchanged save baseline | None | None | Off | Off | Closed | 1x, 3x, 8x | FPS range, sim speed hold, startup/runtime warnings |
| P1 | Light customization scale | Extra large | About 25 | None | Simple, 2 to 3 phases | Off | Off | Closed | 1x, 3x, 8x | Delta from P0 |
| P2 | Heavy custom-phase runtime | Extra large | About 100 | None | Complex, 4 to 6 phases, include pedestrian masks when common | Off | Off | Closed | 1x, 3x, 8x | Delta from P1; repeated stalls while panel is closed |
| P3 | Traffic-group runtime | Extra large | About 100 total | 5 groups of 5 to 10 junctions, plus standalone customized junctions | Mix of simple and complex phases | Off | Off | Closed | 1x, 3x, 8x | Delta from P2; green-wave or group sync warnings |
| P4 | Runtime TSP cost without diagnostics | Extra large | About 100 | None | Same shape as P2 | On for 25 to 50 standalone junctions; include tram and bus corridors if available | Off | Closed | 1x, 3x, 8x | Delta from P2; transit-heavy corridors; request-related warnings |
| P5 | Grouped-junction TSP gating | Extra large | About 100 total | Same shape as P3 | Same shape as P3 | Saved on for some group members before grouping; runtime should be suspended while grouped | Off | Closed | 1x, 3x, 8x | Delta from P3; unexpected TSP work on grouped junctions |
| P6 | Diagnostics option overhead without selected-panel trace | Extra large | Same save as P4 | None | Same shape as P4 | Same as P4 | On | Closed or TLE panel not focused on diagnostics | 1x, 3x, 8x | Delta from P4; cost of diagnostics gates without selected-panel JSONL trace |
| P7 | Selected-panel diagnostics overhead | Extra large | Same save as P4 | None | Same shape as P4 | Same as P4 | On | Open on a busy TSP-enabled junction | 1x, 3x, 8x | Delta from P6; JSONL growth; selected-panel refresh stalls |

Optional stress confirmation:

| ID | Purpose | City size | Customized junctions | Traffic groups | Custom phases | TSP | Diagnostics | Selected-panel diagnostics | Simulation speeds | Main observations |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| S1 | Many customized standalone junctions | Stress | About 250 | None | Mix of simple and complex phases | Off | Off | Closed | 1x, 3x, 8x | Whether custom-phase scale alone produces repeatable slowdown |
| S2 | Many customized junctions with active TSP | Stress | About 250 | None | Mix of simple and complex phases | On for 50 to 100 standalone junctions | Off | Closed | 1x, 3x, 8x | Whether TSP scale adds repeatable runtime cost beyond S1 |
| S3 | Diagnostics worst case | Stress | Same save as S2 | None | Same shape as S2 | Same as S2 | On | Open on a busy TSP-enabled junction | 1x, 3x only | Diagnostic overhead only; do not use as normal gameplay cost |

## Observation Template

Copy this block once per run.

```text
Run ID:
Save/city bucket:
Population:
Customized junction count:
Traffic groups:
Custom-phase count and typical phase count:
TSP setup:
Diagnostics setting:
Selected-panel diagnostics open:
Simulation speeds tested:
FPS range at 1x / 3x / 8x:
Simulation speed behavior:
Repeated stalls or hitches:
New Player.log warnings/errors:
JSONL trace growth:
Notes:
```

## Interpreting Results

Treat `P0` through `P5` as runtime simulation cost because diagnostics are off
and the selected-panel diagnostics path is closed.

Treat `P6` as diagnostics-option overhead. It can still matter, especially for
bus approach samples, but it should not be mixed with the normal diagnostics-off
gameplay baseline.

Treat `P7` and `S3` as selected-panel diagnostics overhead. The panel can build
selected-junction rows and write JSONL trace events. Slowdown that appears only
in these runs is useful for diagnostics tuning, but it is not evidence that
ordinary diagnostics-off simulation regressed.

Use paired comparisons:

- `P1 - P0`: light customization cost.
- `P2 - P1`: custom-phase scale cost.
- `P3 - P2`: traffic-group coordination cost.
- `P4 - P2`: standalone runtime TSP cost.
- `P5 - P3`: grouped-junction TSP gating cost. Local TSP should be suspended
  while grouped, so a large delta here needs investigation.
- `P6 - P4`: diagnostics setting cost without selected-panel trace.
- `P7 - P6`: selected-panel diagnostics and JSONL trace cost.

When filing or updating a performance report, include the closest matching run
ID, the copied observation block, whether the selected-panel diagnostics were
open, and the new log warnings or errors. If the report does not map to any run,
add one temporary local note to the observation block before expanding the
tracked matrix.
