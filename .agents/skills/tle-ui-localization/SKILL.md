---
name: tle-ui-localization
description: "Use when changing TLE Extended React UI, TypeScript bindings, C# UI binding payloads, panel diagnostics, mod option labels, tooltips, localization keys, Locale.json, or user-facing text."
---

# TLE UI Localization

## Overview

The UI is split between C# binding payloads and a React/TypeScript front end. `TrafficLightsEnhancement/Locale.json` is the live source of truth for user-facing strings.

## Ownership Map

| Area | Path | Notes |
| --- | --- | --- |
| C# selected-panel data | `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs` | Builds main panel data, TSP toggles, diagnostics rows, and JSONL traces. |
| UI system shell | `TrafficLightsEnhancement/Systems/UI/UISystem.cs` | Registers triggers, update behavior, panel refreshes, and lifecycle. |
| React main panel | `TrafficLightsEnhancement/UI/src/mods/components/main-panel` | Renders signal controls, TSP source toggles, diagnostics, and notices. |
| UI bindings/types | `TrafficLightsEnhancement/UI/src/mods`, `TrafficLightsEnhancement/UI/src/types` | Keep TypeScript expectations aligned with C# payloads. |
| Styling | `*.module.scss`, common components | Prefer existing component patterns and compact in-game UI density. |
| Localization | `TrafficLightsEnhancement/Locale.json` | Base English strings and live game localization keys. |

## Localization Rules

- Read `docs/localization-workflow.md` before adding or moving user-facing text.
- Add base strings to `Locale.json`; do not create UI-only fallback dictionaries.
- React UI should use Cities II `useLocalization()` and keys from `Locale.json`.
- New visible settings in `Settings.cs` need both:
  - `Options.OPTION[C2VM.TrafficLightsEnhancement.C2VM.TrafficLightsEnhancement.Mod.Settings.<Name>]`
  - `Options.OPTION_DESCRIPTION[C2VM.TrafficLightsEnhancement.C2VM.TrafficLightsEnhancement.Mod.Settings.<Name>]`
- Do not remove inherited `.tooltip` keys during unrelated cleanup.
- Keep new English strings clear and stable for Crowdin; do not hand-translate sibling locale files during feature work unless the task is specifically translation.
- Use sentence case for user-facing titles and UI labels: capitalize only the first word and genuine proper nouns (product names like Traffic Lights Enhancement / TLE Extended, acronyms like TSP, and code identifiers such as `CustomTrafficLights`). Feature, mode, and template names (for example "Split phasing", "Custom phases", "Transit signal priority", "Traffic groups") are not proper nouns. This applies consistently to `Locale.json`, hardcoded C# dialog/message strings, and `GUIDE.md`/`README.md` headings, which should match each other.

## Binding Rules

- Keep C# payload names and TypeScript access in sync.
- TSP has independent tram and bus source controls. Preserve separate visibility, enabled/editable state, and status labels.
- Traffic-group members cannot toggle local TSP, including the group leader. Saved TSP settings are preserved and resume if the junction leaves the group.
- Diagnostics rows and recent events are optional and should only be built/refreshed when diagnostics are enabled.
- JSONL trace output is a selected-panel diagnostic aid, not gameplay state.

## UI Style

- Match existing in-game controls instead of adding landing-page or marketing-style UI.
- Use existing common components before adding new ones.
- Keep panel text compact; long diagnostic labels belong in `GUIDE.md` when they need explanation.
- When changing SCSS, verify compact widths and avoid text overlap in the panel.

## Verification

Run `npm test` in `TrafficLightsEnhancement/UI` for panel logic changes. If UI payload shape changes, include C# tests or build checks that exercise the binding producer. Update `GUIDE.md` when player-visible controls or diagnostics labels change.
