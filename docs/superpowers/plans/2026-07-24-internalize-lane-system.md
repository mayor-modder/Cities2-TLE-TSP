# Internalize Lane System implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make TLE Extended the only mod entry point and public package identity while retaining the Lane System assembly as an internal compatibility dependency.

**Architecture:** Remove Lane System's `IMod` implementation, transfer its one startup action to TLE Extended, keep the existing Lane System assembly name and types intact, remove its separate version from settings, and place both DLLs in the single TLE Extended release folder.

**Tech stack:** C#/.NET Framework 4.8, Unity ECS, xUnit, React/TypeScript UI resources, Node test runner, MSBuild, GitHub Actions.

## Global constraints

- Preserve `C2VM.TrafficLightsEnhancement` and `C2VM.CommonLibraries.LaneSystem` assembly names, namespaces, serialized component types, and save compatibility.
- Work only in the isolated parent worktree and a dedicated branch inside the refreshed `CommonLibraries` submodule.
- Use semantic versioning for TLE Extended; do not create a new user-facing Lane System version.
- Keep Markdown prose unwrapped except where Markdown syntax requires line breaks.

---

## Task 1: Make TLE Extended the only mod entry point

**Files:**

- Create: `TrafficLightsEnhancement.Ecs.Tests/Packaging/ModEntryPointTests.cs`
- Modify: `TrafficLightsEnhancement/Mod.cs`
- Delete: `CommonLibraries/LaneSystem/Mod.cs`

- [ ] Add an ECS test that inspects the TLE Extended and Lane System assemblies and asserts exactly one concrete exported type implements `Game.Modding.IMod`, namely `C2VM.TrafficLightsEnhancement.Mod`.
- [ ] Run `dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj -c Release -p:LangVersion=latest --filter FullyQualifiedName~ModEntryPointTests` and confirm the new test fails because both assemblies currently expose a mod entry point.
- [ ] Create a dedicated branch in the clean, refreshed `CommonLibraries` submodule.
- [ ] Delete `CommonLibraries/LaneSystem/Mod.cs` without changing Lane System assembly metadata or other source types.
- [ ] Add `m_World.GetOrCreateSystemManaged<Game.Net.C2VMPatchedLaneSystem>().Enabled = false;` to the main TLE Extended `Mod.OnLoad` after assigning `m_World`.
- [ ] Re-run the focused test and confirm it passes.
- [ ] Commit the submodule change, then commit the parent submodule pointer, main startup change, and test.

## Task 2: Remove the separate Lane System version from settings

**Files:**

- Modify: `TrafficLightsEnhancement/UI/tests/transit-signal-priority-panel.test.mjs`
- Modify: `TrafficLightsEnhancement/Settings.cs`
- Modify: `TrafficLightsEnhancement/Locale.json`
- Modify: `TrafficLightsEnhancement/Locale/*.json`
- Modify: `docs/mod-option-descriptions-audit.md`

- [ ] Add a UI resource test that asserts `m_LaneSystemVersion` is absent from `Settings.cs`, the base locale, and every translated locale.
- [ ] Run `npm test` from `TrafficLightsEnhancement/UI` and confirm the new assertion fails against the existing setting and locale keys.
- [ ] Remove the `m_LaneSystemVersion` setting property and its label/description from every locale file.
- [ ] Remove the obsolete Lane System version entry from the settings documentation audit.
- [ ] Re-run the UI tests and confirm they pass.
- [ ] Commit the settings, localization, documentation, and test changes.

## Task 3: Package one mod folder

**Files:**

- Modify: `TrafficLightsEnhancement.Tests/ReleaseVersionTests.cs`
- Modify: `.github/workflows/release.yml`
- Modify: `BUILD.md`

- [ ] Add a release test that asserts the workflow copies `C2VM.CommonLibraries.LaneSystem.dll` into `C2VM.TrafficLightsEnhancement`, does not create a top-level `C2VM.CommonLibraries` package folder, and archives only the TLE Extended folder.
- [ ] Run `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release -p:LangVersion=latest --filter FullyQualifiedName~ReleaseVersionTests` and confirm the new packaging assertion fails.
- [ ] Update the release workflow to put both DLLs in `C2VM.TrafficLightsEnhancement` and zip that single folder.
- [ ] Update `BUILD.md` to describe the single-folder package layout.
- [ ] Re-run the focused release tests and confirm they pass.
- [ ] Commit the workflow, build documentation, and test changes.

## Task 4: Verify and install

- [ ] Run the full pure-logic, ECS, serialization, and UI test suites.
- [ ] Run a non-deploying Release build with `dotnet build TrafficLightsEnhancement/TrafficLightsEnhancement.csproj -c Release -p:DisablePostProcessors=true -p:LangVersion=latest`.
- [ ] Confirm the build output contains both expected DLLs and that the entry-point test still reports only TLE Extended as an `IMod`.
- [ ] Confirm Cities: Skylines II is not running before deploying.
- [ ] Run the normal Release build/install and compare hashes between built and installed DLLs.
- [ ] Confirm the installed mod folder contains the TLE Extended and Lane System DLLs together.
- [ ] Refresh Skyve and confirm it reports the TLE Extended version rather than `0.0.17.0`.
- [ ] Launch the game and confirm TLE Extended loads without a separate Lane System mod entry and existing configured intersections remain available.
