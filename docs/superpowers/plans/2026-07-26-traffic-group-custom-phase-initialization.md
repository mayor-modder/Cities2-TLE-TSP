# Traffic group custom phase initialization implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make grouped intersections inherit only phase count and timing while keeping every movement mask local to its own junction.

**Architecture:** `TrafficGroupSystem` will own one idempotent member initializer used by member creation, custom-phase editor navigation, pattern propagation, timing matching, and load migration. The initializer will add only missing phase records and connected-edge masks; it will never clear or translate existing movement masks. The C# main-panel payload will expose whether a follower has a complete runtime movement map so React can distinguish “synced” from “needs phase setup.”

**Tech stack:** C# 12, Unity Entities, Cities: Skylines II game APIs, React 18, TypeScript 4.8, Node test runner, xUnit.

## Global constraints

- Preserve `C2VM.TrafficLightsEnhancement` assembly and root namespace identifiers.
- Do not change serialized component layouts.
- Do not change lockstep or green-wave runtime synchronization.
- Do not copy `EdgeGroupMask` or `SubLaneGroupMask` values between junctions.
- Preserve existing member phases and movement masks; add missing phase records but never shrink automatically.
- User-facing text uses sentence case.
- Do not run a Release build or install files while Cities: Skylines II is running.
- Preserve the pre-existing `CommonLibraries` working-tree modification.

---

## File structure

- `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`
  owns topology-local phase initialization, timing-only matching, and pattern propagation.
- `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
  routes editor navigation through the initializer, exposes setup status, and removes copy triggers.
- `TrafficLightsEnhancement/Systems/Serialization/TLEDataMigrationSystem.cs`
  repairs missing phases without geometry copying and explains the required local setup.
- `TrafficLightsEnhancement/UI/src/bindings.ts`
  exposes the timing-only action and removes copy bindings.
- `TrafficLightsEnhancement/UI/src/mods/general.ts`
  types the `phaseSetupComplete` member payload.
- `TrafficLightsEnhancement/UI/src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx`
  removes copy-to-all, adds timing matching, and renders setup status.
- `TrafficLightsEnhancement/Locale.json` and `TrafficLightsEnhancement/Locale/*.json`
  provide production labels and the revised migration message.
- `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupCustomPhaseInitializationSourceTests.cs`
  guards C#/ECS integration paths that cannot be exercised without a game world.
- `TrafficLightsEnhancement/UI/tests/traffic-group-custom-phase-initialization.test.mjs`
  guards the React, TypeScript binding, and localization contract.
- `docs/traffic-groups.md` and `docs/save-format-contract.md`
  document ownership and verification boundaries.

---

### Task 1: Add failing regression tests for topology-local ownership

**Files:**
- Delete: `TrafficLightsEnhancement.Ecs.Tests/CopyPhasesToAllMembersSourceTests.cs`
- Create: `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupCustomPhaseInitializationSourceTests.cs`
- Create: `TrafficLightsEnhancement/UI/tests/traffic-group-custom-phase-initialization.test.mjs`

**Interfaces:**
- Consumes: existing source-extraction helpers and Node file-reading patterns.
- Produces: regression contracts for `EnsureMemberCustomPhaseSetup(Entity, Entity)`, `phaseSetupComplete`, and `CallMatchPhaseDurationsToLeader`.

- [ ] **Step 1: Replace copy-oriented C# source tests with initialization contracts**

Create tests that extract the relevant methods and assert these exact properties:

```csharp
[Fact]
public void Member_initialization_adds_timing_structure_without_clearing_local_masks()
{
    string source = File.ReadAllText(GetTrafficGroupSystemPath());
    string initializer = ExtractMethod(
        source,
        "public void EnsureMemberCustomPhaseSetup");

    Assert.Contains("new CustomPhaseData()", initializer);
    Assert.Contains("m_MinimumDuration = leaderPhase.m_MinimumDuration", initializer);
    Assert.Contains("m_MaximumDuration = leaderPhase.m_MaximumDuration", initializer);
    Assert.Contains("EnsureTopologyLocalEdgeMasks(memberEntity)", initializer);
    Assert.DoesNotContain(".Clear()", initializer);
    Assert.DoesNotContain("CopyEdgeGroupMask", initializer);
    Assert.DoesNotContain("CopySubLaneGroupMask", initializer);
}

[Fact]
public void Every_group_member_entry_path_uses_the_shared_initializer()
{
    string trafficGroupSource = File.ReadAllText(GetTrafficGroupSystemPath());
    string uiSource = File.ReadAllText(GetUiBindingsPath());

    Assert.Contains(
        "EnsureMemberCustomPhaseSetup(groupEntity, junctionEntity)",
        ExtractMethod(trafficGroupSource, "public bool AddJunctionToGroup"));
    Assert.Contains(
        "EnsureMemberCustomPhaseSetup(member.m_GroupEntity, junctionEntity)",
        ExtractMethod(uiSource, "protected void CallUpdateMemberPattern"));
}

[Fact]
public void Removed_copy_operations_cannot_be_reintroduced_through_bindings()
{
    string trafficGroupSource = File.ReadAllText(GetTrafficGroupSystemPath());
    string uiSource = File.ReadAllText(GetUiBindingsPath());

    Assert.DoesNotContain("TryCopyPhasesToJunction", trafficGroupSource);
    Assert.DoesNotContain("CopyPhasesToAllMembers", trafficGroupSource);
    Assert.DoesNotContain("CallCopyPhasesToJunction", uiSource);
    Assert.DoesNotContain("CallCopyPhasesToAllMembers", uiSource);
}
```

Add companion tests for:

```csharp
Assert.DoesNotContain(
    "CopyEdgeGroupMask",
    ExtractMethod(trafficGroupSource, "public void PropagatePatternToMembers"));
Assert.Contains(
    "EnsureMemberCustomPhaseSetup(groupEntity, memberEntity)",
    ExtractMethod(trafficGroupSource, "public void PropagatePatternToMembers"));
Assert.Contains(
    "EnsureMemberCustomPhaseSetup(groupEntity, memberEntity)",
    File.ReadAllText(GetMigrationSystemPath()));
Assert.Contains("phaseSetupComplete =", File.ReadAllText(GetUiBindingsPath()));
```

- [ ] **Step 2: Add Node source tests for the production UI contract**

Create `traffic-group-custom-phase-initialization.test.mjs` with file helpers and these assertions:

```javascript
test("traffic groups offer timing matching instead of movement copying", async () => {
  const component = await source(
    "src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx");
  const bindings = await source("src/bindings.ts");

  assert.doesNotMatch(component, /CopyPhasesToAllMembers/);
  assert.doesNotMatch(bindings, /CallCopyPhasesTo(AllMembers|Junction)/);
  assert.match(component, /callMatchPhaseDurationsToLeader/);
  assert.match(bindings, /CallMatchPhaseDurationsToLeader/);
});

test("incomplete lockstep followers are marked for phase setup", async () => {
  const component = await source(
    "src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx");
  const types = await source("src/mods/general.ts");
  const locale = JSON.parse(await repoSource("Locale.json"));

  assert.match(types, /phaseSetupComplete: boolean/);
  assert.match(component, /member\.phaseSetupComplete/);
  assert.equal(
    locale["UI.LABEL[C2VM.TrafficLightsEnhancement.NeedsPhaseSetup]"],
    "needs phase setup");
});
```

- [ ] **Step 3: Run focused tests and confirm the new contracts fail**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest --filter TrafficGroupCustomPhaseInitializationSourceTests
npm test --prefix TrafficLightsEnhancement/UI -- --test-name-pattern "traffic groups|incomplete lockstep"
```

Expected: both commands fail because the initializer, payload field, timing action binding, and new labels do not yet exist.

- [ ] **Step 4: Commit the failing tests**

```powershell
git add -- TrafficLightsEnhancement.Ecs.Tests/CopyPhasesToAllMembersSourceTests.cs TrafficLightsEnhancement.Ecs.Tests/TrafficGroupCustomPhaseInitializationSourceTests.cs TrafficLightsEnhancement/UI/tests/traffic-group-custom-phase-initialization.test.mjs
git commit -m "test: define local group phase ownership"
```

---

### Task 2: Implement the idempotent topology-local initializer

**Files:**
- Modify: `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`
- Modify: `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
- Test: `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupCustomPhaseInitializationSourceTests.cs`

**Interfaces:**
- Consumes: `TrafficGroupMember`, `CustomTrafficLights`, `CustomPhaseData`, `ConnectedEdge`, `EdgeGroupMask`, and `NodeUtils.GetEdgePosition(...)`.
- Produces: `public void EnsureMemberCustomPhaseSetup(Entity groupEntity, Entity memberEntity)`.

- [ ] **Step 1: Add the shared initializer**

Add a public method with this control flow:

```csharp
public void EnsureMemberCustomPhaseSetup(Entity groupEntity, Entity memberEntity)
{
    if (memberEntity == Entity.Null || !EntityManager.Exists(memberEntity))
    {
        return;
    }

    EnsureCustomPhaseComponents(memberEntity);
    Entity leaderEntity = GetGroupLeader(groupEntity);
    DynamicBuffer<CustomPhaseData> memberPhases =
        EntityManager.GetBuffer<CustomPhaseData>(memberEntity);

    if (leaderEntity != Entity.Null
        && EntityManager.HasComponent<CustomTrafficLights>(leaderEntity)
        && EntityManager.GetComponentData<CustomTrafficLights>(leaderEntity)
            .GetPatternOnly() == CustomTrafficLights.Patterns.CustomPhase
        && EntityManager.TryGetBuffer(
            leaderEntity,
            true,
            out DynamicBuffer<CustomPhaseData> leaderPhases)
        && leaderPhases.Length > 0)
    {
        EnsureMemberUsesCustomPhases(memberEntity);
        for (int i = memberPhases.Length; i < leaderPhases.Length; i++)
        {
            CustomPhaseData leaderPhase = leaderPhases[i];
            var memberPhase = new CustomPhaseData
            {
                m_MinimumDuration = leaderPhase.m_MinimumDuration,
                m_MaximumDuration = leaderPhase.m_MaximumDuration
            };
            memberPhases.Add(memberPhase);
        }
    }
    else if (memberPhases.Length == 0)
    {
        memberPhases.Add(new CustomPhaseData());
    }

    EnsureTopologyLocalEdgeMasks(memberEntity);
    MarkUpdated(memberEntity);
}
```

Implement the private helpers so they:

- add missing `CustomTrafficLights`, `CustomPhaseData`, `EdgeGroupMask`, and `SubLaneGroupMask`;
- set custom pattern plus dynamic mode only when a custom-phase leader requires it;
- iterate the target’s `ConnectedEdge` buffer and add only absent `EdgeGroupMask` records using `NodeUtils.GetEdgePosition`;
- leave existing edge and sub-lane buffers unchanged;
- add `Updated` only when it is absent.

- [ ] **Step 2: Route every creation and editing path through the initializer**

In `AddJunctionToGroup(...)`, replace the duplicated buffer creation with:

```csharp
EnsureMemberCustomPhaseSetup(groupEntity, junctionEntity);
```

In `CallUpdateMemberPattern(...)`, after the grouped member has been forced to custom phases and written back, invoke:

```csharp
var member = EntityManager.GetComponentData<TrafficGroupMember>(junctionEntity);
trafficGroupSystem.EnsureMemberCustomPhaseSetup(
    member.m_GroupEntity,
    junctionEntity);
```

Keep `UpdateEdgeInfo(junctionEntity)` after initialization so the first editor render receives concrete target-local edge masks.

- [ ] **Step 3: Make timing matching and pattern propagation preserve local masks**

Before matching a follower’s durations, call:

```csharp
EnsureMemberCustomPhaseSetup(groupEntity, memberEntity);
```

Update `PropagatePatternToMembers(...)` to set the pattern, then call the initializer only for `CustomPhase`. Remove both geometry-copy calls and change the log message to report pattern propagation without “full lane matching.”

- [ ] **Step 4: Remove the full-copy API and copy-only geometry helpers**

Delete:

- `ValidatePhaseSyncCompatibility(...)`;
- `GetPatternDisplayName(...)`;
- `TryCopyPhasesToJunction(...)`;
- `CopyPhasesToJunction(...)`;
- `CopyPhasesToAllMembers(...)`;
- `CopyEdgeGroupMaskWithDirectionMatching(...)`;
- `TryFindConnectedSourceEdge(...)`;
- `GetEdgeDirection(...)`;
- `AngleDifference(...)`;
- `CopySubLaneGroupMaskWithLaneTypeMatching(...)`;
- its copy-only lane matching structs and helpers;
- copy-only UI trigger registration and handlers.

Retain `GetEdgePositionForJunction(...)` because green-wave distance logic also uses it.

- [ ] **Step 5: Run the focused C# tests**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest --filter TrafficGroupCustomPhaseInitializationSourceTests
```

Expected: PASS.

- [ ] **Step 6: Commit the runtime initializer**

```powershell
git add -- TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs TrafficLightsEnhancement.Ecs.Tests/TrafficGroupCustomPhaseInitializationSourceTests.cs
git commit -m "fix: initialize group phases from local topology"
```

---

### Task 3: Replace copy migration with safe automatic initialization

**Files:**
- Modify: `TrafficLightsEnhancement/Systems/Serialization/TLEDataMigrationSystem.cs`
- Modify: `TrafficLightsEnhancement/Locale.json`
- Modify: `TrafficLightsEnhancement/Locale/*.json`
- Test: `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupCustomPhaseInitializationSourceTests.cs`

**Interfaces:**
- Consumes: `TrafficGroupSystem.EnsureMemberCustomPhaseSetup(Entity, Entity)`.
- Produces: an automatic migration that preserves local masks and an informational one-button dialog.

- [ ] **Step 1: Update the migration regression test**

Assert the missing-phase scan initializes each affected follower and never offers a copy/reset branch:

```csharp
string migration = File.ReadAllText(GetMigrationSystemPath());
Assert.Contains(
    "EnsureMemberCustomPhaseSetup(groupEntity, memberEntity)",
    migration);
Assert.DoesNotContain("TryCopyPhasesToJunction", migration);
Assert.DoesNotContain("copyFromLeader", migration);
Assert.DoesNotContain("edgeMasks.Clear()", migration);
Assert.DoesNotContain("subLaneMasks.Clear()", migration);
```

- [ ] **Step 2: Convert migration to automatic safe repair**

When a custom-phase follower has no phase records, call:

```csharp
trafficGroupSystem.EnsureMemberCustomPhaseSetup(groupEntity, memberEntity);
```

Count repaired followers, remove `_affectedGroupsForMigration` and `OnMissingPhasesDialogResult(...)`, and show a single `Common.OK` dialog after initialization. The message must say that timings were restored and that each affected member’s permitted movements must be reviewed in its custom phase editor.

- [ ] **Step 3: Update all shipped locale dictionaries**

Set the English base message to:

```text
Detected {0} group member(s) in {1} group(s) with missing custom phases. TLE Extended restored their phase count and timings without copying movement permissions. Review each affected member in the custom phase editor and select the movements served by every phase.
```

Update `MissingPhasesMessage` in every embedded sibling locale with a non-English translation preserving `{0}` and `{1}` exactly.

- [ ] **Step 4: Run ECS and localization coverage tests**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest --filter TrafficGroupCustomPhaseInitializationSourceTests
npm test --prefix TrafficLightsEnhancement/UI -- --test-name-pattern "Crowdin locale dictionaries|machine-assisted localization"
```

Expected: PASS, with every locale covering all base keys and retaining valid metadata references.

- [ ] **Step 5: Commit the migration**

```powershell
git add -- TrafficLightsEnhancement/Systems/Serialization/TLEDataMigrationSystem.cs TrafficLightsEnhancement/Locale.json TrafficLightsEnhancement/Locale
git commit -m "fix: migrate group phases without copying movements"
```

---

### Task 4: Expose setup status and timing-only group controls

**Files:**
- Modify: `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
- Modify: `TrafficLightsEnhancement/UI/src/bindings.ts`
- Modify: `TrafficLightsEnhancement/UI/src/mods/general.ts`
- Modify: `TrafficLightsEnhancement/UI/src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx`
- Modify: `TrafficLightsEnhancement/Locale.json`
- Modify: `TrafficLightsEnhancement/Locale/*.json`
- Test: `TrafficLightsEnhancement/UI/tests/traffic-group-custom-phase-initialization.test.mjs`

**Interfaces:**
- Consumes: `TrafficGroupPhaseMapping.m_Map.IsComplete` and existing backend trigger `CallMatchPhaseDurationsToLeader`.
- Produces: `GroupMemberInfo.phaseSetupComplete: boolean` and `callMatchPhaseDurationsToLeader`.

- [ ] **Step 1: Add movement-map completeness to the main-panel payload**

Before constructing `memberInfo`, compute:

```csharp
bool phaseSetupComplete = memberData.m_IsGroupLeader
    || (EntityManager.TryGetComponent(
            memberEntity,
            out TrafficGroupPhaseMapping phaseMapping)
        && phaseMapping.m_Map.IsComplete);
```

Include `phaseSetupComplete` in the anonymous payload and add the required Boolean property to `GroupMemberInfo`.

- [ ] **Step 2: Replace React copy controls with timing-only matching**

Export:

```typescript
export const callMatchPhaseDurationsToLeader =
  triggers.create<[string]>("CallMatchPhaseDurationsToLeader");
```

Replace `CopyPhasesToMemberButton` with a group-level button that sends:

```typescript
callMatchPhaseDurationsToLeader(JSON.stringify({
  groupIndex: displayedGroup.groupIndex,
  groupVersion: displayedGroup.groupVersion
}));
```

Render it when a group has at least two members. Do not pass a source junction because the leader is resolved by `TrafficGroupSystem`.

- [ ] **Step 3: Render accurate follower status**

Use:

```tsx
const needsPhaseSetup =
  isFollowerInLockstep && !member.phaseSetupComplete;
```

Show `NeedsPhaseSetup` in the foldout header when true, otherwise show `Synced`. When setup is incomplete, replace the “Controlled by leader” description with a message instructing the player to open the member’s custom phase editor.

- [ ] **Step 4: Add localized labels**

Add these base entries and non-English sibling-locale entries:

```json
"UI.LABEL[C2VM.TrafficLightsEnhancement.MatchPhaseTimingsToLeader]": "Match phase timings to leader",
"UI.LABEL[C2VM.TrafficLightsEnhancement.NeedsPhaseSetup]": "needs phase setup",
"UI.LABEL[C2VM.TrafficLightsEnhancement.ConfigureMemberPhases]": "Configure this member's permitted movements in the custom phase editor before lockstep synchronization can control it."
```

- [ ] **Step 5: Run the focused UI tests and production bundle**

Run:

```powershell
npm test --prefix TrafficLightsEnhancement/UI -- --test-name-pattern "traffic groups|incomplete lockstep"
npm run build --prefix TrafficLightsEnhancement/UI
```

Expected: PASS and a successful webpack production bundle.

- [ ] **Step 6: Commit the UI and binding contract**

```powershell
git add -- TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs TrafficLightsEnhancement/UI/src/bindings.ts TrafficLightsEnhancement/UI/src/mods/general.ts TrafficLightsEnhancement/UI/src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx TrafficLightsEnhancement/UI/tests/traffic-group-custom-phase-initialization.test.mjs TrafficLightsEnhancement/Locale.json TrafficLightsEnhancement/Locale
git commit -m "feat: show group phase setup status"
```

---

### Task 5: Update maintainer docs and run complete verification

**Files:**
- Modify: `docs/traffic-groups.md`
- Modify: `docs/save-format-contract.md`

**Interfaces:**
- Consumes: the completed runtime and UI behavior.
- Produces: documented timing-versus-movement ownership and a verified playtest build.

- [ ] **Step 1: Update traffic-group architecture documentation**

Document these invariants:

```text
The group leader owns phase count and timing coordination.
Each junction owns its own vehicle, track, bicycle, and pedestrian movement masks.
Adding or opening a member initializes missing timing structure and stopped local masks.
No production action translates or copies movement masks between junctions.
Incomplete movement maps remain fail-closed and appear as “needs phase setup.”
```

Remove the obsolete copy-compatibility section and its topology-copy warning.

- [ ] **Step 2: Update the save-format verification table**

Replace the obsolete `CopyPhasesToAllMembersSourceTests` reference with `TrafficGroupCustomPhaseInitializationSourceTests` and describe the missing-phase migration as timing-only, topology-local initialization.

- [ ] **Step 3: Run all affected test suites**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj
dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest
dotnet test TrafficLightsEnhancement.Serialization.Tests/TrafficLightsEnhancement.Serialization.Tests.csproj -p:LangVersion=latest
npm test --prefix TrafficLightsEnhancement/UI
npm run build --prefix TrafficLightsEnhancement/UI
git diff --check
```

Expected: every command exits successfully and `git diff --check` prints no errors.

- [ ] **Step 4: Confirm the game is closed before the Release build**

Run:

```powershell
Get-Process Cities2 -ErrorAction SilentlyContinue
```

Expected: no process output. If the process is present, stop before building and ask the user to close the game.

- [ ] **Step 5: Build and verify the installed artifact**

Run:

```powershell
dotnet build Cities2-TrafficLightsEnhancement.sln --configuration Release -p:LangVersion=latest
```

Then compare the Release DLL hash with the locally installed TLE Extended DLL using the deployment path reported by MSBuild. Expected: build succeeds and both SHA-256 hashes match.

- [ ] **Step 6: Commit documentation and verification state**

```powershell
git add -- docs/traffic-groups.md docs/save-format-contract.md
git commit -m "docs: explain local group movement ownership"
```

- [ ] **Step 7: Hand off one targeted gameplay verification**

Ask the user to perform one launch covering:

1. add a fresh follower;
2. open its custom phase editor immediately and confirm all local directions appear;
3. set movements independently for each member;
4. reopen each editor and confirm no movement permissions changed;
5. confirm followers show “synced” only after the runtime movement map is complete;
6. confirm one direction never displays conflicting permanent green and red indications.
