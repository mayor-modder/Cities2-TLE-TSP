# In-game branding implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Present the installed mod as `Traffic Lights Enhancement` everywhere players see its name while retaining the TLE Extended repository identity and all compatibility-sensitive identifiers.

**Architecture:** Treat existing packaged resources as the branding boundary. Change their player-facing values in place, keep localization keys and internal identifiers stable, and protect that separation with existing compatibility/UI contract tests plus a packaged-source scan.

**Tech stack:** C#/.NET 8 xUnit tests, net48 mod assembly, React/Node UI contract tests, JSON localization dictionaries, XML publish metadata, PowerShell, Cities: Skylines II modding toolchain.

## Global constraints

- Every packaged player-facing occurrence uses `Traffic Lights Enhancement`.
- Repository documentation may continue to use `TLE Extended`.
- Keep `C2VM.TrafficLightsEnhancement`, `TrafficLightsEnhancementExtended`, localization keys, bindings, namespaces, assembly names, and save identifiers unchanged.
- Keep the beta-only `TLE Beta` title unchanged.
- Preserve the unrelated dirty `CommonLibraries` submodule.
- Execute inline without subagents.

---

### Task 1: Define the shorter-brand regression contract

**Files:**
- Modify: `TrafficLightsEnhancement.Tests/Compatibility/ReleaseVersionTests.cs`
- Modify: `TrafficLightsEnhancement/UI/tests/transit-signal-priority-panel.test.mjs`

**Interfaces:**
- Consumes: `UI/mod.json`, `Properties/PublishConfiguration.xml`, packaged locale/C#/UI sources.
- Produces: automated expectations that distinguish player-facing branding from compatibility identifiers.

- [ ] **Step 1: Change the compatibility test expectation**

Set `ExpectedDisplayName` to `Traffic Lights Enhancement` and rename
`Tle_uses_the_extended_display_name_without_changing_compatibility_identifiers`
to
`Tle_uses_the_player_facing_display_name_without_changing_compatibility_identifiers`.

- [ ] **Step 2: Change the UI manifest expectation**

Update the `display name is separate from compatibility identifiers` test to
expect `Traffic Lights Enhancement`.

- [ ] **Step 3: Add a packaged-source branding guard**

In the Node test, enumerate these player-facing sources:

```text
Locale.json
Locale/*.json
Properties/PublishConfiguration.xml
Systems/Serialization/TLEDataMigrationSystem.cs
Systems/UI/UISystem.UIBIndings.cs
UI/mod.json
```

Assert that none contains either retired player-facing brand:

```javascript
assert.doesNotMatch(source, /Traffic Lights Enhancement Extended|TLE Extended/);
```

Do not scan repository documentation, project paths, deploy-folder values, or
test descriptions.

- [ ] **Step 4: Run the focused tests and verify RED**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj --no-restore --filter FullyQualifiedName~ReleaseVersionTests -p:LangVersion=latest -p:UseSharedCompilation=false -m:1
npm test --prefix TrafficLightsEnhancement/UI
```

Expected: both suites fail because production metadata/resources still contain
the extended player-facing brand.

### Task 2: Rename packaged player-facing sources

**Files:**
- Modify: `TrafficLightsEnhancement/UI/mod.json`
- Modify: `TrafficLightsEnhancement/Properties/PublishConfiguration.xml`
- Modify: `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
- Modify: `TrafficLightsEnhancement/Systems/Serialization/TLEDataMigrationSystem.cs`
- Modify: `TrafficLightsEnhancement/Locale.json`
- Modify: `TrafficLightsEnhancement/Locale/*.json`

**Interfaces:**
- Consumes: the branding contract from Task 1.
- Produces: shorter options title, input-map title, main-panel header, dialogs, descriptions, and publish display name.

- [ ] **Step 1: Update manifest, publish metadata, and C# fallbacks**

Replace `Traffic Lights Enhancement Extended` with
`Traffic Lights Enhancement` in the UI manifest, publish metadata, main-panel
title, and migration/phase-dialog fallbacks. Leave `TLE Beta` unchanged.

- [ ] **Step 2: Update embedded localization resources**

In `Locale.json` and every `Locale/*.json`, replace:

```text
Traffic Lights Enhancement Extended
```

with:

```text
Traffic Lights Enhancement
```

Replace remaining player-facing `TLE Extended` self-references with
`Traffic Lights Enhancement`, retaining surrounding translations and JSON
structure.

- [ ] **Step 3: Verify compatibility-sensitive values did not change**

Run:

```powershell
rg -n 'C2VM\.TrafficLightsEnhancement|TrafficLightsEnhancementExtended' TrafficLightsEnhancement/TrafficLightsEnhancement.csproj TrafficLightsEnhancement/UI/mod.json TrafficLightsEnhancement/Properties/PublishConfiguration.xml
```

Expected: assembly/root/UI ids and deploy-folder values remain present.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the same focused .NET and UI test commands from Task 1.

Expected: all tests pass and the packaged-source guard finds no retired
player-facing brand.

- [ ] **Step 5: Commit the implementation**

Stage only the test and player-facing source files, then commit:

```powershell
git commit -m "refactor: shorten in-game mod branding"
```

### Task 3: Verify, install, and publish the follow-up

**Files:**
- Modify: `TrafficLightsEnhancement/TrafficLightsEnhancement.csproj`
- Modify: `TrafficLightsEnhancement/UI/mod.json`
- Modify: `TrafficLightsEnhancement.Tests/Compatibility/ReleaseVersionTests.cs`

**Interfaces:**
- Consumes: the completed player-facing branding implementation.
- Produces: a verified local installation and matching remote branch.

- [ ] **Step 1: Increment the locally installed build version**

Change the authoritative version fields from `1.0.7` to `1.0.8`:

```text
TrafficLightsEnhancement.csproj Version: 1.0.8.0
TrafficLightsEnhancement.csproj InformationalVersion: 1.0.8.0
UI/mod.json version: 1.0.8
ReleaseVersionTests.cs ExpectedSemanticVersion: 1.0.8
```

Do not change the unrelated `path-parse` dependency version in
`UI/package-lock.json`.

- [ ] **Step 2: Run the complete automated test suite**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj --no-restore -p:LangVersion=latest -p:UseSharedCompilation=false -m:1
dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj --no-restore -p:LangVersion=latest -p:UseSharedCompilation=false -m:1
dotnet test TrafficLightsEnhancement.Serialization.Tests/TrafficLightsEnhancement.Serialization.Tests.csproj --no-restore -p:LangVersion=latest -p:UseSharedCompilation=false -m:1
npm test --prefix TrafficLightsEnhancement/UI
```

Expected: 0 failures.

- [ ] **Step 3: Build the production UI**

Run:

```powershell
npm run build --prefix TrafficLightsEnhancement/UI
```

Expected: webpack production build succeeds.

- [ ] **Step 4: Confirm the game is closed**

Run:

```powershell
Get-Process Cities2 -ErrorAction SilentlyContinue
```

If Cities: Skylines II is running, stop before Release build/install rather
than overwriting files used by the game.

- [ ] **Step 5: Build and install Release**

Run:

```powershell
dotnet build Cities2-TrafficLightsEnhancement.sln --configuration Release -p:LangVersion=latest -p:UseSharedCompilation=false -m:1
```

Expected: successful post-processing and deployment with zero build errors.

- [ ] **Step 6: Verify installed branding**

Inspect the deployed `C2VM.TrafficLightsEnhancement.mjs`, embedded localization
resources, and decompiled installed assembly. Confirm they contain
`Traffic Lights Enhancement` and do not expose either retired extended brand
to players.

- [ ] **Step 7: Commit the versioned branding build**

Stage the three authoritative version files together with any remaining
branding changes and commit:

```powershell
git commit -m "chore: version in-game branding build 1.0.8"
```

- [ ] **Step 8: Push and verify**

Push `codex/takeover-group-lockstep`, then verify local HEAD, upstream HEAD, and
the remote branch SHA are identical. Do not include the dirty
`CommonLibraries` submodule.
