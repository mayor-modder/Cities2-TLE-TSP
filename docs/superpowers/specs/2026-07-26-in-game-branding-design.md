# In-game branding design

## Goal

Present the mod as **Traffic Lights Enhancement** everywhere players see its
name in Cities: Skylines II. Keep **TLE Extended** as the repository and
development-project qualifier.

## Scope

Update packaged player-facing sources:

- the UI manifest display name;
- the main traffic-light panel title;
- options-section and input-map labels;
- in-game dialogs, descriptions, and fallback messages;
- embedded locale dictionaries; and
- publish metadata shown to players.

The product name is not translated, so embedded locale dictionaries should use
the same `Traffic Lights Enhancement` brand while retaining their surrounding
translated text.

## Compatibility boundaries

Do not change:

- the repository name or maintainer-facing documentation that describes TLE
  Extended;
- the `C2VM.TrafficLightsEnhancement` assembly, namespace, mod id, localization
  keys, UI binding identifiers, or save-facing identifiers;
- the local deployment-folder name; or
- the beta-only `TLE Beta` panel title.

## Implementation

Replace the full `Traffic Lights Enhancement Extended` brand in packaged
player-facing resources with `Traffic Lights Enhancement`. Replace player-facing
`TLE Extended` self-references with either `Traffic Lights Enhancement` or
`TLE`, choosing the form that reads naturally without changing the message's
meaning.

Keep the change textual and localized to existing branding sources. Do not add
a shared branding abstraction or refactor unrelated localization code.

## Verification

Update existing compatibility and UI tests first so they expect the shorter
display name and fail against the current implementation. Add coverage that
scans packaged in-game resources for the retired extended brand while excluding
repository-only documentation.

After implementation:

- run the UI tests and relevant compatibility tests;
- build the production UI bundle;
- run the full test suite;
- with the game closed, build Release and verify the installed assembly and UI
  bundle contain the shorter player-facing name; and
- launch the game to confirm the options entry and panel header display
  `Traffic Lights Enhancement`.
