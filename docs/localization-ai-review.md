# Machine-Assisted Localization Review

This file tracks machine-assisted translation added after importing the original
Traffic Lights Enhancement Crowdin payload.

Runtime locale files under `TrafficLightsEnhancement/Locale/*.json` intentionally
contain only game localization key/value pairs. Translation provenance lives in
this document and the machine-readable sidecar
[`localization-ai-review.json`](localization-ai-review.json).

## Status

All machine-assisted translations need native-speaker review before they should
be treated as fully reviewed community translations. The current goal is to
avoid raw English fallback text in locales that already had substantial Crowdin
coverage, while keeping the provenance honest.

## AI-Filled Coverage

| Locale | AI-filled keys | Review status |
| --- | ---: | --- |
| `de-DE` | 181 | Needs native-speaker review |
| `es-ES` | 181 | Needs native-speaker review |
| `ko-KR` | 182 | Needs native-speaker review |
| `pl-PL` | 184 | Needs native-speaker review |
| `zh-CN` | 182 | Needs native-speaker review |

Proper names, acronyms, and unit-like strings such as `Traffic Lights
Enhancement`, `TSP`, `Auto`, `x`, and similar labels may intentionally remain
unchanged where that is natural for the target locale.

## Review Guidance

When a native speaker reviews a locale:

1. Use `docs/localization-ai-review.json` to find the machine-assisted keys for
   that locale.
2. Compare each translated value against `TrafficLightsEnhancement/Locale.json`.
3. Prefer compact UI wording over literal prose when both preserve meaning.
4. Pay special attention to traffic-control terms such as signal groups, lanes,
   approach/upstream probes, yielding, tram tracks, and TSP diagnostics.
5. Remove reviewed keys from the sidecar or change the locale status only after
   the reviewed wording is committed.

## Verification

The UI test suite checks that sidecar keys are live localization keys, exist in
the target locale file, and no longer match the English fallback value.
