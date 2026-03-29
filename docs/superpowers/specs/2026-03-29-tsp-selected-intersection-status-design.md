# TSP Selected Intersection Status Design

## Summary

This design adds a lightweight live status readout for Transit Signal Priority (TSP) to the existing TLE main panel. The status appears only for the currently selected intersection, and only when Transit Signal Priority is enabled for that intersection.

The goal is to make TSP observable during in-game testing without introducing an always-on overlay or a separate diagnostics workflow.

## Terminology

- **Transit Signal Priority (TSP):** The feature that allows eligible transit vehicles to request an early phase selection or an extension of the current green.
- **Selected intersection:** The intersection currently shown in the TLE main panel.
- **Standalone intersection:** An intersection that is not part of a traffic group.
- **Grouped intersection:** An intersection that has `TrafficGroupMember` and therefore cannot run TSP in the current product slice.
- **Status row:** A read-only line in the TLE main panel that summarizes the current TSP state for the selected intersection.

## Problem Statement

TSP is currently difficult to verify by sight alone. Following a tram or bus and trying to judge whether a signal changed "because of TSP" is unreliable because:

- TSP often holds or advances a phase subtly rather than causing a dramatic visible jump
- request timing depends on the live lane-signal petitioner state
- grouped intersections intentionally suppress TSP entirely
- there is no in-panel feedback that explains whether a request exists or what action the system is taking

The code already computes the relevant request and decision state, but that state is not surfaced to the player in a focused, easy-to-read way.

## Product Decision

Add a single live TSP status row to the TLE main panel for the selected intersection.

The row will:

- appear only when Transit Signal Priority is enabled for the selected intersection
- remain hidden when TSP is disabled
- use plain-language status text rather than raw debug values
- include a little more detail than a simple on/off badge
- stay scoped to the selected intersection instead of adding a global overlay

## User Experience

### Visibility Rules

The status row is shown only when all of the following are true:

- an intersection is selected
- that intersection has TSP enabled in its saved settings
- the TLE main panel is showing the TSP section

The status row is hidden when:

- no intersection is selected
- the selected intersection does not have TSP enabled

### Grouped Intersections

If TSP is enabled in saved settings but the selected intersection belongs to a traffic group, the status row still appears because the user has TSP enabled for that intersection. In that case the text should explain why TSP is inactive.

Recommended grouped text:

`Transit Signal Priority inactive: this intersection is part of a traffic group.`

This complements the existing disabled controls and message rather than replacing them.

### Status Vocabulary

The status should read like gameplay feedback, not internal ECS diagnostics.

Recommended status phrases:

- `No active request`
- `Tram request`
- `Bus request`
- `Holding current green`
- `Advancing to signal group 3`

If a request is active, the status should favor action-oriented text over passive state whenever possible. For example, `Holding current green` is more useful than only reporting `Tram request`.

## Data Source

The status row should reuse live runtime state that already exists in the simulation path instead of inventing a separate parallel tracker.

Preferred source order:

1. Use the current `TransitSignalPriorityRequest` on the selected intersection when one exists.
2. Use transient decision-trace data when it is already available and helps distinguish between "request exists" and "what action was taken."
3. Fall back to a stable inactive message when no request or action data exists.

The design should not require the diagnostics system to stay permanently enabled just to support the status row.

## State Mapping

The panel should map runtime state to a single concise display string.

Recommended mapping rules:

- no request and runtime eligible: `No active request`
- request source is track and action is not otherwise known: `Tram request`
- request source is public car and action is not otherwise known: `Bus request`
- request extends the current phase: `Holding current green`
- request selects a different target phase: `Advancing to signal group N`
- runtime ineligible because the intersection is grouped: `Transit Signal Priority inactive: this intersection is part of a traffic group.`

If both source and action are known, the displayed text should prioritize the action because it answers the testing question more directly.

## UI Shape

The status row should live within the existing TSP section in the main panel.

Design constraints:

- read-only presentation
- no extra buttons
- no always-on tooltip requirement
- consistent visual treatment with the rest of the panel
- easy to scan while watching the junction in-game

The row can use a label such as `Status` with the value text beside or beneath it, following the established panel style.

## Scope

### In Scope

- add a selected-intersection TSP status row to the main panel
- surface grouped-intersection inactivity through the same row when TSP is enabled
- map live request/action state to a concise player-facing status string
- add tests for status mapping and binding behavior

### Out of Scope

- always-on world overlays
- new debug windows or modals
- persistent logging changes
- redesigning the TSP request pipeline
- new global diagnostics toggles

## Implementation Notes

- The status row should be driven by a dedicated binding shape rather than baking string formatting directly into the React component.
- The binding model should separate raw runtime facts from localized display text so tests can cover mapping rules cleanly.
- Grouped-intersection messaging should reuse the existing terminology: `Transit Signal Priority` and `traffic group`.
- The implementation should avoid adding per-frame heavy scans outside the already selected intersection context.

## Testing Strategy

We need both mapping coverage and a light UI-binding verification layer.

### Logic and Binding Tests

- TSP disabled returns no status row payload
- standalone enabled intersection with no request maps to `No active request`
- track request maps to `Tram request` when no stronger action is known
- public-car request maps to `Bus request` when no stronger action is known
- extension action maps to `Holding current green`
- phase-selection action maps to `Advancing to signal group N`
- grouped enabled intersection maps to the inactive grouped message

### Manual In-Game Checks

- standalone intersection with TSP enabled shows the status row
- grouped intersection with TSP enabled shows the inactive grouped message
- toggling TSP off hides the row
- an approaching tram or bus can change the row from `No active request` to an active/action state during play

## Risks

- If the available runtime data does not always tell us the exact action taken, some states may need to fall back to source-oriented text such as `Tram request`.
- If the row updates too noisily frame to frame, it may feel flickery; the implementation may need to stabilize wording around the current request/action snapshot.
- Reusing diagnostics-only state too directly would risk coupling a user-facing feature to debug infrastructure, so the implementation should keep that dependency minimal.

## Recommendation

Implement the selected-intersection status row as a lightweight panel enhancement. It gives us much better TSP observability during manual testing, stays aligned with the current standalone-only TSP model, and avoids the downsides of an always-on overlay.
