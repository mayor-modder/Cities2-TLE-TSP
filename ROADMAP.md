# TLE Extended Roadmap

TLE Extended is a compatible extended fork of Traffic Lights Enhancement for
Cities: Skylines II. The project starts from the rewritten TLE codebase plus
Transit Signal Priority, and grows from there with a focus on compatibility,
maintainability, diagnostics, and broader transit-priority features.

This roadmap is maintainer-facing. The repository is public, but these notes are
not release promises.

## Guiding Principles

- Preserve drop-in compatibility with existing TLE saves and configured
  intersections wherever possible.
- Prefer opt-in behavior for changes that alter existing junction behavior.
- Version, document, and test save-format changes before users accumulate
  incompatible data.
- Keep complex logic isolated and testable outside Unity/ECS when practical.
- Make diagnostics useful enough to explain behavior in real cities, but keep
  expensive diagnostics off by default.
- Treat uncertain work as research until the implementation path is understood.
- Track concrete work as GitHub issues so future agents can continue without
  relying on chat history.

## Current Foundation

These are no longer roadmap guesses. They are the foundation future work should
preserve and extend:

- Transit Signal Priority is implemented as an opt-in, per-junction feature
  with separate tram and bus source controls.
- TSP has pure policy tests, UI source tests, serialization coverage, and a
  custom state-machine regression harness for TSP-off behavior.
- Dynamic mode now documents and tests restored narrow linked-phase behavior,
  including how it interacts with TSP-selected phases.
- Bicycle phase weight is exposed in the custom phase vehicle-weight UI.
- The bus source has a separate off-by-default player control and a soft MVP
  runtime path. Bus requests can hold an already-serving green or select their
  group at normal transition points, but trams outrank buses and buses do not
  use aggressive tram-style preemption.
- Bus diagnostics can identify mixed and bus-only approaches, including current
  and change-lane samples, but bus stop and lane-change semantics still need
  real-save playtesting and refinement.
- Maintainer docs now cover TSP architecture, diagnostics, dynamic mode,
  save-format compatibility, localization workflow, and serialization/migration
  audit notes.
- The fork is named and documented as TLE Extended while retaining the inherited
  TLE compatibility posture.

## Near-Term Decision Queue

These are the next bounded choices to resolve before larger feature expansion:

- Continue playtesting bus priority and bus approach diagnostics in real saves,
  with special attention to mixed lanes, bus-only lanes, lane changes, queues,
  and stop behavior.
- Refine bus stop-relation classification and lane-change request semantics
  before making bus priority more aggressive.
- Extract custom phase selection into pure logic only when a behavior change or
  larger refactor needs it; the current extraction audit does not require an
  immediate rewrite.
- Remove or retire unused inherited localization paths only after supported game
  version checks confirm they are safe to remove.
- Keep the save-format contract and localization workflow current whenever
  those surfaces change.

## Bus Priority Path

Bus priority builds on the TSP architecture with a conservative, opt-in soft
MVP:

- Pure bus-priority policy tests are in place.
- Bus approach diagnostics can identify mixed-lane and bus-only approaches
  behind the existing off-by-default diagnostics option.
- Pure stop-aware suppression rules are in place for boarding, near-side stops,
  far-side stops, unknown stop relation, and queued buses.
- A separate bus source control exists and is off by default.
- Bus requests are soft: they may hold an already-serving green or select their
  group at normal transition points, while tram requests outrank bus requests.
- Bus priority does not use tram-style aggressive minimum-green shortening in
  this MVP.
- Next, use playtesting to refine stop relation, lane-change behavior, and bus
  stop semantics before expanding bus priority behavior.

## Longer-Term Direction

- Support broader transit priority policies across trams and buses.
- Improve pedestrian-phase and conflict-policy behavior while keeping outcomes
  predictable.
- Build better compatibility and migration tooling for users moving from TLE.
- Improve inherited TLE documentation for migrations, custom phases, traffic
  groups, and maintenance workflows.
- Prepare public release documentation only when the project is ready to be
  publicized.

## Current Non-Goals

- No Paradox Mods publication push yet.
- No release dates.
- No broad marketing page.
- No large unrelated cleanup without an issue and a focused commit.
- No aggressive bus preemption until diagnostics, stop behavior, and lane-change
  semantics are understood in real saves.
