# Crowdin Localization Audit

This audit records the first import of Crowdin-generated locale dictionaries
from the original Traffic Lights Enhancement project into TLE Extended.

## Source

- Crowdin project: `Cities2-TrafficLightsEnhancement`
- Upstream repository remote: `upstream-tle`
- Upstream translation branch commit: `63779b8`
- Imported path: `TrafficLightsEnhancement/Locale/*.json`

The current runtime localization path already embeds `TrafficLightsEnhancement/Locale.json`
and optional sibling dictionaries under `TrafficLightsEnhancement/Locale/*.json`.
The old `Resources/Localisations` and TypeScript fallback dictionaries remain
intentionally removed.

## Import Shape

The upstream translation branch provided 11 Crowdin output files:

- `de-DE.json`
- `es-ES.json`
- `fr-FR.json`
- `it-IT.json`
- `ja-JP.json`
- `ko-KR.json`
- `pl-PL.json`
- `pt-PT.json`
- `ru-RU.json`
- `zh-CN.json`
- `zh-TW.json`

Each upstream file covered 154 of the 337 live keys in the current
`TrafficLightsEnhancement/Locale.json`. No upstream translation file contained
keys that are obsolete or unknown in the current fork.

The imported files were normalized to all 337 current live keys. Crowdin
translations are preserved where present; keys that Crowdin never saw use the
current English base string as fallback so newer TLE Extended UI strings do not
render as raw localization ids.

## Translation Coverage

| Locale file | Crowdin live keys | Different from English after fallback | English fallback or equal |
| --- | ---: | ---: | ---: |
| `de-DE.json` | 154 | 327 | 10 |
| `es-ES.json` | 154 | 330 | 7 |
| `fr-FR.json` | 154 | 0 | 337 |
| `it-IT.json` | 154 | 0 | 337 |
| `ja-JP.json` | 154 | 0 | 337 |
| `ko-KR.json` | 154 | 335 | 2 |
| `pl-PL.json` | 154 | 332 | 5 |
| `pt-PT.json` | 154 | 0 | 337 |
| `ru-RU.json` | 154 | 0 | 337 |
| `zh-CN.json` | 154 | 333 | 4 |
| `zh-TW.json` | 154 | 0 | 337 |

The languages with substantial submitted translations in the upstream Crowdin
payload are German, Spanish, Korean, Polish, and Simplified Chinese. The other
Crowdin files currently behave as complete English fallback dictionaries.

## Manual QA Follow-Up

After import, a targeted QA pass corrected clear meaning drift in the translated
locales, including track/rail terminology, yielding to oncoming traffic,
group-member wording, phase-change labels, and a few grammar or typo issues.
The UI test suite includes a focused regression check for the highest-risk
traffic-control terms so these corrections are not accidentally overwritten by a
future bulk translation import.

## Verification

`TrafficLightsEnhancement/UI/tests/transit-signal-priority-panel.test.mjs`
checks that the Crowdin dictionaries are present under the embedded sibling
locale path and that each file covers the complete live `Locale.json` key set.
