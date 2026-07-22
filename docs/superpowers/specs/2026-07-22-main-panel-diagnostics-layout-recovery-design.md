# Main panel diagnostics layout recovery design

## Goal

Restore the TLE Extended main-panel layout shown in the June 14, 2026 reference screenshots without changing simulation behavior, diagnostic payloads, localization, or the legacy UI appearance setting.

## Recovered behavior

- When transit signal priority diagnostics are disabled or absent, the main panel remains the existing narrow 18em controls panel.
- When diagnostics are present, the panel expands into two adjacent panes:
  - an 18em controls pane on the left;
  - a 30em diagnostics pane on the right.
- The shared header spans the complete expanded panel.
- The controls and diagnostics panes scroll independently.
- Diagnostic rows appear before recent TSP events, matching the current information order and the reference screenshots.
- Existing panel color and blur variables remain responsible for opacity and backdrop blur. The legacy UI setting must continue to change transparency without selecting a different layout.

## Implementation boundaries

The change is limited to the normal main-panel React structure and its SCSS module:

- `TrafficLightsEnhancement/UI/src/mods/components/main-panel/content.tsx`
- `TrafficLightsEnhancement/UI/src/mods/components/main-panel/mainPanel.module.scss`

The controls currently rendered before and after the diagnostic block remain in the left pane. The diagnostic title, rows, and recent-event list move into a dedicated right-pane `Scrollable`. The right pane is rendered only when `mainData.transitSignalPriority.diagnostics` exists, so the no-diagnostics layout retains its current width.

No C# binding shape, settings behavior, localization keys, traffic-light simulation, save data, or traffic-group panel behavior changes as part of this recovery.

## Styling

The outer content region becomes a horizontal flex container only for the expanded state. Pane widths are fixed to the recovered 18em and 30em proportions. Both panes reuse the current `--panelColorNormal`, `--panelColorDark`, `--panelBlur`, and text color variables rather than hardcoding opacity. Overflow is contained within each pane so long diagnostics do not lengthen or horizontally distort the controls pane.

## Verification

Add a focused UI source test that proves:

- diagnostics render in a dedicated pane outside the controls scroller;
- the dedicated diagnostics pane is conditional on the diagnostics payload;
- the SCSS preserves the 18em controls width and 30em diagnostics width;
- diagnostics rows remain ordered before recent events.

Run the complete UI test suite and production UI build. Do not copy artifacts into the installed Cities: Skylines II mod directory or launch the game until the user explicitly approves that separate step.

## Success criteria

At diagnostics-off state, the panel is the original narrow controls panel. At diagnostics-on state, it matches the supplied screenshots structurally: controls left, diagnostics right, shared header, independent scrolling, and transparency governed by the legacy UI setting.
