# Localization Resource Audit

This audit maps the inherited localization/resource paths in TLE Extended so
future cleanup does not remove a path that is still used by the game, the React
UI, or a translation workflow.

## Current Runtime Path

`TrafficLightsEnhancement/Locale.json` is the active game-side localization
source.

`Mod.OnLoad()` registers:

```csharp
new LocaleHelper(modName + ".Locale.json").GetAvailableLanguages()
```

The project file embeds `Locale.json` and sibling files under
`TrafficLightsEnhancement/Locale/*.json`. The sibling files come from the
upstream Crowdin translation branch and are normalized with English fallback
strings for current TLE Extended keys that Crowdin did not see. The base
English `Locale.json` dictionary remains registered as `en-US`.

The active React UI also translates through Cities II's localization manager.
Source search shows UI components calling `useLocalization()` and translating
`UI.LABEL[...]` or `Tooltip.LABEL[...]` keys. New in-game UI text should
therefore be added to `Locale.json` first.

## Resource Inventory

| Resource surface | Loader | Current status | Notes |
| --- | --- | --- | --- |
| `TrafficLightsEnhancement/Locale.json` | `LocaleHelper` from `Mod.OnLoad()` | Active | Owns option labels, option descriptions, warnings, tooltips, and UI labels. |
| `TrafficLightsEnhancement/Locale/*.json` | `LocaleHelper` resource scan | Active | Embedded sibling dictionaries imported from upstream Crowdin output. See `docs/crowdin-localization-audit.md` for coverage and fallback details. |
| `TrafficLightsEnhancement/Resources/Localisations/*.json` | removed | Removed unused backend dictionaries | The active loader is `LocaleHelper`; source tests now guard against reintroducing this legacy resource path. |
| `TrafficLightsEnhancement/Utils/LocalisationUtils.cs` | removed | Removed unused backend loader | Code search found no production caller constructing the class or calling its helpers. |
| `TrafficLightsEnhancement/UI/src/mods/localisations/*.ts` | removed | Removed unused fallback dictionaries | Current UI components use `useLocalization()`, not the old TypeScript `getString()` helper. Do not reintroduce a parallel UI-only string source without tests and docs. |
| `crowdin.yml` | Crowdin | Needs follow-up before automated sync | The source pattern is `Locale*.json`; the manual Crowdin import landed under the embedded `TrafficLightsEnhancement/Locale/*.json` path. Verify the project integration before relying on automated import/export. |

## Cleanup Opportunities

The remaining plausible cleanup tasks should still be handled as focused
changes:

- Keep the removed TypeScript `getString()` dictionaries out of the UI unless a
  future feature intentionally needs a separate fallback source and adds tests
  proving that source is used.
- Consolidate supported-locale lists. Locale identifiers are currently repeated
  in `LocaleHelper` and localization documentation.
- Add a source test that flags accidental reintroduction of UI text paths that
  bypass `Locale.json`.

## Risks

Deleting legacy resources without proof could break inherited Crowdin output or
a runtime code path that is not obvious from static search. The completed
backend-resource removal followed this sequence:

1. Add or update tests that prove the active loader path.
2. Build the mod and inspect embedded resources.
3. Smoke test the options screen and main panel in-game.
4. Remove exactly one legacy path in a focused commit.

`Locale.json` remains the live source of truth. Future localization cleanup
should preserve that contract unless the loader is intentionally refactored.
