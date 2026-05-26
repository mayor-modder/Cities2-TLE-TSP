# Inherited Documentation Accuracy Audit

This note records the issue #92 documentation accuracy pass. The goal was to
check inherited user-facing claims against current repo evidence, not to rewrite
the docs for style.

## Scope Scanned

- `README.md`
- `GUIDE.md`
- Maintainer docs under `docs/`, with extra attention to TSP, custom phases,
  dynamic mode, traffic groups, localization, diagnostics, and save format
- `.github` issue and pull request templates

Historical implementation plans under `docs/superpowers/specs/` were treated as
planning records. They were scanned for conflicting current-facing claims, but
not rewritten as live user documentation.

## Verified From Current Code Or Repo Contracts

- Junction support is topology-gated, not limited to only three-way and
  four-way junctions. `UISystem.UIBIndings.cs` always exposes Vanilla and Custom
  Phases for selected signalized junctions, while `PredefinedPatternsProcessor`
  hides predefined advanced modes when topology checks fail.
- Protected Left/Right-Turns require a four-approach junction whose approaches
  have straight-through car/public-car/track lanes, and they are rejected for
  track-turn cases.
- Split-phasing variants are rejected for rail/track junctions and for
  junctions with more than seven connected edges.
- Pedestrian Phase Duration is a multiplier. The UI uses the
  `CustomPedestrianDurationMultiplier` label and `x` suffix, and the runtime
  multiplies the base pedestrian green duration.
- TSP tram and bus controls are separate source toggles. Disabling one source
  leaves the saved `TransitSignalPrioritySettings` component in place when the
  other source remains enabled; disabling both removes it.
- Bus priority is represented internally as `TspSource.PublicCar`, has lower
  source priority than tram/track requests, and does not use the tram-only
  aggressive preemption path.
- TSP diagnostics are off by default, gated by
  `Settings.m_ShowTransitSignalPriorityDiagnostics`, and exposed through
  selected-intersection rows and optional JSONL trace output.
- Traffic-group members, including leaders, cannot toggle or run local TSP
  while grouped. Saved TSP settings are preserved for use after removal from the
  group.
- The save-format docs match the current serialized TSP source flags and
  component-removal behavior after both TSP sources are disabled.

## Updated In This Pass

- `GUIDE.md`: replaced the inherited three-way/four-way limitation with
  topology-gated wording.
- `GUIDE.md`: clarified Pedestrian Phase Duration as a multiplier.
- `GUIDE.md`: softened the bus-priority playtest claim to cite repo notes
  rather than presenting fresh verification from this audit.
- `GUIDE.md`: added a note that the inherited screenshots remain tracked by
  issue #91.
- `docs/tsp-architecture.md`, `docs/tsp-diagnostics-audit.md`, and
  `docs/save-format-contract.md`: corrected stale TSP/public-car/bus wording.
- `.github/ISSUE_TEMPLATE/bug_report.md`: updated the diagnostics prompt from
  Tram Signal Priority to Transit Signal Priority.

## Still Requiring In-Game Evidence Or Follow-Up

- Current GUIDE screenshots were not replaced because no current TLE Extended
  screenshots are checked into the repository. This remains tracked by issue
  #91.
- Bus-priority release-readiness and mixed-lane behavior are supported by
  current repo playtest notes and policy tests, but this audit did not perform a
  fresh in-game playtest.
- The How To Use flow was checked against current UI/tool code, but the exact
  current in-game visual flow should be confirmed when refreshing screenshots.
- Compatibility with the current Cities: Skylines II version still needs the
  normal release-readiness check before any public release, as already stated in
  `README.md`.
