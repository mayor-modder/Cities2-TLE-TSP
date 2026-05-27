# Inherited Documentation Accuracy Audit

This note records the issue #92 documentation accuracy pass. The goal was to
check inherited current-facing claims against current repo evidence, not to
rewrite the docs for style.

Historical implementation plans under `docs/superpowers/specs/` are treated as
planning records. They were scanned for conflicting current-facing claims, but
not rewritten as live user documentation.

## Broad Scan Matrix

| Area | Files checked | Result |
| --- | --- | --- |
| Project identity and release status | `README.md`, `BUILD.md`, `ROADMAP.md` | Current wording matches the repo posture: TLE Extended is source-built/local for now, not publicized on Paradox Mods, while retaining inherited `C2VM.TrafficLightsEnhancement` assembly/root identifiers for compatibility. |
| Player guide | `GUIDE.md` | One inherited topology claim and one ambiguous pedestrian-duration description were stale; both were updated. The old screenshots are flagged but not replaced in this pass. |
| Custom phases and dynamic mode | `docs/custom-phase-data-flow.md`, `docs/dynamic-mode.md`, `docs/custom-state-machine-no-tsp-regression.md`, `docs/custom-phase-selection-extraction.md` | Claims match the current architecture docs and code shape: custom phases use `CustomTrafficLights`, `CustomPhaseData`, `EdgeGroupMask`, and `SubLaneGroupMask`; dynamic durations are signal update ticks despite `s` UI labels; pure extraction remains a recommendation rather than completed work. |
| Traffic groups | `docs/traffic-groups.md`, `GUIDE.md`, `docs/dynamic-mode.md`, `docs/custom-phase-data-flow.md` | Current docs now consistently say local TSP is suspended for every grouped junction, including the leader, while saved settings are preserved. |
| TSP, diagnostics, and bus priority | `GUIDE.md`, `ROADMAP.md`, `docs/tsp-architecture.md`, `docs/tsp-diagnostics-audit.md`, `docs/transit-signal-priority-bus-research.md` | Stale tram-only wording was replaced where it was current-facing. Bus priority is documented as a conservative soft MVP backed by existing tests and playtest notes, not by a fresh playtest from this audit. |
| Save format and migration | `docs/save-format-contract.md`, `docs/serialization-and-migration-audit.md`, `README.md` | Current docs match the additive TSP-save posture, inherited serializer coverage, grouped-intersection TSP suspension, and downgrade warning. |
| Localization and user-facing text | `docs/localization-workflow.md`, `docs/localization-resource-audit.md`, `docs/mod-option-descriptions-audit.md`, `TrafficLightsEnhancement/Locale.json` | Current docs match the active `Locale.json` path and the test-backed `Options.OPTION_DESCRIPTION[...]` convention. Legacy `.tooltip` keys remain intentionally preserved. |
| GitHub templates and agent docs | `.github/ISSUE_TEMPLATE/*.md`, `.github/PULL_REQUEST_TEMPLATE.md`, `.github/copilot-instructions.md`, `AGENTS.md`, `docs/agent-workflow.md` | Templates are consistent with current project workflow after replacing remaining current-facing tram-only diagnostics wording. Client-specific agent shims correctly defer to `AGENTS.md`. |

## Verified From Current Code Or Repo Contracts

- Junction support is topology-gated, not limited to only three-way and
  four-way junctions. `UISystem.UIBIndings.cs` always exposes Vanilla and Custom
  Phases for selected signalized junctions, while `PredefinedPatternsProcessor`
  hides predefined advanced modes when topology checks fail.
- Protected Left/Right-Turns require a four-approach junction whose approaches
  have straight-through car/public-car/track lanes, and they are rejected for
  track-turn cases.
- Split-phasing variants are rejected for junctions with more than seven
  connected edges and for connected standalone track edges exposed by the game
  through `TrainTrack`. Roads with embedded tram lanes are evaluated by the
  normal lane-layout checks.
- Pedestrian Phase Duration is a multiplier. The UI uses the
  `CustomPedestrianDurationMultiplier` label and `x` suffix, and the runtime
  multiplies the base pedestrian green duration.
- The dynamic/custom phase docs match the current model: phase indexes are
  signal group indexes minus one, edge/sub-lane masks encode served movements,
  dynamic timing compares raw update ticks, and `CustomStateMachine` still owns
  mutable timer/state transitions.
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
- The save-format docs match the current serialized TSP source flags,
  component-removal behavior after both TSP sources are disabled, and inherited
  traffic-group/custom-phase compatibility posture.
- The localization docs match the current runtime path: `Mod.OnLoad()` loads
  `Locale.json` through `LocaleHelper`, React UI code uses `useLocalization()`,
  and source tests guard visible option descriptions.

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
- `.github/ISSUE_TEMPLATE/bug_report.md` and
  `.github/ISSUE_TEMPLATE/playtest_observation.md`: updated diagnostics prompts
  from tram-only wording to Transit Signal Priority.
- `docs/inherited-documentation-audit.md`: expanded the audit trail beyond TSP
  so future maintainers can see which inherited documentation areas were
  checked and which still need in-game evidence.

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
