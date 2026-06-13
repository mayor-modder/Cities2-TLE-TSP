# Mod loading and UI visibility troubleshooting

Use this guide when a tester reports that TLE Extended is installed but the
toolbar button, selected-intersection panel, or expected controls are missing.
The goal is to classify the report before deciding whether there is a code bug.

TLE Extended is currently a source-built local mod. Start by separating
installation and playset problems from in-game UI state, selected-junction
binding, and topology-based control visibility.

## Evidence to request

Ask for this evidence in the first report:

- Cities: Skylines II game version.
- TLE Extended version from the mod options Version group.
- Build source: branch, commit, or release artifact used to build the local mod.
- Playset state: whether TLE Extended is enabled in the active playset, whether
  the original Traffic Lights Enhancement is also enabled, and whether the game
  was restarted after changing the playset.
- Local mod install state: whether the Release build completed and copied the
  mod into the local mods directory.
- `Player.log` lines containing `TrafficLightsEnhancement`, `OnLoad`,
  `Current mod asset`, `Compatibility mode`, `Failed to patch vanilla traffic
  light query`, or UI/binding exceptions.
- What is missing: toolbar button, panel after pressing the button, controls
  after selecting a junction, or only specific controls such as TSP toggles.
- Selected junction details: simple road intersection, tram-only junction,
  train crossing, complex junction with more than seven connected edges, or
  traffic-group member.
- Diagnostics state: whether Show diagnostics is enabled in the mod options.
- Reproduction steps from launching the game to observing the missing UI.
- Screenshots of the playset, mod options Version group, toolbar area, selected
  panel, and selected junction when practical.

If Show diagnostics is enabled and the panel opens for the selected junction,
also request the visible diagnostics rows and the JSONL trace file:

```text
<Cities II persistent data path>\C2VM.TrafficLightsEnhancement.TspDiagnostics.jsonl
```

On Windows this is usually under the game's `LocalLow` folder. Do not ask
testers to keep diagnostics enabled for normal play; it is an opt-in
troubleshooting aid.

## Classification flow

### 1. No TLE entry in the game options

Likely class: install, build, or playset failure.

Evidence to check:

- The active playset includes TLE Extended.
- The local mod directory contains the build output from the intended commit.
- The game was restarted after enabling or replacing the local mod.
- `Player.log` contains `OnLoad` from `C2VM.TrafficLightsEnhancement.Mod`.

Code boundary:

- `TrafficLightsEnhancement/Mod.cs` logs `OnLoad`, logs the current mod asset
  path when available, registers settings, key bindings, localization, and
  schedules the UI systems.

Do not start with selected-panel or React debugging if the mod options entry is
absent. The UI bundle and selected-junction bindings are downstream of mod load.

### 2. TLE options exist but the top-left button is missing

Likely class: UI registration or UI bundle failure.

Evidence to check:

- `Player.log` includes `OnLoad` and `Current mod asset`.
- The active mod version in the options Version group matches the build under
  investigation.
- `Player.log` has UI, cohtml, binding, or JavaScript exceptions near startup.
- The UI build output was included in the local mod build.

Code boundary:

- `TrafficLightsEnhancement/UI/src/index.tsx` appends the React app to
  `GameTopLeft`.
- `TrafficLightsEnhancement/UI/src/mods/app.tsx` mounts the main panel, custom
  phase tool, and migration modal.
- `TrafficLightsEnhancement/UI/src/mods/components/main-panel/index.tsx`
  always renders the floating Traffic Lights Enhancement button. The C# binding
  controls whether the panel is visible, not whether the button is created.

If options exist but the button is absent, collect logs before changing selected
junction code. A selected-entity failure should not remove the button.

### 3. Button exists but the panel does not open

Likely class: C# UI trigger/binding failure, panel state failure, or JavaScript
exception.

Evidence to check:

- Whether clicking the button changes its selected state.
- Whether the main panel toggle key binding opens the panel.
- `Player.log` binding or JavaScript exceptions after clicking the button.
- Whether `GetMainPanel` binding data appears malformed in the log.

Code boundary:

- `UISystem.OnCreate()` calls `AddUIBindings()`.
- `UISystem.UIBIndings.cs` registers `GetMainPanel` and the `SetPanelState`
  trigger.
- React calls `SetPanelState(Empty)` or `SetPanelState(Hidden)` from the
  floating button.
- `UISystem.SetMainPanelState()` updates `m_MainPanelState`, refreshes the
  main-panel binding, redraws the icon, and enables or disables tool/update
  systems.

This class is the narrowest place to consider lightweight instrumentation, but
only after logs show the mod and UI bundle loaded.

### 4. Panel opens but says to select a junction

Likely class: no selected eligible traffic-light junction.

Evidence to check:

- Tester clicked a signalized junction while the panel was open.
- The clicked object is a junction, not a road segment or non-signalized node.
- Tool selection mode was active and no other tool stole the selection.
- `Player.log` contains no exceptions after clicking the junction.

Code boundary:

- `UISystem.ChangeSelectedEntity()` changes the panel from Empty to Main when a
  non-null entity is selected.
- `ToolSystem` owns raycast selection and calls `ChangeSelectedEntity()`.

If diagnostics cannot be enabled because the panel never reaches a selected
junction, ask for a screenshot or clip of the click target and the toolbar/panel
state.

### 5. Panel opens but expected controls are missing

Likely class: expected topology or traffic-group gating.

Evidence to check:

- Selected junction topology: edge count, train tracks, tram-only lanes, road
  vehicle lanes, and whether the junction is part of a traffic group.
- Selected mode: Vanilla, split phasing, protected turns, custom phases, or a
  traffic-group view.
- Diagnostics rows for junction topology, available patterns, extra options,
  tram control, and bus control.
- JSONL `selectedJunction` object when diagnostics are enabled.

Code boundary:

- `UISystem.UIBIndings.cs` computes selected-junction visibility in
  `GetSelectedJunctionDiagnosticsSnapshot()`.
- Extra options are hidden for train-track topology, junctions with more than
  seven connected edges, and pattern modes that do not support them.
- Vehicle turn options are hidden when the selected junction has no road
  vehicle lanes.
- Tram TSP is visible only when the selected junction has tram track lanes.
- Bus TSP is visible only when the selected junction has road vehicle lanes.
- Traffic-group members cannot edit local TSP. The panel should show grouped
  status text while preserving saved TSP settings.

This is not automatically a bug. The diagnostics rows should explain expected
hidden controls. File a smaller implementation issue only when diagnostics show
the selected topology should expose a control but the React panel does not.

## Report summary template

Maintainers can paste this into an issue comment after triage:

```markdown
### Loading/UI classification

- Game version:
- TLE Extended version:
- Build branch/commit:
- Active playset includes TLE Extended:
- Original TLE also enabled:
- `Player.log` has `OnLoad`:
- `Player.log` has UI/binding errors:
- Missing surface: button / panel / selected controls
- Selected junction:
- Diagnostics enabled:
- Diagnostics/trace attached:

Classification:

Next action:
```

## When to create follow-up issues

Create a smaller bug issue when the evidence narrows to one of these:

- Install and playset are confirmed, options exist, but the button is missing
  with UI bundle or JavaScript errors.
- Button exists, but `SetPanelState` or `GetMainPanel` fails with a binding
  exception.
- Tool selection reaches a valid signalized junction, but `ChangeSelectedEntity`
  does not produce selected main-panel data.
- Diagnostics show a control should be visible, but React does not render it.
- Diagnostics show stale or contradictory topology/control visibility data.

Keep broad source-built install problems in the original report until the build,
local mod copy, active playset, and game restart are confirmed.
