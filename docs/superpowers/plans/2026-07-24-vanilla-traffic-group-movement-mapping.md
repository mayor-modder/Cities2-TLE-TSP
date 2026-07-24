# Vanilla traffic-group movement mapping implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task by task. Add a failing test before each production behavior change.

**Goal:** Make coordinated traffic groups synchronize equivalent physical movements, preserve every member's locally winning demand, and eliminate the coordination job-memory lifetime errors.

**Architecture:** Build a transient movement map from each member's live lane-signal groups and incoming road/track axes. Store the packed map on the member without changing save data. During the existing three-pass traffic-light update, translate member demand into leader phase space, update the active leader once, then translate the same-tick leader state back into each follower's local phase space. A runtime group component records the leader's `UpdateFrame` shard so the separate active-group discovery job and map can be removed without processing inactive groups.

**Tech stack:** C# 10/12, .NET Standard 2.0 pure logic, Unity Entities/net48 runtime, Burst-compatible unmanaged values, xUnit, Cities: Skylines II mod toolchain.

## Global constraints

- Work only in `.worktrees/codex-fix-vanilla-group-lockstep` on `codex/fix-vanilla-group-lockstep`.
- Preserve the unrelated untracked `docs/superpowers/plans/2026-07-22-vanilla-traffic-group-lockstep.md`.
- Preserve the `C2VM.TrafficLightsEnhancement` assembly, root namespace, mod id, and serialized `TrafficGroup`/`TrafficGroupMember` layouts.
- Do not add a save migration. New mapping and scheduling components are runtime-only and reconstructed after loading.
- Keep local TSP suspended for every grouped intersection, including the leader.
- Do not overwrite follower phase definitions or silently fall back to raw phase-number copying.
- Use `apply_patch` for source edits.
- Do not run a deploying Release build while Cities: Skylines II is open. Use `DisablePostProcessors=true` for the non-deploying build.

## File and responsibility map

- Create `TrafficLightsEnhancement.Logic/TrafficGroups/TrafficGroupPhaseMap.cs`: movement signatures, deterministic complete-map construction, packed leader-to-member lookup, inverse lookup, and axis quantization.
- Modify `TrafficLightsEnhancement.Logic/TrafficGroups/VanillaTrafficGroupDemandPolicy.cs`: fair cross-member merge and map-based member-to-leader demand remapping.
- Create `TrafficLightsEnhancement.Tests/TrafficGroups/TrafficGroupPhaseMapTests.cs`: pure mapping, ambiguity, inverse lookup, and axis-equivalence coverage.
- Modify `TrafficLightsEnhancement.Tests/TrafficGroups/VanillaTrafficGroupDemandPolicyTests.cs`: fair demand union and inverse-remap coverage.
- Create `TrafficLightsEnhancement/Components/TrafficGroupRuntimeData.cs`: non-serialized leader update-shard metadata.
- Create `TrafficLightsEnhancement/Components/TrafficGroupPhaseMapping.cs`: non-serialized per-member wrapper around the pure packed phase map.
- Modify `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`: derive live road/track movement signatures, refresh runtime maps, and refresh leader shard metadata.
- Modify `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs`: expose read-only runtime group and phase-map lookups to the simulation job.
- Modify `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs`: remove active-group discovery, use the leader shard, remap demand, schedule three passes, and use safe-lived coordination containers.
- Modify `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`: translate current/next phases through the movement map and fail closed when it is unavailable.
- Modify `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`: report the actual leader-to-member mapping or `Movement mapping unavailable` in the existing group phase-mapping row.
- Modify `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs`: source contracts for mapping construction/consumption, shard routing, discovery removal, safe allocators, and fail-closed behavior.
- Modify `docs/traffic-groups.md`: explain physical movement mapping, fair group demand, diagnostics, and independent fallback.

---

### Task 1: Define the packed movement-map policy test first

**Files:**

- Create: `TrafficLightsEnhancement.Logic/TrafficGroups/TrafficGroupPhaseMap.cs`
- Create: `TrafficLightsEnhancement.Tests/TrafficGroups/TrafficGroupPhaseMapTests.cs`

**Interfaces:**

```csharp
public readonly struct TrafficGroupPhaseSignature
{
    public TrafficGroupPhaseSignature(
        int signalGroup,
        ulong roadApproachAxisMask,
        ulong trackApproachAxisMask);

    public int SignalGroup { get; }
    public ulong RoadApproachAxisMask { get; }
    public ulong TrackApproachAxisMask { get; }
}

public readonly struct TrafficGroupPhaseMap
{
    public int LeaderPhaseCount { get; }
    public int MemberPhaseCount { get; }
    public bool IsComplete { get; }

    public bool TryMapLeaderToMember(int leaderPhase, out int memberPhase);
    public bool TryMapMemberToLeader(int memberPhase, out int leaderPhase);
}

public static class TrafficGroupMovementMappingPolicy
{
    public const int AxisBinCount = 16;
    public const int MaximumMappedPhaseCount = 16;

    public static int QuantizeUndirectedAxis(double x, double z);

    public static bool TryBuild(
        IReadOnlyList<TrafficGroupPhaseSignature> leader,
        IReadOnlyList<TrafficGroupPhaseSignature> member,
        out TrafficGroupPhaseMap map);
}
```

- [ ] Add failing tests for:
  - aligned two-phase signatures mapping `G1 -> G1`, `G2 -> G2`;
  - swapped member signatures mapping leader `G1 -> member G2` and leader `G2 -> member G1`;
  - inverse lookup returning the leader phase for a member phase;
  - opposite vectors quantizing to the same undirected road-axis bin;
  - separate road and track masks contributing to a match;
  - tied candidates, empty signatures, duplicate member use, or an incomplete leader set rejecting the entire map.
- [ ] Run:

  ```powershell
  dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj --filter FullyQualifiedName~TrafficGroupPhaseMapTests
  ```

  Expected: FAIL because the mapping types do not exist.
- [ ] Implement `TrafficGroupPhaseMap` as an unmanaged packed value. Encode at most 16 one-based mappings in two `ulong` fields using five bits per leader phase; zero means unmapped. Keep phase counts in bytes and reject invalid lookup arguments.
- [ ] Implement undirected axis quantization by normalizing `atan2(z, x)` modulo π into 16 bins. This makes opposite directions on the same physical road axis equivalent.
- [ ] Implement deterministic fail-closed matching:
  - prefer a unique exact road/track signature match;
  - otherwise choose a unique highest weighted overlap, with road and track masks evaluated separately and symmetric-difference penalties preventing a broad phase from winning only because it contains everything;
  - process leader phases in numeric order;
  - reject ties, zero-overlap candidates, duplicate member assignments, or any unmapped leader phase instead of inventing a raw numeric fallback.
- [ ] Re-run the focused test. Expected: PASS.
- [ ] Run the full pure test project. Expected: PASS.
- [ ] Commit:

  ```powershell
  git add TrafficLightsEnhancement.Logic\TrafficGroups\TrafficGroupPhaseMap.cs TrafficLightsEnhancement.Tests\TrafficGroups\TrafficGroupPhaseMapTests.cs
  git commit -m "test: define traffic group movement mapping"
  ```

---

### Task 2: Make group-demand aggregation fair and map based

**Files:**

- Modify: `TrafficLightsEnhancement.Logic/TrafficGroups/VanillaTrafficGroupDemandPolicy.cs`
- Modify: `TrafficLightsEnhancement.Tests/TrafficGroups/VanillaTrafficGroupDemandPolicyTests.cs`

**Changed interface:**

```csharp
public static bool TryRemapMemberToLeader(
    VanillaTrafficGroupDemand memberDemand,
    TrafficGroupPhaseMap phaseMap,
    out VanillaTrafficGroupDemand leaderDemand);
```

- [ ] Replace `Higher_priority_follower_replaces_lower_priority_leader` with a failing test proving a lower-priority leader winner and a higher-priority follower winner both remain in `RequestedPhaseMask` and `ExtendablePhaseMask`, while `HighestPriority` remains the numeric maximum.
- [ ] Add a failing swapped-map test proving member `G1` demand becomes leader `G2` demand and member `G2` demand becomes leader `G1` demand.
- [ ] Add failing tests proving an incomplete map rejects demand remapping and that suppressed masks are translated and unioned.
- [ ] Run:

  ```powershell
  dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj --filter FullyQualifiedName~VanillaTrafficGroupDemandPolicyTests
  ```

  Expected: FAIL on the old winner-takes-all merge and missing map-based remapper.
- [ ] Change `Merge` to union every member's already-local-winning requested, extendable, and suppressed masks. Preserve `Math.Max(current.HighestPriority, candidate.HighestPriority)` only as metadata used by the existing zero-priority suppression rule.
- [ ] Replace count/wrap-based `TryRemap` and its private `RemapMask` with `TryRemapMemberToLeader`, iterating set member bits through `TrafficGroupPhaseMap.TryMapMemberToLeader`.
- [ ] Keep `MapRequiredOneBasedPhase` and `MapOptionalOneBasedPhase` temporarily so the runtime still compiles before Task 5. Remove them in Task 5 after every lockstep call site moves to the movement map.
- [ ] Run the focused and full pure tests. Expected: PASS.
- [ ] Commit:

  ```powershell
  git add TrafficLightsEnhancement.Logic\TrafficGroups\VanillaTrafficGroupDemandPolicy.cs TrafficLightsEnhancement.Tests\TrafficGroups\VanillaTrafficGroupDemandPolicyTests.cs
  git commit -m "fix: preserve every group member demand winner"
  ```

---

### Task 3: Specify runtime mapping and scheduler contracts before production changes

**Files:**

- Modify: `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs`

- [ ] Add a source test proving `TrafficGroupSystem` builds phase signatures from:
  - `NodeUtils.GetLaneConnectionMap`;
  - each lane signal's `m_GroupMask`;
  - the connection's incoming `m_SourceEdge`;
  - `GetEdgePositionForJunction`;
  - separate car-lane and track-lane axis masks;
  - `TrafficGroupMovementMappingPolicy.TryBuild`.
- [ ] Add a source test proving runtime-only mapping is written as `TrafficGroupPhaseMapping` and is absent/invalid when a complete map cannot be built.
- [ ] Replace `Active_leader_shard_collects_and_synchronizes_all_group_members` with a test proving:
  - `TrafficGroupRuntimeData` records the leader's shared `UpdateFrame.m_Index`;
  - collection and follower passes use that shard value;
  - `DiscoverActiveGroupedBaseDemandJob`, `m_ActiveGroupedBaseDemand`, and `activeGroupedBaseDemand` are absent.
- [ ] Replace the raw-number follower assertion with tests proving:
  - same-tick current and nonzero next phases call `TryMapLeaderToMember`;
  - next phase zero remains zero;
  - missing/incomplete mapping reaches `UpdateGroupedBaseFollowerIndependently`;
  - no lockstep branch calls `MapRequiredOneBasedPhase`, `MapOptionalOneBasedPhase`, `WrapPhase`, or modulo mapping.
- [ ] Add a scoped allocator test extracting the coordination-container allocation block and asserting it contains `Allocator.Persistent` and not `Allocator.TempJob`.
- [ ] Retain the existing three-pass ordering, local-demand reset-once, custom-path, and grouped-TSP-suspension contracts.
- [ ] Run:

  ```powershell
  dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj --filter FullyQualifiedName~TrafficGroupSystemSourceTests -p:LangVersion=latest
  ```

  Expected: FAIL because the new runtime mapping/shard types and behavior do not exist.
- [ ] Commit the failing contract tests:

  ```powershell
  git add TrafficLightsEnhancement.Ecs.Tests\TrafficGroupSystemSourceTests.cs
  git commit -m "test: specify mapped traffic group runtime"
  ```

---

### Task 4: Build transient maps and leader-shard metadata

**Files:**

- Create: `TrafficLightsEnhancement/Components/TrafficGroupRuntimeData.cs`
- Create: `TrafficLightsEnhancement/Components/TrafficGroupPhaseMapping.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`

**Runtime components:**

```csharp
public struct TrafficGroupRuntimeData : IComponentData
{
    public uint m_LeaderUpdateFrameIndex;
}

public struct TrafficGroupPhaseMapping : IComponentData
{
    public TrafficGroupPhaseMap m_Map;
}
```

- [ ] Add the two components without `ISerializable`; add comments stating they are reconstructed runtime state.
- [ ] In `TrafficGroupSystem.OnUpdate`, before publishing the master clock:
  - identify the live leader;
  - read its shared `UpdateFrame` through `EntityManager.TryGetSharedComponent`;
  - set or add `TrafficGroupRuntimeData` on the group;
  - rebuild movement signatures for the leader and every member from current lane signals and topology.
- [ ] Implement `BuildPhaseSignatures(Entity junction, TrafficLights lights)`:
  - reject counts outside `1..16`;
  - obtain `SubLane`, `ConnectedEdge`, `Lane`, `Edge`, and `EdgeGeometry` lookups;
  - build `NodeUtils.GetLaneConnectionMap` with `Allocator.Temp` and dispose it in the same method;
  - ignore pedestrian-only lanes;
  - for car and/or track lane signals with a valid incoming source edge, quantize the undirected edge-to-node axis and OR it into every phase selected by `LaneSignal.m_GroupMask`;
  - return no signature set when any required phase has no motor/track approach.
- [ ] Call `TrafficGroupMovementMappingPolicy.TryBuild` for leader-to-member signatures. Set/add `TrafficGroupPhaseMapping` only for a complete map; remove a stale component when rebuilding fails.
- [ ] Rebuild from current data each `TrafficGroupSystem` update. This makes load reconstruction, pattern changes, leader changes, and topology changes self-healing without serialized invalidation flags. Avoid persistent native allocations in this main-thread path.
- [ ] Remove a member's runtime mapping when it leaves a group and allow group destruction to remove `TrafficGroupRuntimeData` with the group entity.
- [ ] Run the focused ECS/source test. Expected: it still fails only on the unimplemented simulation consumption.
- [ ] Run:

  ```powershell
  dotnet build TrafficLightsEnhancement\TrafficLightsEnhancement.csproj -c Debug -p:DisablePostProcessors=true -p:LangVersion=latest
  ```

  Expected: PASS without installing the mod or building the UI.
- [ ] Commit:

  ```powershell
  git add TrafficLightsEnhancement\Components\TrafficGroupRuntimeData.cs TrafficLightsEnhancement\Components\TrafficGroupPhaseMapping.cs TrafficLightsEnhancement\Systems\TrafficGroupSystem.cs
  git commit -m "feat: derive transient traffic group movement maps"
  ```

---

### Task 5: Consume maps in the three-pass simulation and fix job lifetimes

**Files:**

- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`

- [ ] Add read-only `ComponentLookup<TrafficGroupRuntimeData>` and `ComponentLookup<TrafficGroupPhaseMapping>` fields to `ExtraTypeHandle`, including `AssignHandles` and `Update`.
- [ ] Pass the current `UpdateFrame` index into `UpdateTrafficLightsJob`.
- [ ] Replace `m_ActiveGroupedBaseDemand` checks with `IsActiveCoordinatedGroup`: validate the member/group, validate `TrafficGroupRuntimeData`, and require its leader shard to equal the current update-frame index.
- [ ] Delete `DiscoverActiveGroupedBaseDemandJob`, its schedule, its map allocation, and its disposal dependency.
- [ ] In `CollectAndResetGroupedBaseDemand`:
  - keep the existing local per-junction priority arbitration unchanged;
  - store local demand for independent fallback;
  - require a complete `TrafficGroupPhaseMapping`;
  - call `VanillaTrafficGroupDemandPolicy.TryRemapMemberToLeader`;
  - append only successfully remapped demand to the group multi-map.
- [ ] In `GetGroupedLeaderDemand`, merge all remapped member-local winners with the fair `Merge` policy. The leader's own complete identity map is built by Task 4, so it follows the same path.
- [ ] Publish the leader's same-tick state only for an active coordinated base group with a valid complete leader map.
- [ ] Change the follower pass:
  - if the group is inactive for this update shard, do nothing;
  - if same-tick master state and a complete member map exist, translate required current and optional nonzero next phase through `TryMapLeaderToMember`, copy state/timers, and refresh lane signals/objects;
  - if either translated phase is unavailable, run the follower independently from its already-collected local demand;
  - never read the serialized group master clock as a raw-number fallback.
- [ ] Update `CustomStateMachine.ShouldFollowLeader` and both sync overloads to require a complete movement map. Translate the master phase first; for green-wave mode apply the existing phase offset and timer delay after that translated phase. If mapping is unavailable, return to the normal local custom state machine.
- [ ] Remove the now-unused `VanillaTrafficGroupDemandPolicy.MapRequiredOneBasedPhase`, `MapOptionalOneBasedPhase`, and raw lockstep `WrapPhase` helpers only after `rg` confirms no remaining caller needs them.
- [ ] Allocate only `localGroupedDemand`, `groupedDemand`, and `sameTickMasterState` for coordination, using `Allocator.Persistent`. Dispose all three from `followerDependency`. Preserve strict collection → leader → follower dependencies.
- [ ] Keep the existing tram/bus approach-index allocators unchanged; this task addresses only the four coordination allocations introduced by `171d6fd`.
- [ ] Run the focused ECS/source test. Expected: PASS.
- [ ] Run all pure tests. Expected: PASS.
- [ ] Run all ECS tests with `-p:LangVersion=latest`. Expected: PASS without deployment.
- [ ] Run all serialization tests with `-p:LangVersion=latest`. Expected: PASS without a save-layout regression.
- [ ] Run the non-deploying Debug build from Task 4. Expected: PASS.
- [ ] Commit:

  ```powershell
  git add TrafficLightsEnhancement\Systems\TrafficLightSystems\Simulation\ExtraTypeHandle.cs TrafficLightsEnhancement\Systems\TrafficLightSystems\Simulation\PatchedTrafficLightSystem.cs TrafficLightsEnhancement\Systems\TrafficLightSystems\Simulation\CustomStateMachine.cs
  git commit -m "fix: synchronize traffic groups by physical movement"
  ```

---

### Task 6: Make diagnostics truthful and document the behavior

**Files:**

- Modify: `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
- Modify: `docs/traffic-groups.md`

- [ ] Add a failing source assertion that the existing `TSPDiagnosticsTrafficGroupMasterPhase` row calls a formatter that reads `TrafficGroupPhaseMapping`.
- [ ] Change `FormatTrafficGroupMasterPhase` to receive the selected entity and:
  - show `leader G1 -> member G2` style current/next translations for a mapped follower;
  - show `Identity mapping` for a mapped leader;
  - show `Movement mapping unavailable; running independently` when the component is absent, incomplete, or cannot translate the current phase.
- [ ] Reuse the existing localized row label `TSPDiagnosticsTrafficGroupMasterPhase` (`Group phase mapping`). Do not add a new localization key or hand-edit sibling locale files.
- [ ] Update `docs/traffic-groups.md` to explain automatic physical movement mapping, inverse demand remapping, fair cross-member union, independent fallback, and the diagnostic text.
- [ ] Run the focused ECS/source test and full ECS test project. Expected: PASS.
- [ ] Run `git diff --check`. Expected: no whitespace errors.
- [ ] Commit:

  ```powershell
  git add TrafficLightsEnhancement\Systems\UI\UISystem.UIBIndings.cs TrafficLightsEnhancement.Ecs.Tests\TrafficGroupSystemSourceTests.cs docs\traffic-groups.md
  git commit -m "docs: expose traffic group movement mapping"
  ```

---

### Task 7: Complete non-deploying verification

- [ ] Run:

  ```powershell
  dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj -p:LangVersion=latest
  dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest
  dotnet test TrafficLightsEnhancement.Serialization.Tests\TrafficLightsEnhancement.Serialization.Tests.csproj -p:LangVersion=latest
  dotnet build TrafficLightsEnhancement\TrafficLightsEnhancement.csproj -c Release -p:DisablePostProcessors=true -p:LangVersion=latest
  git diff --check
  ```

- [ ] Record exact pass counts and build warnings/errors.
- [ ] Run `rg -n "DiscoverActiveGroupedBaseDemandJob|m_ActiveGroupedBaseDemand|activeGroupedBaseDemand|MapRequiredOneBasedPhase|MapOptionalOneBasedPhase" TrafficLightsEnhancement TrafficLightsEnhancement.Logic`. Expected: no remaining raw-number coordination or discovery references.
- [ ] Inspect `git status --short` and `git diff --stat origin/main...HEAD`; confirm only planned branch changes plus the preserved unrelated untracked old plan.
- [ ] Stop before deployment while Cities: Skylines II is open. Tell the user the verified build is ready and ask them to quit the game.

---

### Task 8: Install and run the guarded live acceptance test

- [ ] Receive confirmation that Cities: Skylines II is closed.
- [ ] Verify no process is running:

  ```powershell
  Get-Process Cities2 -ErrorAction SilentlyContinue
  ```

- [ ] Run the deploying build:

  ```powershell
  dotnet build TrafficLightsEnhancement\TrafficLightsEnhancement.csproj -c Release -p:LangVersion=latest
  ```

- [ ] Verify the installed mod directory:
  `C:\Users\matt\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\C2VM.TrafficLightsEnhancement`.
- [ ] Hash the built and installed `C2VM.TrafficLightsEnhancement.dll`; where post-processing changes the binary, decompile/string-inspect the installed copy to prove the movement-map and three-pass symbols are present.
- [ ] Ask the user to launch and load Copeland. Do not estimate or rush the load time.
- [ ] Re-enable the same two-junction group and keep diagnostics enabled.
- [ ] Confirm the follower diagnostic shows the expected non-identity movement translation rather than raw `G1 -> G1`.
- [ ] Unpause and observe at least two complete cycles. Sample leader and follower in the same frame at each transition.
- [ ] Success requires:
  - equivalent corridor movements show the same physical signal state;
  - both phases receive service under queued demand;
  - mapped current/next phases and timers agree with diagnostics;
  - no follower uses raw-number lockstep when mapping is unavailable;
  - fresh `Player.log` contains no new coordination-related `JobTempAlloc` or invalid-pointer reports.
- [ ] Immediately pause and remove the group if a queue grows continuously, a phase is starved, or physical signals contradict the reported mapping.
- [ ] Only after the live test passes, update the branch/PR as separately authorized. Prefix any GitHub-facing text with `*Written by Codex.*`; do not merge without explicit permission.

## Plan self-review

- [x] Every failure in the approved design has a production change and a red/green test.
- [x] Mapping is based on physical road/track axes, not local phase ordinals.
- [x] The packed map supports Burst/runtime use without a managed collection.
- [x] Ambiguity and incomplete topology fail closed.
- [x] Leader update sharding remains correct after removal of the active-group discovery map.
- [x] Every member's local winner survives group aggregation.
- [x] Current and optional next phases use the map, with zero preserved.
- [x] Custom and green-wave paths consume the map without changing saved phase definitions.
- [x] Coordination allocations outlive long job chains and are disposed after the follower pass.
- [x] No serialized layouts, mod identifiers, or grouped-TSP policy change.
- [x] Diagnostics reuse the existing localized row and disclose mapping failure.
- [x] Non-deploying verification completes before the user is asked to quit.
- [x] The live test checks physical signals, phase fairness, and allocator logs.
- [x] No step leaves an unresolved placeholder or asks the implementer to invent behavior.
