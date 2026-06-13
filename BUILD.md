## Build Instructions

These instructions build TLE Extended as a local Cities: Skylines II mod. The internal assembly and mod identifiers still use `C2VM.TrafficLightsEnhancement` for compatibility with existing TLE save data and configured intersections.

1. Install [Node.js 20](https://nodejs.org/), [.NET 8.0 SDK](https://dotnet.microsoft.com/download), and the in-game Modding Toolchain.

2. Clone the repository

```shell
git clone git@github.com:mayor-modder/Cities2-TrafficLightsEnhancement-Extended.git
cd Cities2-TrafficLightsEnhancement-Extended
git submodule update --init --recursive
```

3. Build the plugin

```shell
dotnet restore
dotnet build --configuration Release
```

The release build runs the Cities: Skylines II mod post-processor and copies the built mod into the local mod directory when the game is not holding the target files open.

If the build appears installed but the in-game button, panel, or controls are
missing, use the [mod loading and UI visibility troubleshooting guide](docs/mod-loading-ui-troubleshooting.md)
to collect the game version, mod version, playset state, logs, selected junction,
diagnostics state, and reproduction steps before assuming a UI binding bug.
