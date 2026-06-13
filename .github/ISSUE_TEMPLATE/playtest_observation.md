---
name: Playtest observation
about: Record in-game observations that should guide future behavior
title: ''
labels: 'type:research,lane:needs-playtest'
assignees: ''
---

## Scenario

Describe the junction, save, and traffic pattern being observed.

- Game version:
- TLE Extended version:
- Build branch or commit:
- Active playset includes TLE Extended:
- Original Traffic Lights Enhancement also enabled:
- City/save:
- Junction type:
- Relevant modes/options enabled:
- Transit involved: tram, bus, or both

## What You Observed

Describe what happened in-game. Include screenshots, clips, or trace snippets when useful.

## Expected or Desired Behavior

Describe what you think the mod should do in this situation, if known.

## Diagnostics

For reports where the mod is installed but the toolbar button, selected panel,
or controls are missing, include:

- Missing surface: toolbar button, panel, selected-junction controls, or a
  specific control:
- Playset state and whether the game was restarted after playset changes:
- `Player.log` lines containing `TrafficLightsEnhancement`, `OnLoad`,
  `Current mod asset`, `Compatibility mode`, or UI/binding errors:
- Diagnostics enabled:
- Reproduction steps from game launch:

For Transit Signal Priority diagnostics, include:

- Selected entity:
- Signal state/current group/next group:
- Request/source/target group:
- Probe/index rows:
- Recent event rows:
- JSONL trace snippet, if available:

## Follow-Up

What should happen next?

- [ ] Documentation update
- [ ] New implementation issue
- [ ] More playtesting
- [ ] No action; observation only
