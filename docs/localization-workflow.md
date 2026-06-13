# Localization Workflow

This document records the current localization ownership model for TLE
Extended. It is maintainer-facing: use it before adding option labels,
descriptions, tooltips, or translated UI text.

For the resource-path inventory and cleanup risk assessment, see
[`localization-resource-audit.md`](localization-resource-audit.md).

## Live Source Of Truth

`TrafficLightsEnhancement/Locale.json` is the live source of truth for game
localization entries in the current codebase.

`Mod.OnLoad()` registers dictionaries from
`new LocaleHelper(modName + ".Locale.json").GetAvailableLanguages()`. The
project embeds:

- `Locale.json`
- optional sibling locale files under `TrafficLightsEnhancement/Locale/*.json`

`LocaleHelper` loads the base `Locale.json` as `en-US` and then scans embedded
resources whose names share the same base name. Sibling locale files currently
come from the upstream Crowdin translation branch and are normalized with
machine-assisted fallback translations for current TLE Extended keys that were
not present in the upstream source. See
[`crowdin-localization-audit.md`](crowdin-localization-audit.md) and
[`localization-ai-review.md`](localization-ai-review.md).

When an imported Crowdin file name differs from a game locale id, `LocaleHelper`
registers runtime aliases instead of duplicating JSON files. Current aliases are
`pt-PT` to `pt-BR`, `zh-CN` to `zh-HANS`, and `zh-TW` to `zh-HANT`/`zh-HK`.

`Locale.json` owns these live key families:

- `Options.SECTION`, `Options.TAB`, and `Options.GROUP`
- `Options.OPTION[...]` labels
- `Options.OPTION_DESCRIPTION[...]` hover descriptions
- `Options.WARNING[...]` warning text
- `Tooltip.LABEL[...]`
- `UI.LABEL[...]`

Current React UI components call Cities II's `useLocalization()` and translate
`UI.LABEL[...]` or `Tooltip.LABEL[...]` keys from the game localization manager.
They do not use the older TypeScript `getString()` helper as their primary text
source.

## Option Labels And Descriptions

For every visible setting in `Settings.cs`, add or maintain both:

```text
Options.OPTION[C2VM.TrafficLightsEnhancement.C2VM.TrafficLightsEnhancement.Mod.Settings.<OptionName>]
Options.OPTION_DESCRIPTION[C2VM.TrafficLightsEnhancement.C2VM.TrafficLightsEnhancement.Mod.Settings.<OptionName>]
```

`Options.OPTION_DESCRIPTION[...]` is required for the current options hover
description panel. Legacy `.tooltip` keys are not sufficient for that panel.

The Node test suite checks the visible options list against `Locale.json`.
Whenever a new visible option is added, update the test list if needed and add a
non-empty base description.

## Legacy `.tooltip` Keys

Many inherited entries also include:

```text
Options.OPTION[...Settings.<OptionName>].tooltip
```

Do not remove these keys in drive-by localization work. They appear to be legacy
fallbacks rather than the current hover-description path, but removal should
wait for an explicit supported-game-version check and an in-game smoke test that
confirms no supported options UI still reads them.

New visible settings must have `Options.OPTION_DESCRIPTION[...]`. Adding a
matching `.tooltip` key is acceptable for consistency, but it should not be
treated as the authoritative description.

## Removed Resource Localisations

The inherited `TrafficLightsEnhancement/Resources/Localisations/*.json`
dictionaries and `TrafficLightsEnhancement/Utils/LocalisationUtils.cs` loader
were removed because production code did not construct the loader or call its
helpers. They were packaged only through the broad `Resources/**/*` embedded
resource rule.

Do not reintroduce a backend localization loader parallel to `LocaleHelper`
unless the runtime localization strategy is intentionally refactored. Source
tests now guard that the backend localization path stays centered on
`Locale.json`.

## Removed TypeScript Localisation Files

The inherited `TrafficLightsEnhancement/UI/src/mods/localisations/*.ts`
fallback dictionaries and `getString()` helper were removed because production
UI code did not import them. Current UI components translate through Cities II's
localization manager, using keys supplied by `Locale.json`.

Do not add a new UI-only localization dictionary for user-facing text. Add live
keys to `Locale.json` first. If a future UI path intentionally needs a separate
fallback dictionary, document that decision and add tests that prove those files
are used.

## Translation Workflow

Base strings should be written in `Locale.json`. Non-English strings should flow
through the project's translation workflow rather than ad-hoc hand translation
during feature work.

The current `crowdin.yml` lists `Locale*.json` as the source pattern. Before
turning on broad translation updates, verify that Crowdin output lands in the
embedded sibling-locale location expected by the project:

```text
TrafficLightsEnhancement/Locale/*.json
```

Machine-assisted fallback translations are used to avoid raw English fallback
strings in shipped locale files. Do not block feature or hardening work on
hand-translating every locale, but keep new base strings clear and stable so
they are ready for Crowdin.
