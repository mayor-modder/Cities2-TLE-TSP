# Cities2-TLE-TSP

`Cities2-TLE-TSP` is a fork of [Traffic Lights Enhancement](https://github.com/bruceyboy24804/Cities2-TrafficLightsEnhancement) for Cities: Skylines II. This fork keeps upstream TLE as the base traffic-light mod and adds fork-specific work around Transit Signal Priority (TSP), plus a small UI cleanup. This README is intentionally fork-first: it documents what we can verify in this repository today and calls out where we are still uncertain.

## Status

- This is not the original TLE repository.
- Upstream for this fork is [bruceyboy24804/Cities2-TrafficLightsEnhancement](https://github.com/bruceyboy24804/Cities2-TrafficLightsEnhancement).
- The workflow for this fork is to pull upstream fixes selectively and carry local changes here, rather than develop features in the upstream repo.
- Inherited docs such as [GUIDE.md](GUIDE.md) and [BUILD.md](BUILD.md) are still useful background, but they were written for the upstream project and are not the authoritative description of this fork.

> [!WARNING]
> This fork changes traffic-light decision logic and stores extra serialized per-junction data for TSP. We have not yet produced a fork-specific compatibility matrix for saves, downgrade paths, or every supported junction layout. Treat it as experimental.

## What This Fork Changes

- Adds Transit Signal Priority settings and runtime request handling on top of upstream TLE. This branch slice only keeps TSP active on standalone intersections.
- Adds extracted pure TSP logic under `TrafficLightsEnhancement.Logic` with xUnit coverage in `TrafficLightsEnhancement.Tests`.
- Adds a small UI cleanup by switching the main panel to a shared floating-button component and covering that behavior with a Node regression test.
- Keeps the upstream TLE signal modes, custom phases, traffic groups, and green-wave systems as the base that TSP builds on. This README does not try to fully re-document those inherited features.

## Transit Signal Priority

### Confirmed behavior

- TSP is stored per junction in a dedicated serialized `TransitSignalPrioritySettings` component and defaults to off.
- The current junction UI exposes `Enable Transit Signal Priority`, `Allow Tram and Track Requests`, and `Allow Bus Lane Requests`.
- When a junction belongs to a traffic group, the junction UI also exposes `Propagate Requests to Coordinated Group`.
- In this branch slice, the traffic-group panel shows coordinated TSP as unavailable.
- Transit Signal Priority is only available on standalone intersections.
- Intersections that are part of a traffic group keep their saved TSP settings, but TSP stays inactive while grouped.
- The runtime generates local TSP requests from eligible track lanes and public-only car lanes. In practice, the current bus/public-transit path is the public-only car-lane path.
- Requests are mapped onto the junction's existing signal groups. This fork does not create a separate TSP-only phase plan or a second corridor editor.
- Custom-phase junctions can either keep the current phase briefly when it already serves the request or bias the next-phase choice toward a phase that does.
- Non-custom / built-in signal patterns also have an explicit signal-group override path for TSP; the request is not limited to the custom-phase branch.
- Coordinated traffic groups can forward TSP requests from follower junctions to the leader when propagation is enabled.
- The serialized TSP settings include a request horizon and a maximum green extension. The current code defaults them to `120` ticks and `45` ticks respectively.

### How it works

1. During simulation, a junction with TSP enabled inspects eligible sub-lanes that already have lane-signal demand.
2. If an eligible track lane or public-only lane is requesting service, the runtime creates a short-lived request that targets one of the junction's existing signal groups.
3. New requests are latched for a short window instead of dropping immediately, so transient gaps do not instantly clear priority.
4. If the active signal group already serves the requested movement and extension is allowed, TSP can hold that group briefly.
5. Otherwise the base next-phase or next-signal-group choice is overridden toward the target that serves the request.
6. In coordinated groups, followers can contribute requests and the leader consumes the strongest active request while the existing coordination and green-wave logic remains the base timing model.

### Known limits and uncertainty

- We have verified the code paths for local request generation, custom-phase selection, and built-in signal-group override in this repository. We have not yet completed a full in-game validation matrix across every junction geometry, traffic pattern, and save state, and this README should not be read as claiming coordinated/grouped TSP is active in this slice.
- The current UI exposes on/off and source-selection toggles, but it does not expose tuning controls for the internal request-horizon and max-extension values.
- The inherited [GUIDE.md](GUIDE.md) still describes the upstream TLE modes and workflow, but it does not fully document the TSP additions in this fork.
- The older upstream README claimed compatibility with a specific game version and referenced specific release and translation workflows. We have not re-validated those claims for this fork, so we are not repeating them here.
- We have not yet published a fork-specific downgrade guide. Because TSP settings are serialized into save data, you should assume downgrade risk exists unless you have tested your exact scenario yourself.

## Build And Installation Notes

- The main mod project targets `net48`.
- The extracted TSP test project targets `net8.0`.
- The UI package requires Node.js `>=18` and builds with webpack.
- The main mod project imports CSII modding targets from `CSII_TOOLPATH`, so the Cities: Skylines II modding toolchain still needs to be installed and configured.
- [BUILD.md](BUILD.md) is still the closest thing to a build recipe in this repo. It is inherited from upstream and useful as a starting point, but it has not yet been rewritten as a fork-specific build guide.
- We have not yet documented a fork-specific packaged release channel or manual-install flow. For now, the safest assumption is that local build and local mod installation are the supported path for this repo.

## References

- [GUIDE.md](GUIDE.md): inherited upstream usage guide for the base TLE feature set
- [BUILD.md](BUILD.md): inherited upstream build notes
- [docs/plans/2026-03-07-transit-signal-priority-design.md](docs/plans/2026-03-07-transit-signal-priority-design.md): fork design notes for TSP
- [docs/plans/2026-03-07-transit-signal-priority.md](docs/plans/2026-03-07-transit-signal-priority.md): fork implementation plan for TSP

## Acknowledgements

This fork is built on top of the upstream TLE codebase and its contributor history. It also still depends on the same broader ecosystem pieces the upstream project relied on, including the Cities: Skylines II modding toolchain, Harmony, and the inherited TLE traffic-light systems and UI structure.
