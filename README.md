## TLE Extended

TLE Extended is a compatible extended fork of Traffic Lights Enhancement for Cities: Skylines II. It starts from the rewritten TLE codebase and adds Transit signal priority for trams and buses, with a longer-term focus on compatibility, maintainability, documentation, diagnostics, and broader transit-priority work.

Current capabilities include:

* Set traffic lights to predefined signal modes.
* Configure custom traffic-light phases at supported junctions.
* Enable optional Transit signal priority for trams or buses on selected intersections. Buses on a dedicated bus lane can cut a conflicting phase short like trams; in mixed lanes they wait for a normal phase change.
* Use opt-in diagnostics when investigating selected-junction behavior, including Transit signal priority.

See the [guide](GUIDE.md), [roadmap](ROADMAP.md), [TSP architecture notes](docs/tsp-architecture.md), [traffic group notes](docs/traffic-groups.md), [custom phase data flow](docs/custom-phase-data-flow.md), and [save-format contract](docs/save-format-contract.md) for the current direction of the project. Coding agents should start with [AGENTS.md](AGENTS.md) and the [agent workflow notes](docs/agent-workflow.md).

## Status

This repository is public, but TLE Extended is not currently being publicized or published on Paradox Mods. For now, treat it as a source-built local mod for testing and development.

Compatibility with the current Cities: Skylines II version should be verified before any public release.

## Compatibility

TLE Extended is intended to be a drop-in replacement for Traffic Lights Enhancement. A city with intersections already configured in TLE should continue to load and preserve those intersection settings when switching to TLE Extended.

The mod stores extra data in saves to provide additional functionality. If the mod is removed, traffic lights and junctions should usually revert to default settings when a road update is triggered, but this cannot be guaranteed.

You should not downgrade a save from TLE Extended to an older TLE build after saving with this fork. Cities saved with newer data may not be compatible with previous versions.

> [!WARNING]
> These modifications are experimental. Back up important saves before testing, especially while TLE Extended is still source-built and not packaged for public release.

## Installation

TLE Extended is not currently distributed through Paradox Mods. Build it from source and install it as a [local mod](https://cs2.paradoxwikis.com/Modding_Toolchain#Local_Mods_Location).

Build instructions are available in [BUILD.md](BUILD.md).

If the mod is installed for local testing but the toolbar button, panel, or
selected-junction controls are missing, use the
[mod loading and UI visibility troubleshooting guide](docs/mod-loading-ui-troubleshooting.md)
to collect the evidence needed to separate install, playset, UI initialization,
and selected-junction visibility problems.

## Translations

This fork inherits Traffic Lights Enhancement's localization structure and Crowdin workflow. Some inherited strings intentionally use English fallback text until translations are available. New or changed user-facing strings should follow the [localization workflow](docs/localization-workflow.md) and avoid ad-hoc translation churn.

## Acknowledgements

TLE Extended builds on Traffic Lights Enhancement and the work of its original contributors. This mod would not have reached its current stage without help from the following people and projects:

[slyh](https://github.com/slyh), creator of the original Traffic Lights Enhancement mod.

[bruceyboy24804](https://github.com/bruceyboy24804), maintainer of Traffic Lights Enhancement, whose current codebase this fork builds on.

[Cities2Modding](https://github.com/optimus-code/Cities2Modding): An example mod for starting modding in Cities: Skylines II **(BepInEx-based template, now obsolete)**

[Harmony](https://github.com/pardeike/Harmony): A library for patching, replacing and decorating .NET and Mono methods during runtime

[Material Design Icons](https://github.com/google/material-design-icons): Icons for in-game UI and textures

[PickledDragon](https://github.com/EisbarGFX) and [Rebecca](https://github.com/slash-under) for their insights into the lane system's inner workings.

[Krzysztof](https://github.com/krzychu124) for devising a solution that allows multiple systems with the same functionality to coexist, and for advising on UI performance issues.

[Primeinc](https://github.com/primeinc) and [Windows200000](https://github.com/Windows200000) for the original guide.

Additionally, gratitude is extended to the individuals listed below for their translation contributions:

* Chinese (Simplified): [SuperYYT](https://github.com/SuperYYT) and [RilkeXS](https://crowdin.com/profile/rilkexs)

* Chinese (Traditional): [angel84326](https://github.com/angel84326) and the Taiwanese Cities: Skylines community

* Dutch: [Jord38](https://github.com/Jord38), [Randy von der Weide](https://crowdin.com/profile/thesonnyx) and [starrysum](https://crowdin.com/profile/starrysum)

* French: [PsykotropyK](https://github.com/PsykotropyK), [Edou24](https://github.com/Edou24), [Clark](https://crowdin.com/profile/clarkent), [rorobuibui](https://crowdin.com/profile/rorobuibui) and [Morgan Touverey Quilling](https://crowdin.com/profile/mtouverey)

* German: [TheL0ki](https://github.com/TheL0ki), [Simanova86](https://github.com/Simanova86), [Mark](https://crowdin.com/profile/randomkuchen), [fahei](https://github.com/fahei), [KaeseKuchen](https://crowdin.com/profile/kaesemitkuchen) and [PoxiiHD](https://crowdin.com/profile/poxiihd)

* Italian: [Stefano](https://crowdin.com/profile/furios) and [Amazing Amazon](https://crowdin.com/profile/bertrandmati)

* Japanese: [macoto-hino](https://github.com/macoto-hino) and [amanao](https://crowdin.com/profile/amanao)

* Korean: [Twotoolus-FLY-LShst](https://github.com/Twotoolus-FLY-LShst), [DevelopmentAnything](https://github.com/DevelopmentAnything) and [Leo Han](https://crowdin.com/profile/akdls4707)

* Polish: [karmel68](https://crowdin.com/profile/karmel68)

* Portuguese (Brazil): [djotabr](https://github.com/djotabr) and [lucianoedipo](https://github.com/lucianoedipo)

* Russian: [Mellaway](https://github.com/Mellaway), [BuiIdTheBuilder](https://github.com/BuiIdTheBuilder), [Alex Motor](https://crowdin.com/profile/orwester), [Sivenesis](https://crowdin.com/profile/sivenesis) and [KIRFEDO](https://crowdin.com/profile/kirfedo)

* Spanish: [Fabio Rodriguez](https://crowdin.com/profile/elwingcr) and [elGendo87](https://crowdin.com/profile/elgendo87)

Finally, heartfelt thanks to all the players who have submitted bug reports and suggestions.
