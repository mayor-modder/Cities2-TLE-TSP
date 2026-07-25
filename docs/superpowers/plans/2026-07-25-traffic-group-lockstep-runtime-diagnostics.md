# Traffic Group Lockstep Runtime Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a TLE Extended 1.0.7 diagnostic build that captures every controller and visible-output boundary for an entire selected lockstep group in one game launch.

**Architecture:** A pure logic classifier turns immutable evidence into an explicit lockstep verdict. A runtime-only ECS component records which simulation passes touched each group member plus before/after controller and output hashes. The existing JSONL writer expands one selected group member into a whole-group snapshot with live lane and rendered-object details, compares those live values with the simulation record, and emits a debounced warning for each changed failure signature.

**Tech Stack:** C# 12, .NET 8 test projects, net48 CS2 mod assembly, Unity ECS, Newtonsoft.Json, xUnit, Cities: Skylines II modding toolchain.

## Global Constraints

- Do not change serialized `TrafficGroup`, `TrafficGroupMember`, or save versions.
- Do not change group coordination, movement mapping, demand policy, or signal behavior in the diagnostic build.
- Gate all new collection and logging behind `m_ShowTransitSignalPriorityDiagnostics`.
- Selecting any member must capture the leader and every follower without selecting members one by one.
- Record controller state, all three simulation passes, lane outputs, rendered signal objects, mappings, deterministic hashes, and explicit missing-evidence reasons.
- Do not silently truncate a selected group.
- Do not inspect or modify unrelated mods.
- Use deterministic repository-owned hashing; do not use `GetHashCode`.
- Bump every authoritative TLE Extended version field from `1.0.6` to `1.0.7` before installing the diagnostic build.
- Install only through the normal Release build while Cities: Skylines II is closed.
- Verify the installed DLL hash matches the built DLL before requesting the single diagnostic playtest.

---

## File structure

- Create `TrafficLightsEnhancement.Logic/TrafficGroups/TrafficGroupLockstepDiagnostics.cs`
  - Pure snapshots, evidence, dispositions, verdicts, deterministic hash primitive, and classifier.
- Create `TrafficLightsEnhancement.Tests/TrafficGroups/TrafficGroupLockstepDiagnosticsTests.cs`
  - Literal evidence fixtures for every verdict branch.
- Create `TrafficLightsEnhancement/Components/TrafficGroupLockstepDebugState.cs`
  - Runtime-only ECS record made entirely of primitives and the pure snapshot/disposition types.
- Create `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TrafficGroupLockstepRuntimeDiagnostics.cs`
  - Unity-specific controller snapshots plus deterministic lane/rendered-object hashes.
- Modify `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`
  - Ensure debug components on all group members while diagnostics are enabled and remove them when disabled.
- Modify `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs`
  - Add a writable debug-state lookup.
- Modify `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs`
  - Record independent-pass and synchronization-pass evidence without changing state-machine decisions.
- Create `TrafficLightsEnhancement/Systems/UI/UISystem.TrafficGroupLockstepDiagnostics.cs`
  - Build whole-group JSONL records, capture live lane/rendered objects, classify evidence, and debounce warnings.
- Modify `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
  - Attach `trafficGroupLockstep` to existing JSONL events.
- Modify `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs`
  - Guard instrumentation gating, pass coverage, and gameplay-state noninterference.
- Modify `TrafficLightsEnhancement.Ecs.Tests/UISystemSourceTests.cs`
  - Guard whole-group expansion, detailed output capture, verdicts, and warning debounce.
- Modify `TrafficLightsEnhancement/TrafficLightsEnhancement.csproj`
  - Set assembly/informational version `1.0.7.0`.
- Modify `TrafficLightsEnhancement/UI/mod.json`
  - Set UI module version `1.0.7`.
- Modify `TrafficLightsEnhancement.Tests/Compatibility/ReleaseVersionTests.cs`
  - Set expected semantic version `1.0.7`.

---

### Task 1: Pure lockstep evidence and verdict classifier

**Files:**
- Create: `TrafficLightsEnhancement.Logic/TrafficGroups/TrafficGroupLockstepDiagnostics.cs`
- Create: `TrafficLightsEnhancement.Tests/TrafficGroups/TrafficGroupLockstepDiagnosticsTests.cs`

**Interfaces:**
- Produces: `TrafficGroupLockstepControllerSnapshot`
- Produces: `TrafficGroupLockstepPassFlags`
- Produces: `TrafficGroupLockstepSyncDisposition`
- Produces: `TrafficGroupLockstepVerdict`
- Produces: `TrafficGroupLockstepEvidence`
- Produces: `TrafficGroupLockstepDiagnostics.Classify(in TrafficGroupLockstepEvidence)`
- Produces: `TrafficGroupLockstepDiagnostics.AddHash(ulong, ulong)`

- [ ] **Step 1: Write the failing classifier tests**

Create literal fixtures with no production helper used to derive expected
values. The minimum test names and expected verdicts are:

```csharp
[Theory]
[InlineData(TrafficGroupLockstepSyncDisposition.MissingMaster,
    TrafficGroupLockstepVerdict.SynchronizationDidNotRun)]
[InlineData(TrafficGroupLockstepSyncDisposition.InvalidMaster,
    TrafficGroupLockstepVerdict.SynchronizationRefused)]
[InlineData(TrafficGroupLockstepSyncDisposition.IncompleteMapping,
    TrafficGroupLockstepVerdict.SynchronizationRefused)]
public void Sync_failure_is_reported_with_explicit_verdict(
    TrafficGroupLockstepSyncDisposition disposition,
    TrafficGroupLockstepVerdict expected)
```

```csharp
[Fact]
public void Independent_pass_mutation_wins_over_later_matching_sync()
```

```csharp
[Fact]
public void Live_controller_mutation_after_sync_is_reported()
```

```csharp
[Fact]
public void Live_lane_hash_mutation_after_sync_is_reported()
```

```csharp
[Fact]
public void Live_rendered_hash_mutation_after_sync_is_reported()
```

```csharp
[Fact]
public void Mapped_phase_missing_from_output_masks_is_reported()
```

```csharp
[Fact]
public void Matching_lockstep_evidence_is_in_sync()
```

```csharp
[Fact]
public void Green_wave_is_excluded_from_strict_lockstep_verdict()
```

```csharp
[Fact]
public void Missing_debug_component_is_insufficient_evidence()
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj -p:LangVersion=latest --filter FullyQualifiedName~TrafficGroupLockstepDiagnosticsTests
```

Expected: compilation fails because the new lockstep diagnostic types do not
exist.

- [ ] **Step 3: Implement the pure types and classifier**

Use immutable value types with primitive fields:

```csharp
public readonly struct TrafficGroupLockstepControllerSnapshot
{
    public readonly byte State;
    public readonly byte CurrentGroup;
    public readonly byte NextGroup;
    public readonly byte Timer;
    public readonly byte SignalGroupCount;
    public readonly uint CustomTimer;

    public TrafficGroupLockstepControllerSnapshot(
        byte state,
        byte currentGroup,
        byte nextGroup,
        byte timer,
        byte signalGroupCount,
        uint customTimer)
    {
        State = state;
        CurrentGroup = currentGroup;
        NextGroup = nextGroup;
        Timer = timer;
        SignalGroupCount = signalGroupCount;
        CustomTimer = customTimer;
    }
}

[Flags]
public enum TrafficGroupLockstepPassFlags : byte
{
    None = 0,
    CollectionVisited = 1,
    IndependentVisited = 2,
    IndependentDeferred = 4,
    IndependentHeld = 8,
    IndependentAdvanced = 16,
    SynchronizationVisited = 32,
    SynchronizationApplied = 64,
}

public enum TrafficGroupLockstepSyncDisposition : byte
{
    None,
    Applied,
    NotLockstep,
    MissingMaster,
    InvalidMaster,
    MissingMapping,
    IncompleteMapping,
    UnmappedCurrentPhase,
    UnmappedNextPhase,
    InactiveGroup,
    MissingLocalDemand,
}

public enum TrafficGroupLockstepVerdict : byte
{
    InSync,
    GreenWaveExcluded,
    InsufficientEvidence,
    IndependentStateMachineAdvanced,
    SynchronizationDidNotRun,
    SynchronizationRefused,
    ControllerChangedAfterSynchronization,
    LaneOutputsChangedAfterSynchronization,
    RenderedOutputsChangedAfterSynchronization,
    OutputMasksDoNotRepresentMappedPhase,
}
```

Define the evidence contract exactly as:

```csharp
public readonly struct TrafficGroupLockstepEvidence
{
    public readonly bool HasDebugState;
    public readonly bool IsCoordinated;
    public readonly bool IsGreenWave;
    public readonly TrafficGroupLockstepPassFlags PassFlags;
    public readonly TrafficGroupLockstepSyncDisposition SyncDisposition;
    public readonly TrafficGroupLockstepControllerSnapshot Before;
    public readonly TrafficGroupLockstepControllerSnapshot Master;
    public readonly TrafficGroupLockstepControllerSnapshot After;
    public readonly TrafficGroupLockstepControllerSnapshot Live;
    public readonly ulong LaneHashAfter;
    public readonly ulong LiveLaneHash;
    public readonly ulong RenderedHashAfter;
    public readonly ulong LiveRenderedHash;
    public readonly ushort MappedCurrentGroupBit;
    public readonly ushort MappedNextGroupBit;
    public readonly ushort LiveOutputGroupMask;

    public TrafficGroupLockstepEvidence(
        bool hasDebugState,
        bool isCoordinated,
        bool isGreenWave,
        TrafficGroupLockstepPassFlags passFlags,
        TrafficGroupLockstepSyncDisposition syncDisposition,
        TrafficGroupLockstepControllerSnapshot before,
        TrafficGroupLockstepControllerSnapshot master,
        TrafficGroupLockstepControllerSnapshot after,
        TrafficGroupLockstepControllerSnapshot live,
        ulong laneHashAfter,
        ulong liveLaneHash,
        ulong renderedHashAfter,
        ulong liveRenderedHash,
        ushort mappedCurrentGroupBit,
        ushort mappedNextGroupBit,
        ushort liveOutputGroupMask)
    {
        HasDebugState = hasDebugState;
        IsCoordinated = isCoordinated;
        IsGreenWave = isGreenWave;
        PassFlags = passFlags;
        SyncDisposition = syncDisposition;
        Before = before;
        Master = master;
        After = after;
        Live = live;
        LaneHashAfter = laneHashAfter;
        LiveLaneHash = liveLaneHash;
        RenderedHashAfter = renderedHashAfter;
        LiveRenderedHash = liveRenderedHash;
        MappedCurrentGroupBit = mappedCurrentGroupBit;
        MappedNextGroupBit = mappedNextGroupBit;
        LiveOutputGroupMask = liveOutputGroupMask;
    }
}

public readonly struct TrafficGroupLockstepClassification
{
    public readonly TrafficGroupLockstepVerdict Verdict;
    public readonly string Reason;

    public TrafficGroupLockstepClassification(
        TrafficGroupLockstepVerdict verdict,
        string reason)
    {
        Verdict = verdict;
        Reason = reason;
    }
}
```

`Classify` must implement the comparison order from the approved design.
`AddHash` must implement FNV-1a over each supplied 64-bit value:

```csharp
public static ulong AddHash(ulong hash, ulong value)
{
    const ulong prime = 1099511628211UL;
    for (int shift = 0; shift < 64; shift += 8)
    {
        hash ^= (byte)(value >> shift);
        hash *= prime;
    }
    return hash;
}
```

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the Step 2 command.

Expected: all `TrafficGroupLockstepDiagnosticsTests` pass.

- [ ] **Step 5: Commit the pure diagnostic policy**

```powershell
git add TrafficLightsEnhancement.Logic\TrafficGroups\TrafficGroupLockstepDiagnostics.cs TrafficLightsEnhancement.Tests\TrafficGroups\TrafficGroupLockstepDiagnosticsTests.cs
git commit -m "test: define traffic group lockstep verdicts"
```

---

### Task 2: Runtime-only component lifecycle

**Files:**
- Create: `TrafficLightsEnhancement/Components/TrafficGroupLockstepDebugState.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs:25-78`
- Modify: `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs`

**Interfaces:**
- Consumes: pure snapshot, pass flag, and sync-disposition types from Task 1.
- Produces: `TrafficGroupLockstepDebugState`
- Produces: `TrafficGroupSystem.MaintainLockstepDiagnosticsComponents(bool)`

- [ ] **Step 1: Write failing lifecycle source tests**

Add tests that extract `OnUpdate` and
`MaintainLockstepDiagnosticsComponents` and assert:

```csharp
Assert.Contains(
    "Mod.m_Setting != null && Mod.m_Setting.m_ShowTransitSignalPriorityDiagnostics",
    onUpdate);
Assert.Contains("EntityManager.AddComponentData", maintenance);
Assert.Contains("EntityManager.RemoveComponent<TrafficGroupLockstepDebugState>",
    maintenance);
Assert.DoesNotContain("ISerializable", debugComponentSource);
```

Also assert maintenance enumerates `m_MemberQuery`, so every member is covered
without depending on UI selection.

- [ ] **Step 2: Run the lifecycle test and verify RED**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest --filter "FullyQualifiedName~Lockstep_diagnostics"
```

Expected: FAIL because the component and maintenance method do not exist.

- [ ] **Step 3: Implement the runtime component**

The component must implement only `IComponentData`, never `ISerializable`:

```csharp
public struct TrafficGroupLockstepDebugState : IComponentData
{
    public uint SimulationFrame;
    public uint MemberUpdateFrame;
    public uint LeaderUpdateFrame;
    public TrafficGroupLockstepPassFlags PassFlags;
    public TrafficGroupLockstepSyncDisposition SyncDisposition;
    public bool IsCoordinated;
    public bool IsGreenWave;
    public bool HasCompleteMapping;
    public byte MappedCurrentGroup;
    public byte MappedNextGroup;
    public TrafficGroupLockstepControllerSnapshot Before;
    public TrafficGroupLockstepControllerSnapshot Master;
    public TrafficGroupLockstepControllerSnapshot After;
    public ulong LaneHashBefore;
    public ulong LaneHashAfter;
    public ulong RenderedHashBefore;
    public ulong RenderedHashAfter;
    public int LaneCount;
    public int RenderedCount;
}
```

- [ ] **Step 4: Implement gated component maintenance**

At the beginning of `TrafficGroupSystem.OnUpdate`, compute the diagnostics
setting once and call:

```csharp
MaintainLockstepDiagnosticsComponents(
    Mod.m_Setting != null
    && Mod.m_Setting.m_ShowTransitSignalPriorityDiagnostics);
```

When enabled, add a zeroed debug component to every member lacking it. When
disabled, remove it from every member that has it. Do not add the component to
synthetic group entities.

- [ ] **Step 5: Run the lifecycle tests and verify GREEN**

Run the Step 2 command.

Expected: all matching ECS source tests pass.

- [ ] **Step 6: Commit the runtime component lifecycle**

```powershell
git add TrafficLightsEnhancement\Components\TrafficGroupLockstepDebugState.cs TrafficLightsEnhancement\Systems\TrafficGroupSystem.cs TrafficLightsEnhancement.Ecs.Tests\TrafficGroupSystemSourceTests.cs
git commit -m "feat: maintain lockstep diagnostic state"
```

---

### Task 3: Instrument every simulation boundary

**Files:**
- Create: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TrafficGroupLockstepRuntimeDiagnostics.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs:84-103,132-173`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs:184-558,1943-1980`
- Modify: `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs`

**Interfaces:**
- Consumes: `TrafficGroupLockstepDebugState`
- Produces: `TrafficGroupLockstepRuntimeDiagnostics.Snapshot(...)`
- Produces: `TrafficGroupLockstepRuntimeDiagnostics.HashLaneSignals(...)`
- Produces: `TrafficGroupLockstepRuntimeDiagnostics.HashRenderedLights(...)`
- Produces: per-pass debug-state updates from `UpdateTrafficLightsJob.Execute`

- [ ] **Step 1: Write failing instrumentation source tests**

Tests must prove the implementation records:

```csharp
Assert.Contains("TrafficGroupLockstepPassFlags.CollectionVisited", execute);
Assert.Contains("TrafficGroupLockstepPassFlags.IndependentDeferred", execute);
Assert.Contains("TrafficGroupLockstepPassFlags.IndependentHeld", execute);
Assert.Contains("TrafficGroupLockstepPassFlags.IndependentAdvanced", execute);
Assert.Contains("TrafficGroupLockstepPassFlags.SynchronizationVisited", execute);
Assert.Contains("TrafficGroupLockstepPassFlags.SynchronizationApplied", execute);
Assert.Contains("TrafficGroupLockstepSyncDisposition.MissingMaster", execute);
Assert.Contains("TrafficGroupLockstepSyncDisposition.InvalidMaster", execute);
Assert.Contains("TrafficGroupLockstepSyncDisposition.IncompleteMapping", execute);
Assert.Contains("HashLaneSignals", execute);
Assert.Contains("HashRenderedLights", execute);
```

The same tests must assert that diagnostic helper calls are guarded by
`m_TransitSignalPriorityDiagnosticsEnabled` and that no new assignment to
`trafficLights`, `LaneSignal`, or `Game.Objects.TrafficLight` occurs inside
`TrafficGroupLockstepRuntimeDiagnostics.cs`.

- [ ] **Step 2: Run the instrumentation tests and verify RED**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest --filter "FullyQualifiedName~Lockstep_diagnostics|FullyQualifiedName~Lockstep_simulation"
```

Expected: FAIL because the helper and pass recording do not exist.

- [ ] **Step 3: Add the writable lookup and runtime helper**

Add to `ExtraTypeHandle`:

```csharp
public ComponentLookup<TrafficGroupLockstepDebugState>
    m_TrafficGroupLockstepDebugState;
```

Acquire it with `isReadOnly: false` and update it in `Update`.

The runtime helper must snapshot controller values without mutating them and
hash, in stable list order:

- lane entity index/version, group mask, flags, signal, default, petitioner,
  blocker, priority, yield mask, and ignore-priority mask;
- rendered object entity index/version, both group masks, and visible state.

Use `TrafficGroupLockstepDiagnostics.AddHash` for every primitive.

- [ ] **Step 4: Record collection and independent-pass outcomes**

At the start of a member iteration, read its debug component only when
diagnostics are enabled and it exists. Reset evidence when the stored simulation
frame differs from the current frame.

In the collection pass, set `CollectionVisited`.

In the independent pass:

- coordinated active followers set `IndependentDeferred`;
- coordinated inactive-shard followers set `IndependentHeld`;
- leaders remain marked as leaders without a follower failure;
- a coordinated follower that reaches either state machine captures `Before`,
  hashes, runs the existing unchanged state machine, captures `After`, and sets
  `IndependentAdvanced` only when the controller snapshot changed.

- [ ] **Step 5: Record synchronization-pass outcomes**

The follower pass must set `SynchronizationVisited` before eligibility checks.
Replace silent `continue` branches with diagnostic dispositions while
preserving the same control flow.

Before calling `SyncSignalGroupWithLeader`, capture follower `Before`, master,
and output hashes. After the existing call and existing output updates, capture
`After` and output hashes and set `SynchronizationApplied`.

Classify each refusal exactly:

- missing map component -> `MissingMapping`;
- incomplete map -> `IncompleteMapping`;
- unmapped current/next master phase -> matching unmapped disposition;
- absent same-tick master -> `MissingMaster`;
- invalid same-tick master -> `InvalidMaster`;
- inactive group -> `InactiveGroup`;
- absent required local demand -> `MissingLocalDemand`.

- [ ] **Step 6: Run instrumentation tests and verify GREEN**

Run the Step 2 command.

Expected: all matching ECS source tests pass.

- [ ] **Step 7: Run the entire ECS test project**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest
```

Expected: all tests pass.

- [ ] **Step 8: Commit simulation instrumentation**

```powershell
git add TrafficLightsEnhancement\Systems\TrafficLightSystems\Simulation\TrafficGroupLockstepRuntimeDiagnostics.cs TrafficLightsEnhancement\Systems\TrafficLightSystems\Simulation\ExtraTypeHandle.cs TrafficLightsEnhancement\Systems\TrafficLightSystems\Simulation\PatchedTrafficLightSystem.cs TrafficLightsEnhancement.Ecs.Tests\TrafficGroupSystemSourceTests.cs
git commit -m "feat: trace every lockstep simulation boundary"
```

---

### Task 4: Emit a complete whole-group trace

**Files:**
- Create: `TrafficLightsEnhancement/Systems/UI/UISystem.TrafficGroupLockstepDiagnostics.cs`
- Modify: `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs:1492-1529`
- Modify: `TrafficLightsEnhancement.Ecs.Tests/UISystemSourceTests.cs`

**Interfaces:**
- Consumes: pure classifier, runtime debug component, runtime hash helper.
- Produces: `UISystem.GetTrafficGroupLockstepTrace(Entity)`
- Produces: `UISystem.GetTrafficGroupLockstepMemberTrace(Entity, Entity)`
- Produces: `UISystem.GetRenderedTrafficLightTrace(Entity)`
- Produces: `UISystem.WarnLockstepVerdictIfChanged(Entity, string)`

- [ ] **Step 1: Write failing whole-group trace source tests**

Add tests proving:

```csharp
Assert.Contains(
    "trafficGroupLockstep = GetTrafficGroupLockstepTrace(entity)",
    writerSource);
Assert.Contains("GetGroupMembers", groupTraceSource);
Assert.Contains("GetTrafficGroupLockstepMemberTrace", groupTraceSource);
Assert.Contains("GetTspLaneSignalTrace", memberTraceSource);
Assert.Contains("GetRenderedTrafficLightTrace", memberTraceSource);
Assert.Contains("TrafficGroupLockstepDiagnostics.Classify", memberTraceSource);
Assert.Contains("TrafficGroupLockstepDebugState", memberTraceSource);
Assert.Contains("m_LockstepVerdictWarnings", warningSource);
Assert.Contains("Mod.log.Warn", warningSource);
```

Also assert the rendered trace includes entity index/version, `m_GroupMask0`,
`m_GroupMask1`, and `m_State`, and the lane trace includes petitioner, blocker,
priority, flags, and default priority.

- [ ] **Step 2: Run the UI source tests and verify RED**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest --filter "FullyQualifiedName~Lockstep_trace"
```

Expected: FAIL because whole-group trace methods do not exist.

- [ ] **Step 3: Build whole-group and member trace methods**

`GetTrafficGroupLockstepTrace` must:

1. return `null` unless the selected entity is a valid group member;
2. obtain the group and all current members;
3. emit group identity, mode, movement-map summary, and every member record;
4. dispose the member collection in `finally`; and
5. never abort the full group because one member is stale.

Each member record must include:

- identity and role;
- update-frame identity;
- live controller state;
- live leader and stored master state;
- runtime debug component or an explicit absence marker;
- map completeness and mapped current/next phases;
- full lane trace;
- full rendered-object trace;
- live deterministic hashes/counts;
- pure classifier verdict name and reason.

- [ ] **Step 4: Expand lane details and rendered-object details**

Extend the existing lane trace object with:

```csharp
flags = laneSignal.m_Flags.ToString(),
petitioner = FormatEntity(laneSignal.m_Petitioner),
blocker = FormatEntity(laneSignal.m_Blocker),
priority = laneSignal.m_Priority,
defaultPriority = laneSignal.m_Default,
```

Enumerate `Game.Objects.SubObject` for each member and, when the subobject has a
`Game.Objects.TrafficLight`, serialize:

```csharp
new
{
    entity = FormatEntity(subObject.m_SubObject),
    groupMask0 = rendered.m_GroupMask0,
    groupMask1 = rendered.m_GroupMask1,
    state = rendered.m_State.ToString(),
}
```

- [ ] **Step 5: Add debounced warnings**

Maintain:

```csharp
private readonly Dictionary<Entity, string> m_LockstepVerdictWarnings = new();
```

Key the signature by verdict, reason, simulation/pass frame, and relevant
before/after/live values. Log only when a non-healthy signature differs from the
last signature for that member. Remove the cached signature after the member
returns to `InSync`.

- [ ] **Step 6: Attach the whole-group object to existing JSONL**

Add exactly:

```csharp
trafficGroupLockstep = GetTrafficGroupLockstepTrace(entity),
```

beside the existing `trafficGroup` and `leaderTrafficLights` fields. Reuse the
existing file, cadence, lock, rotation, and exception handling.

- [ ] **Step 7: Run UI source tests and verify GREEN**

Run the Step 2 command.

Expected: all matching UI source tests pass.

- [ ] **Step 8: Run both focused test projects**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj -p:LangVersion=latest
dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest
```

Expected: both projects pass.

- [ ] **Step 9: Commit whole-group diagnostics**

```powershell
git add TrafficLightsEnhancement\Systems\UI\UISystem.TrafficGroupLockstepDiagnostics.cs TrafficLightsEnhancement\Systems\UI\UISystem.UIBIndings.cs TrafficLightsEnhancement.Ecs.Tests\UISystemSourceTests.cs
git commit -m "feat: emit complete lockstep group traces"
```

---

### Task 5: Version, verify, build, and install the diagnostic patch

**Files:**
- Modify: `TrafficLightsEnhancement/TrafficLightsEnhancement.csproj:11-12`
- Modify: `TrafficLightsEnhancement/UI/mod.json:6`
- Modify: `TrafficLightsEnhancement.Tests/Compatibility/ReleaseVersionTests.cs:9`

**Interfaces:**
- Consumes: all diagnostics from Tasks 1-4.
- Produces: installed TLE Extended `1.0.7` diagnostic build.

- [ ] **Step 1: Write the failing release-version expectation**

Change only:

```csharp
private const string ExpectedSemanticVersion = "1.0.7";
```

- [ ] **Step 2: Run the version test and verify RED**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj -p:LangVersion=latest --filter FullyQualifiedName~ReleaseVersionTests
```

Expected: FAIL because project and UI metadata still report `1.0.6`.

- [ ] **Step 3: Bump authoritative project and UI versions**

Set:

```xml
<Version>1.0.7.0</Version>
<InformationalVersion>1.0.7.0</InformationalVersion>
```

and:

```json
"version": "1.0.7"
```

- [ ] **Step 4: Run the version test and verify GREEN**

Run the Step 2 command.

Expected: `ReleaseVersionTests` pass.

- [ ] **Step 5: Run all .NET test projects**

Run:

```powershell
dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj -p:LangVersion=latest
dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest
dotnet test TrafficLightsEnhancement.Serialization.Tests\TrafficLightsEnhancement.Serialization.Tests.csproj -p:LangVersion=latest
```

Expected: all projects pass with no failures.

- [ ] **Step 6: Confirm Cities: Skylines II is closed**

Run:

```powershell
Get-Process 'Cities2' -ErrorAction SilentlyContinue
```

Expected: no process output. If the game is running, stop before the Release
build and ask the user to close it.

- [ ] **Step 7: Build and install Release**

Run:

```powershell
dotnet build Cities2-TrafficLightsEnhancement.sln --configuration Release -p:LangVersion=latest
```

Expected: build succeeds and the normal mod post-processor installs to
`C:\Users\matt\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\TrafficLightsEnhancementExtended`.

- [ ] **Step 8: Verify installed identity and hash**

Compare the built/staged and installed DLLs:

```powershell
Get-FileHash -Algorithm SHA256 `
  '.artifacts\staged-localmods\TrafficLightsEnhancementExtended\C2VM.TrafficLightsEnhancement.dll'
Get-FileHash -Algorithm SHA256 `
  "$env:USERPROFILE\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\TrafficLightsEnhancementExtended\C2VM.TrafficLightsEnhancement.dll"
```

Expected: identical SHA256 hashes. Verify assembly and UI metadata report
`1.0.7`.

- [ ] **Step 9: Check repository hygiene and commit**

Run:

```powershell
git diff --check
git status --short
```

Stage only source, tests, and authoritative metadata. Do not commit generated
build artifacts.

```powershell
git add TrafficLightsEnhancement\TrafficLightsEnhancement.csproj TrafficLightsEnhancement\UI\mod.json TrafficLightsEnhancement.Tests\Compatibility\ReleaseVersionTests.cs
git commit -m "chore: version lockstep diagnostic build 1.0.7"
```

- [ ] **Step 10: Hand off the single diagnostic playtest**

Ask the user to:

1. launch the verified 1.0.7 build;
2. enable diagnostics;
3. load the existing reproduction save;
4. select any member of the affected lockstep group;
5. observe at least two complete leader cycles and one visible drift event; and
6. exit normally.

After exit, inspect the JSONL trace, TLE mod log, `Player.log`, and loaded
version. The first non-healthy verdict identifies the boundary for the separate
test-first gameplay fix.
