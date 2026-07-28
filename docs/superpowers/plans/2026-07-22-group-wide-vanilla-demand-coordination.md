# Group-Wide Vanilla Demand Coordination Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make coordinated vanilla and predefined-pattern traffic-group members contribute their local lane demand to one leader decision, then apply that new state to followers in the same simulation tick without converting an optional zero next phase into G1.

**Architecture:** Add a Unity-free demand summary and selection policy to `TrafficLightsEnhancement.Logic`, then run the existing ECS traffic-light job in three dependency-ordered passes: collect and consume grouped member demand, update independent junctions and leaders, and synchronize grouped base-state-machine followers. Runtime-only native maps carry demand and master state between passes; missing data takes an independent local fallback path and no serialized component changes.

**Tech Stack:** C# 10/12, .NET Standard 2.0 pure logic, Unity Entities jobs and native containers, xUnit pure tests, net48 ECS source-contract tests, Cities: Skylines II mod toolchain.

## Global Constraints

- Work only on `codex/fix-vanilla-group-lockstep` in `.worktrees/codex-fix-vanilla-group-lockstep`; do not edit `main`.
- Preserve the unrelated untracked `docs/superpowers/plans/2026-07-22-vanilla-traffic-group-lockstep.md` file.
- Keep `C2VM.TrafficLightsEnhancement` assembly and root namespace identifiers unchanged.
- Do not change `TrafficGroup`, `TrafficGroupMember`, save versions, or serialized layouts.
- Do not change custom-phase follower policy or enable TSP for grouped intersections.
- Do not run a normal `TrafficLightsEnhancement.csproj` Release build while Cities: Skylines II is open because the mod targets deploy to the live local mod directory.
- Use `apply_patch` for source edits and add a failing test before each production behavior change.
- If a native map entry, group reference, leader state, or phase count is invalid, update the affected base-state-machine junction independently from its already-collected local demand; never copy stale group state or freeze.

## File and Responsibility Map

- Create `TrafficLightsEnhancement.Logic/TrafficGroups/VanillaTrafficGroupDemandPolicy.cs`: unmanaged demand value, priority merge, phase-mask remap, optional phase mapping, and vanilla cyclic selection.
- Create `TrafficLightsEnhancement.Tests/TrafficGroups/VanillaTrafficGroupDemandPolicyTests.cs`: pure behavior coverage for aggregation, remapping, selection, and zero sentinel handling.
- Modify `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs`: three-pass scheduling, lane-demand collection/reset, group aggregation, same-tick master publication, follower synchronization, and local fallback.
- Modify `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`: accept a runtime master snapshot for follower application and use required-versus-optional phase mapping without changing custom follower routing.
- Modify `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs`: replace the now-invalid pre-dispatch follower assertion with source contracts for pass ordering and branch boundaries.
- Modify `docs/traffic-groups.md`: document group-wide base-state-machine demand, same-tick follower state, optional phase semantics, and fallback behavior.

---

### Task 1: Add the pure vanilla group-demand policy

**Files:**

- Create: `TrafficLightsEnhancement.Logic/TrafficGroups/VanillaTrafficGroupDemandPolicy.cs`
- Create: `TrafficLightsEnhancement.Tests/TrafficGroups/VanillaTrafficGroupDemandPolicyTests.cs`

**Interfaces:**

```csharp
public readonly struct VanillaTrafficGroupDemand
{
    public VanillaTrafficGroupDemand(
        int highestPriority,
        int requestedPhaseMask,
        int extendablePhaseMask,
        int suppressedPhaseMask);

    public int HighestPriority { get; }
    public int RequestedPhaseMask { get; }
    public int ExtendablePhaseMask { get; }
    public int SuppressedPhaseMask { get; }
}

public static class VanillaTrafficGroupDemandPolicy
{
    public static VanillaTrafficGroupDemand Merge(
        VanillaTrafficGroupDemand current,
        VanillaTrafficGroupDemand candidate);

    public static bool TryRemap(
        VanillaTrafficGroupDemand demand,
        int sourcePhaseCount,
        int targetPhaseCount,
        out VanillaTrafficGroupDemand remapped);

    public static int MapRequiredOneBasedPhase(int phase, int phaseCount);
    public static int MapOptionalOneBasedPhase(int phase, int phaseCount);

    public static int SelectNextPhase(
        VanillaTrafficGroupDemand demand,
        int currentPhase,
        int phaseCount,
        bool preferChange,
        out bool canExtend);
}
```

- [ ] Add `VanillaTrafficGroupDemandPolicyTests.cs` with these initial tests:

```csharp
[Fact]
public void Higher_priority_follower_replaces_lower_priority_leader()
{
    var leader = new VanillaTrafficGroupDemand(1, 0b0001, 0b0001, 0);
    var follower = new VanillaTrafficGroupDemand(5, 0b0100, 0, 0);

    VanillaTrafficGroupDemand merged = VanillaTrafficGroupDemandPolicy.Merge(leader, follower);

    Assert.Equal(5, merged.HighestPriority);
    Assert.Equal(0b0100, merged.RequestedPhaseMask);
    Assert.Equal(0, merged.ExtendablePhaseMask);
}

[Fact]
public void Equal_priority_members_combine_requested_and_extendable_masks()
{
    var leader = new VanillaTrafficGroupDemand(3, 0b0001, 0b0001, 0);
    var follower = new VanillaTrafficGroupDemand(3, 0b0100, 0b0100, 0b0010);

    VanillaTrafficGroupDemand merged = VanillaTrafficGroupDemandPolicy.Merge(leader, follower);

    Assert.Equal(3, merged.HighestPriority);
    Assert.Equal(0b0101, merged.RequestedPhaseMask);
    Assert.Equal(0b0101, merged.ExtendablePhaseMask);
    Assert.Equal(0b0010, merged.SuppressedPhaseMask);
}

[Fact]
public void Remap_wraps_member_masks_into_leader_phase_space()
{
    var member = new VanillaTrafficGroupDemand(2, 0b1100, 0b0100, 0b1000);

    bool valid = VanillaTrafficGroupDemandPolicy.TryRemap(member, 4, 2, out var remapped);

    Assert.True(valid);
    Assert.Equal(0b0011, remapped.RequestedPhaseMask);
    Assert.Equal(0b0001, remapped.ExtendablePhaseMask);
    Assert.Equal(0b0010, remapped.SuppressedPhaseMask);
}

[Theory]
[InlineData(0, 4, 0)]
[InlineData(4, 3, 1)]
public void Optional_phase_preserves_zero_and_wraps_nonzero(int phase, int phaseCount, int expected)
{
    Assert.Equal(expected, VanillaTrafficGroupDemandPolicy.MapOptionalOneBasedPhase(phase, phaseCount));
}

[Theory]
[InlineData(0, 2)]
[InlineData(2, 0)]
public void Invalid_phase_counts_reject_aggregation(int sourceCount, int targetCount)
{
    bool valid = VanillaTrafficGroupDemandPolicy.TryRemap(
        new VanillaTrafficGroupDemand(1, 1, 1, 0),
        sourceCount,
        targetCount,
        out _);

    Assert.False(valid);
}

[Fact]
public void No_positive_priority_honors_suppressed_masks()
{
    var demand = new VanillaTrafficGroupDemand(0, 0b0011, 0, 0b0010);

    int next = VanillaTrafficGroupDemandPolicy.SelectNextPhase(
        demand,
        currentPhase: 1,
        phaseCount: 2,
        preferChange: true,
        out bool canExtend);

    Assert.Equal(1, next);
    Assert.False(canExtend);
}
```

- [ ] Run `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj --filter FullyQualifiedName~VanillaTrafficGroupDemandPolicyTests`. Expected: FAIL because the new policy types do not exist.
- [ ] Implement `VanillaTrafficGroupDemand` as a readonly unmanaged value with constructor-set properties.
- [ ] Implement `Merge` with the exact inherited semantics: always union suppressed masks; replace requested/extendable masks when the candidate priority is higher; union them when priorities are equal; ignore lower-priority requested/extendable masks.
- [ ] Implement mask remapping by iterating source phases `1..sourcePhaseCount`, mapping set bits through `TrafficGroupTimingPolicy.WrapOneBasedPhase`, and setting the corresponding target bit. Reject counts outside `1..31` so signed `int` shifts remain defined.
- [ ] Implement `MapRequiredOneBasedPhase` through `TrafficGroupTimingPolicy.WrapOneBasedPhase`; implement `MapOptionalOneBasedPhase` as `phase == 0 ? 0 : MapRequiredOneBasedPhase(phase, phaseCount)`.
- [ ] Implement `SelectNextPhase` by moving the cyclic mask-selection tail of `GetNextSignalGroupWithoutTsp` into the pure layer: when priority is zero, clear suppressed bits and force `preferChange = false`; compute `canExtend` from the winning extendable mask; scan from the preferred start through the end, then wrap to the beginning; return current phase when no bit is eligible.
- [ ] Add boundary tests for required phase wrapping, phase 31 masks, selection wraparound, and `canExtend` only when the current winning phase is extendable.
- [ ] Re-run the focused pure tests. Expected: PASS.
- [ ] Run `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj`. Expected: all pure tests PASS.
- [ ] Commit only the two pure-policy files with `git commit -m "test: define group-wide vanilla demand policy"`.

---

### Task 2: Define ECS orchestration contracts before changing runtime code

**Files:**

- Modify: `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs`

**Interfaces:**

The runtime job will expose these source-level concepts for contract tests:

```csharp
private enum TrafficLightUpdatePass
{
    CollectGroupedBaseDemand,
    UpdateLeadersAndIndependent,
    SynchronizeGroupedBaseFollowers
}

private readonly struct TrafficGroupMasterSignalState
{
    // state, required current phase, optional next phase, timers, phase count
}
```

- [ ] Replace `Coordinated_followers_sync_before_custom_or_vanilla_dispatch` with source tests that assert:

```csharp
[Fact]
public void Grouped_base_state_machine_runs_collection_leader_and_follower_passes_in_order()
{
    string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
    string onUpdate = ExtractMethod(source, "protected override void OnUpdate");

    int collect = onUpdate.IndexOf("TrafficLightUpdatePass.CollectGroupedBaseDemand", StringComparison.Ordinal);
    int leaders = onUpdate.IndexOf("TrafficLightUpdatePass.UpdateLeadersAndIndependent", StringComparison.Ordinal);
    int followers = onUpdate.IndexOf("TrafficLightUpdatePass.SynchronizeGroupedBaseFollowers", StringComparison.Ordinal);

    Assert.True(collect >= 0, "Could not find grouped demand collection pass.");
    Assert.True(leaders > collect, "Leader updates must depend on demand collection.");
    Assert.True(followers > leaders, "Follower synchronization must depend on leader updates.");
}

[Fact]
public void Grouped_base_demand_is_consumed_only_in_collection_pass()
{
    string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
    string execute = ExtractSection(source, "public void Execute(in ArchetypeChunk chunk", "private void FillLaneSignals");

    Assert.Contains("CollectAndResetGroupedBaseDemand", execute);
    Assert.Contains("m_LocalGroupedDemand", execute);
    Assert.Contains("m_GroupedDemand", execute);
    Assert.Contains("UseCollectedDemand", execute);
}

[Fact]
public void Missing_same_tick_master_uses_independent_base_fallback()
{
    string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
    string execute = ExtractSection(source, "public void Execute(in ArchetypeChunk chunk", "private void FillLaneSignals");

    Assert.Contains("TryGetValue(groupEntity, out var masterState)", execute);
    Assert.Contains("UpdateGroupedBaseFollowerIndependently", execute);
}

[Fact]
public void Custom_followers_keep_the_existing_custom_sync_path()
{
    string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
    string execute = ExtractSection(source, "public void Execute(in ArchetypeChunk chunk", "private void FillLaneSignals");

    Assert.Contains("usesCustomPhase", execute);
    Assert.Contains("CustomStateMachine.ShouldFollowLeader", execute);
    Assert.Contains("CustomStateMachine.SyncSignalGroupWithLeader", execute);
}
```

- [ ] Add an assertion that the optional mapper is used for `m_NextSignalGroup` while the required mapper is used for `m_CurrentSignalGroup` in `CustomStateMachine.SyncSignalGroupWithLeader`.
- [ ] Run `dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj --filter FullyQualifiedName~TrafficGroupSystemSourceTests`. Expected: FAIL because the three-pass and fallback source contracts do not exist.
- [ ] Commit only the source-test change with `git commit -m "test: specify group-wide vanilla runtime ordering"`.

---

### Task 3: Implement collection, leader aggregation, and same-tick follower sync

**Files:**

- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`

**Runtime values:**

```csharp
private readonly struct TrafficGroupMasterSignalState
{
    public readonly Game.Net.TrafficLightState State;
    public readonly byte CurrentSignalGroup;
    public readonly byte NextSignalGroup;
    public readonly byte Timer;
    public readonly uint CustomTimer;
    public readonly byte SignalGroupCount;
}
```

`UpdateTrafficLightsJob` gains one mutable handle per transient container. Each scheduled pass receives the same container values, but the strict dependency chain prevents overlap; collection and publication call `AsParallelWriter()` locally, while later passes read through the container's normal lookup APIs. Do not put writer and read-only aliases for the same native container into one job instance because Unity's safety system can reject the aliasing even when a pass does not execute both paths.

```csharp
public TrafficLightUpdatePass m_Pass;
public NativeParallelHashMap<Entity, VanillaTrafficGroupDemand> m_LocalGroupedDemand;
public NativeParallelMultiHashMap<Entity, VanillaTrafficGroupDemand> m_GroupedDemand;
public NativeParallelHashMap<Entity, TrafficGroupMasterSignalState> m_SameTickMasterState;
```

- [ ] Add the pass enum and unmanaged runtime master-state struct near `UpdateTrafficLightsJob`.
- [ ] Add `TryGetCoordinatedBaseMember(...)` that identifies a coordinated group/member pair using the base state machine (`!usesCustomPhase`) and separately returns whether both local and master phase counts are valid in `1..31`. This distinction is required so malformed grouped base members take the independent path instead of falling into generic follower synchronization.
- [ ] Add `CollectAndResetGroupedBaseDemand(...)`. It must:
  - read the same `laneSignals` list already produced by `FillLaneSignals`;
  - apply the current-group ignore-priority rule before sampling priority;
  - preserve the moveable-bridge priority cap;
  - record the highest priority, winning request mask, winning extendable mask, and suppressed mask;
  - track local petitioner and blocker entities and update local lane blockers with the same inherited rule;
  - clear `m_Petitioner`, restore `m_Priority = m_Default`, and write every sampled `LaneSignal` exactly once;
  - store the local summary by junction entity for fallback;
  - remap the summary to `group.m_MasterSignalGroupCount` and append it to the multi-map by group entity.
- [ ] At the start of each entity iteration, calculate `usesCustomPhase` before TSP work. In `CollectGroupedBaseDemand`, collect eligible base members, clear `laneSignals`, and continue without executing diagnostics, state updates, or object refresh.
- [ ] Gate the existing `CustomStateMachine.ShouldFollowLeader` dispatch with `usesCustomPhase` so only custom-phase followers use the stored group master-clock path. Keep its behavior unchanged otherwise.
- [ ] In `UpdateLeadersAndIndependent`, defer only coordinated base followers whose aggregation inputs are valid. Run malformed grouped base members independently. Perform the defer at the state-update dispatch point after the existing TSP diagnostic cleanup/setup has run, so grouped followers do not retain stale diagnostic components.
- [ ] For a coordinated base leader, enumerate all `m_GroupedDemand` values for its group and merge them through `VanillaTrafficGroupDemandPolicy.Merge`. If the group has no valid entries, retrieve the local summary and mark the call as independent fallback.
- [ ] Extend `UpdateTrafficLightState`, `GetNextSignalGroup`, and `GetNextSignalGroupWithoutTsp` with a small demand-source value (`UseCollectedDemand` plus summary). When collected demand is supplied, do not reread or reset lane request fields; select through `VanillaTrafficGroupDemandPolicy.SelectNextPhase`. When it is absent, retain the current local lane-reading path, but finish selection through the same pure policy.
- [ ] After a valid coordinated base leader update, publish `TrafficGroupMasterSignalState` keyed by group entity from the newly updated local `trafficLights` and `customTrafficLights` values.
- [ ] In `SynchronizeGroupedBaseFollowers`, process only coordinated base followers with valid phase counts. If a valid same-tick master exists, apply it, update lane signals and traffic-light objects, write the component arrays, clear `laneSignals`, and continue.
- [ ] Add `UpdateGroupedBaseFollowerIndependently(...)`. If the same-tick master is missing or invalid, retrieve the follower's local collected summary, run the normal base state machine with collected demand, refresh lanes/objects only when the state changed, and write the follower state. If local demand is also missing, fall through to the original independent lane-reading path rather than copying the serialized group master clock.
- [ ] Add a `CustomStateMachine.SyncSignalGroupWithLeader` overload that accepts `TrafficGroupMasterSignalState`. Apply current phase through `MapRequiredOneBasedPhase` and next phase through `MapOptionalOneBasedPhase`. Keep the existing group-component overload for custom followers, but change its next-phase mapping to the optional mapper as the shared sentinel fix.
- [ ] Delete `AggregateGroupMemberPriority` only after a repo-wide `rg "AggregateGroupMemberPriority"` confirms it still has no callers; its custom-phase aggregation behavior is not part of this fix and dead code should not imply that it is active.
- [ ] In `OnUpdate`, allocate capacities from `Math.Max(1, m_TrafficLightQuery.CalculateEntityCount())`:
  - one `NativeParallelHashMap<Entity, VanillaTrafficGroupDemand>` for local summaries;
  - one `NativeParallelMultiHashMap<Entity, VanillaTrafficGroupDemand>` for group summaries;
  - one `NativeParallelHashMap<Entity, TrafficGroupMasterSignalState>` for same-tick leader states.
- [ ] Schedule the same job in strict dependency order: collection depends on `base.Dependency`; leaders depend on collection; followers depend on leaders. Do not use `ScheduleParallel` without passing the preceding handle.
- [ ] Dispose all three native containers after the follower handle. Combine those disposal handles with the tram/bus index disposal handles and register the final dependency with `m_EndFrameBarrier`.
- [ ] Run the focused ECS test from Task 2. Expected: PASS.
- [ ] Run `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj`. Expected: all pure tests PASS.
- [ ] Run `dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj`. Expected: all ECS/source-contract tests PASS with `DisablePostProcessors=true`, so no live mod deployment occurs.
- [ ] Run `dotnet test TrafficLightsEnhancement.Serialization.Tests/TrafficLightsEnhancement.Serialization.Tests.csproj`. Expected: all serialization tests PASS, proving no save-contract regression.
- [ ] Run `dotnet build TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest`. Expected: build PASS without deploying the mod.
- [ ] Commit runtime and source-contract completion with `git commit -m "fix: coordinate vanilla traffic groups from member demand"`.

---

### Task 4: Document behavior and complete non-deploying verification

**Files:**

- Modify: `docs/traffic-groups.md`

- [ ] Replace the statement that lockstep followers simply copy the stored master clock with the distinction between custom followers and base-state-machine members.
- [ ] Document that grouped vanilla/predefined members consume local lane demand in a collection pass, the leader merges it, and followers receive the new leader state in the same tick.
- [ ] Document that current phase is required/one-based, next phase is optional, and zero means no pending next phase.
- [ ] Document independent fallback for missing aggregate/master state and reaffirm that grouped TSP remains suspended.
- [ ] Run `rg -n "TBD|TODO|placeholder" TrafficLightsEnhancement.Logic/TrafficGroups/VanillaTrafficGroupDemandPolicy.cs TrafficLightsEnhancement.Tests/TrafficGroups/VanillaTrafficGroupDemandPolicyTests.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs docs/traffic-groups.md`. Expected: no new placeholder text.
- [ ] Run `git diff --check`. Expected: no whitespace errors.
- [ ] Run all three non-deploying test projects again and record exact pass counts.
- [ ] Inspect `git status --short` and `git diff --stat origin/main...HEAD`; confirm only planned files plus the pre-existing untracked old plan are present.
- [ ] Commit the documentation with `git commit -m "docs: explain group-wide vanilla coordination"`.
- [ ] Stop here while Cities: Skylines II is open. Tell the user implementation and safe verification are ready, summarize the exact test evidence, and explicitly ask them to quit before Release build/install.

---

### Task 5: Build, install, and conduct the guarded live test

**Files:**

- Verify output: `TrafficLightsEnhancement/bin/Release/net48/C2VM.TrafficLightsEnhancement.dll`
- Verify installed directory: `%LOCALAPPDATA%Low/Colossal Order/Cities Skylines II/Mods/C2VM.TrafficLightsEnhancement`

- [ ] Receive explicit confirmation that Cities: Skylines II is closed.
- [ ] Confirm no `Cities2` process is running with `Get-Process Cities2 -ErrorAction SilentlyContinue`. Expected: no process.
- [ ] Run `dotnet build TrafficLightsEnhancement/TrafficLightsEnhancement.csproj -c Release -p:LangVersion=latest`. Expected: build and UI build PASS with zero errors; this is the authorized local install step.
- [ ] Verify required installed artifacts exist, including the postprocessed DLL, UI bundle, `mod.json`, and localization resources.
- [ ] Compare the built/deployed artifact timestamps and hashes where postprocessing permits; decompile or string-inspect the installed DLL to confirm the three pass names, demand policy calls, independent fallback, and optional phase mapping are present.
- [ ] Ask the user to launch Cities: Skylines II and load Copeland; do not assume the game's load duration.
- [ ] Recreate a two-junction group with vanilla pattern, lockstep mode, green wave off, zero signal delay, zero phase offset, and diagnostics on.
- [ ] Establish a paused baseline before resuming: record group entity, leader/follower entities, current state, current phase, optional next phase, timers, and visible lane states.
- [ ] Resume and observe at least three complete leader cycles. At each transition, sample leader and follower in the same frame and record whether both member approaches receive service.
- [ ] Immediately pause and remove the traffic group if either queue grows continuously, a member approach is starved, follower state/timer differs from the leader in lockstep, or leader `next = 0` appears as follower `next = G1`.
- [ ] Success requires: follower demand changes the leader's eligible decision when appropriate; every approach receives service within the observed cycles; current/state/timer are same-tick in lockstep; optional zero remains zero; and no new Player/Modding log errors point to TLE Extended.
- [ ] After successful playtesting, run `git status --short`, push `codex/fix-vanilla-group-lockstep`, and update draft PR #159 with the live evidence. Prefix GitHub-facing text with `*Written by Codex.*`. Do not merge without separate explicit authorization.

---

## Plan Self-Review Checklist

- [ ] Every required behavior in `docs/superpowers/specs/2026-07-22-group-wide-vanilla-demand-coordination-design.md` maps to a task and test above.
- [ ] Pure/runtime types agree on `int` masks, one-based phases, a zero optional sentinel, and phase counts limited to `1..31`.
- [ ] Collection resets each grouped member's lane requests once; leader/follower passes never reset them a second time.
- [ ] Custom followers and ungrouped base-state-machine junctions retain their existing dispatch paths.
- [ ] Native containers are runtime-only, dependency-ordered, capacity-bounded, and disposed after the follower pass.
- [ ] No release build or live mod mutation occurs until the user confirms the game is closed.
- [ ] No step contains an unresolved placeholder or asks the implementer to invent behavior not specified here.
