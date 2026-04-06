# Tram-Only TSP Approach Index Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace broken lane-object tram early detection with a tram-runtime lane index that can switch TSP before the tram reaches the stop line while preserving petitioner fallback and the already-fixed request lifecycle behavior.

**Architecture:** Build one temporary tram-approach index per `PatchedTrafficLightSystem` update, pass it into `UpdateTrafficLightsJob` as a read-only native map, and have `TransitSignalPriorityRuntime` resolve early track requests from approach/upstream lane lookups instead of `LaneObject` buffers. Keep buses on petitioner fallback only, and keep the panel/debug logging aligned with the new tram-index states.

**Tech Stack:** C# 12, .NET 8, Unity ECS (`EntityQuery`, `ComponentLookup`, `NativeParallelHashMap`), xUnit, PowerShell build/deploy scripts

---

## File Structure

- Modify: `TrafficLightsEnhancement.Logic/Tsp/EarlyApproachDetection.cs`
  Responsibility: own the pure track-index selection helper that chooses `NoTramSamples`, `BelowThreshold`, `MatchOnApproachLane`, or `MatchOnUpstreamLane`.
- Modify: `TrafficLightsEnhancement.Tests/Tsp/TspEarlyDetectionTests.cs`
  Responsibility: prove the pure lane-sample selection logic and the existing approach-lane fallback rules.
- Create: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TramApproachIndex.cs`
  Responsibility: synchronously collect moving, unsuppressed tram samples into a temporary `NativeParallelHashMap<Entity, float>` keyed by lane.
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs`
  Responsibility: cache the tram query, build the temporary index before scheduling `UpdateTrafficLightsJob`, pass the map into the job, and dispose it with the returned dependency.
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs`
  Responsibility: remove track `LaneObject` probing, evaluate approach/upstream tram-index samples, keep petitioner fallback, and preserve target-group behavior.
- Modify: `TrafficLightsEnhancement/Components/TransitSignalPriorityRuntimeDebugInfo.cs`
  Responsibility: narrow track probe enum values to the tram-index states used by the UI and diagnostics.
- Modify: `TrafficLightsEnhancement/Systems/TransitSignalPriorityDiagnosticsSystem.cs`
  Responsibility: log the new tram-index probe states instead of the old lane-object probe strings.
- Modify: `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
  Responsibility: surface the new tram-index probe states in the TSP panel.
- Delete: `TrafficLightsEnhancement.Logic/Tsp/TransitApproachEntityResolver.cs`
  Responsibility: remove dead owner-chain lane-object resolver logic after track early detection stops using `LaneObject` buffers.
- Delete: `TrafficLightsEnhancement.Tests/Tsp/TransitApproachEntityResolverTests.cs`
  Responsibility: remove tests that only existed for the deleted resolver.

### Task 1: Lock Down Pure Indexed Track Selection

**Files:**
- Modify: `TrafficLightsEnhancement.Logic/Tsp/EarlyApproachDetection.cs`
- Modify: `TrafficLightsEnhancement.Tests/Tsp/TspEarlyDetectionTests.cs`

- [ ] **Step 1: Write the failing tests for indexed tram-lane selection**

```csharp
[Fact]
public void Indexed_track_detection_prefers_approach_lane_before_upstream_lane()
{
    var match = EarlyApproachDetection.EvaluateIndexedTrackTramSamples(
        hasApproachSample: true,
        approachCurvePosition: 0.35f,
        hasUpstreamSample: true,
        upstreamCurvePosition: 0.95f,
        approachLaneThreshold: 0.2f,
        upstreamLaneThreshold: 0.9f);

    Assert.Equal(IndexedTrackProbeMatch.MatchOnApproachLane, match);
}

[Fact]
public void Indexed_track_detection_reports_below_threshold_when_samples_exist_but_are_too_early()
{
    var match = EarlyApproachDetection.EvaluateIndexedTrackTramSamples(
        hasApproachSample: true,
        approachCurvePosition: 0.1f,
        hasUpstreamSample: true,
        upstreamCurvePosition: 0.6f,
        approachLaneThreshold: 0.2f,
        upstreamLaneThreshold: 0.9f);

    Assert.Equal(IndexedTrackProbeMatch.BelowThreshold, match);
}

[Fact]
public void Indexed_track_detection_reports_no_samples_when_index_is_empty()
{
    var match = EarlyApproachDetection.EvaluateIndexedTrackTramSamples(
        hasApproachSample: false,
        approachCurvePosition: 0f,
        hasUpstreamSample: false,
        upstreamCurvePosition: 0f,
        approachLaneThreshold: 0.2f,
        upstreamLaneThreshold: 0.9f);

    Assert.Equal(IndexedTrackProbeMatch.NoTramSamples, match);
}
```

- [ ] **Step 2: Run the targeted tests and verify they fail**

Run: `dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj --filter TspEarlyDetectionTests`

Expected: `FAIL` with a compile error similar to `EarlyApproachDetection does not contain a definition for 'EvaluateIndexedTrackTramSamples'` and `The name 'IndexedTrackProbeMatch' does not exist in the current context`.

- [ ] **Step 3: Add the minimal pure helper and enum**

```csharp
public enum IndexedTrackProbeMatch : byte
{
    None = 0,
    NoTramSamples = 1,
    BelowThreshold = 2,
    MatchOnApproachLane = 3,
    MatchOnUpstreamLane = 4,
}

public static IndexedTrackProbeMatch EvaluateIndexedTrackTramSamples(
    bool hasApproachSample,
    float approachCurvePosition,
    bool hasUpstreamSample,
    float upstreamCurvePosition,
    float approachLaneThreshold,
    float upstreamLaneThreshold)
{
    if (hasApproachSample && approachCurvePosition >= approachLaneThreshold)
    {
        return IndexedTrackProbeMatch.MatchOnApproachLane;
    }

    if (hasUpstreamSample && upstreamCurvePosition >= upstreamLaneThreshold)
    {
        return IndexedTrackProbeMatch.MatchOnUpstreamLane;
    }

    if (hasApproachSample || hasUpstreamSample)
    {
        return IndexedTrackProbeMatch.BelowThreshold;
    }

    return IndexedTrackProbeMatch.NoTramSamples;
}
```

- [ ] **Step 4: Re-run the targeted tests and verify they pass**

Run: `dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj --filter TspEarlyDetectionTests`

Expected: `PASS` and the new indexed-sample tests appear alongside the existing approach-lane and petitioner-fallback tests.

- [ ] **Step 5: Commit the pure helper/test slice**

```bash
git add TrafficLightsEnhancement.Logic/Tsp/EarlyApproachDetection.cs TrafficLightsEnhancement.Tests/Tsp/TspEarlyDetectionTests.cs
git commit -m "test: lock down indexed tram approach selection"
```

### Task 2: Build and Wire the Per-Update Tram Approach Index

**Files:**
- Create: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TramApproachIndex.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs`

- [ ] **Step 1: Add the compile-first seam in `PatchedTrafficLightSystem`**

```csharp
private EntityQuery m_TrafficLightQuery;
private EntityQuery m_RailTransitQuery;
```

```csharp
[ReadOnly]
public NativeParallelHashMap<Entity, float>.ReadOnly m_TramApproachIndex;
```

```csharp
m_RailTransitQuery = GetEntityQuery(
    ComponentType.ReadOnly<PublicTransport>(),
    ComponentType.ReadOnly<TrainNavigation>(),
    ComponentType.ReadOnly<TrainCurrentLane>(),
    ComponentType.Exclude<Deleted>(),
    ComponentType.Exclude<Destroyed>(),
    ComponentType.Exclude<Temp>());
```

```csharp
var updatedExtraTypeHandle = m_ExtraTypeHandle.Update(ref base.CheckedStateRef);
var tramApproachIndex = TramApproachIndex.Build(
    m_RailTransitQuery,
    updatedExtraTypeHandle,
    Allocator.TempJob);

JobHandle dependency = JobChunkExtensions.ScheduleParallel(new UpdateTrafficLightsJob
{
    m_TramApproachIndex = tramApproachIndex.AsReadOnly(),
    m_ExtraTypeHandle = updatedExtraTypeHandle,
}, m_TrafficLightQuery, base.Dependency);

base.Dependency = tramApproachIndex.Dispose(dependency);
```

- [ ] **Step 2: Run a verification build and confirm the missing collector fails the build**

Run: `powershell -ExecutionPolicy Bypass -File .\scripts\Verify-Mod.ps1`

Expected: `Build FAILED` with an error similar to `The name 'TramApproachIndex' does not exist in the current context`.

- [ ] **Step 3: Create the synchronous collector**

```csharp
using C2VM.TrafficLightsEnhancement.Components;
using Game.Prefabs;
using Game.Vehicles;
using TrafficLightsEnhancement.Logic.Tsp;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace C2VM.TrafficLightsEnhancement.Systems.TrafficLightSystems.Simulation;

internal static class TramApproachIndex
{
    private const float MovingTrainSpeedThreshold = 0.5f;

    public static NativeParallelHashMap<Entity, float> Build(
        EntityQuery railTransitQuery,
        ExtraTypeHandle extraTypeHandle,
        Allocator allocator)
    {
        int capacity = math.max(1, railTransitQuery.CalculateEntityCount() * 2);
        var index = new NativeParallelHashMap<Entity, float>(capacity, allocator);

        using NativeArray<Entity> railTransitEntities = railTransitQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < railTransitEntities.Length; i++)
        {
            Entity vehicleEntity = railTransitEntities[i];
            if (!extraTypeHandle.m_PublicTransport.TryGetComponent(vehicleEntity, out var publicTransport)
                || !extraTypeHandle.m_TrainNavigation.TryGetComponent(vehicleEntity, out var trainNavigation)
                || !extraTypeHandle.m_TrainCurrentLane.TryGetComponent(vehicleEntity, out var trainCurrentLane))
            {
                continue;
            }

            TransitApproachSuppressionFlags suppressionFlags =
                GetSuppressionFlags(publicTransport.m_State);

            if (!EarlyApproachDetection.IsMovingEligibleApproachState(
                    isEligibleLane: true,
                    isVehicleMoving: trainNavigation.m_Speed > MovingTrainSpeedThreshold,
                    suppressionFlags))
            {
                continue;
            }

            TryRecordLaneSample(index, trainCurrentLane.m_Front.m_Lane, trainCurrentLane.m_Front.m_CurvePosition.x, extraTypeHandle);
            TryRecordLaneSample(index, trainCurrentLane.m_Rear.m_Lane, trainCurrentLane.m_Rear.m_CurvePosition.x, extraTypeHandle);
        }

        return index;
    }

    private static void TryRecordLaneSample(
        NativeParallelHashMap<Entity, float> index,
        Entity laneEntity,
        float curvePosition,
        ExtraTypeHandle extraTypeHandle)
    {
        if (laneEntity == Entity.Null || !IsTramTrackLane(extraTypeHandle, laneEntity))
        {
            return;
        }

        if (index.TryGetValue(laneEntity, out float existingCurvePosition) && existingCurvePosition >= curvePosition)
        {
            return;
        }

        index[laneEntity] = curvePosition;
    }

    private static bool IsTramTrackLane(ExtraTypeHandle extraTypeHandle, Entity laneEntity)
    {
        if (!extraTypeHandle.m_TrackLane.HasComponent(laneEntity))
        {
            return false;
        }

        if (!extraTypeHandle.m_PrefabRef.TryGetComponent(laneEntity, out var prefabRef))
        {
            return false;
        }

        return extraTypeHandle.m_TrackLaneData.TryGetComponent(prefabRef.m_Prefab, out var trackLaneData)
            && (trackLaneData.m_TrackTypes & TrackTypes.Tram) != 0;
    }

    private static TransitApproachSuppressionFlags GetSuppressionFlags(PublicTransportFlags state)
    {
        TransitApproachSuppressionFlags flags = TransitApproachSuppressionFlags.None;

        if ((state & PublicTransportFlags.Boarding) != 0) flags |= TransitApproachSuppressionFlags.Boarding;
        if ((state & PublicTransportFlags.Arriving) != 0) flags |= TransitApproachSuppressionFlags.Arriving;
        if ((state & PublicTransportFlags.RequireStop) != 0) flags |= TransitApproachSuppressionFlags.RequireStop;

        return flags;
    }
}
```

- [ ] **Step 4: Re-run the verification build and confirm the job wiring is valid**

Run: `powershell -ExecutionPolicy Bypass -File .\scripts\Verify-Mod.ps1`

Expected: `Build succeeded.` followed by the script warning that this is a verification build only and not a playable deploy.

- [ ] **Step 5: Commit the collector/wiring slice**

```bash
git add TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TramApproachIndex.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs
git commit -m "feat: add per-update tram approach index"
```

### Task 3: Replace Track Lane-Object Probing With Tram-Index Lookups

**Files:**
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs`

- [ ] **Step 1: Write the compile-first seam for the index-based track path**

```csharp
TrackProbeSnapshot signaledProbe = ProbeIndexedTrackLane(
    job.m_TramApproachIndex,
    signaledLaneEntity,
    TramApproachLaneCurveThreshold,
    isUpstreamLane: false);

TrackProbeSnapshot approachProbe = signaledLaneEntity == approachLaneEntity
    ? signaledProbe
    : ProbeIndexedTrackLane(
        job.m_TramApproachIndex,
        approachLaneEntity,
        TramApproachLaneCurveThreshold,
        isUpstreamLane: false);

Entity upstreamLaneEntity = TryResolveImmediateUpstreamTramLane(job, approachLaneEntity);
TrackProbeSnapshot upstreamProbe = upstreamLaneEntity == Entity.Null
    ? default
    : ProbeIndexedTrackLane(
        job.m_TramApproachIndex,
        upstreamLaneEntity,
        TramUpstreamLaneCurveThreshold,
        isUpstreamLane: true);

IndexedTrackProbeMatch indexedMatch = EarlyApproachDetection.EvaluateIndexedTrackTramSamples(
    approachProbe.HasSample,
    approachProbe.CurvePosition,
    upstreamProbe.HasSample,
    upstreamProbe.CurvePosition,
    TramApproachLaneCurveThreshold,
    TramUpstreamLaneCurveThreshold);
```

- [ ] **Step 2: Run a verification build and confirm the missing helpers fail the build**

Run: `powershell -ExecutionPolicy Bypass -File .\scripts\Verify-Mod.ps1`

Expected: `Build FAILED` with errors similar to `The type or namespace name 'TrackProbeSnapshot' could not be found` and `The name 'ProbeIndexedTrackLane' does not exist in the current context`.

- [ ] **Step 3: Implement the runtime switch to the tram index**

```csharp
private readonly struct TrackProbeSnapshot
{
    public TrackProbeSnapshot(bool hasSample, float curvePosition, TransitSignalPriorityTrackProbeResult result)
    {
        HasSample = hasSample;
        CurvePosition = curvePosition;
        Result = result;
    }

    public bool HasSample { get; }
    public float CurvePosition { get; }
    public TransitSignalPriorityTrackProbeResult Result { get; }
}

private static TrackProbeSnapshot ProbeIndexedTrackLane(
    NativeParallelHashMap<Entity, float>.ReadOnly tramApproachIndex,
    Entity laneEntity,
    float threshold,
    bool isUpstreamLane)
{
    if (laneEntity == Entity.Null || !tramApproachIndex.TryGetValue(laneEntity, out float curvePosition))
    {
        return new TrackProbeSnapshot(false, 0f, TransitSignalPriorityTrackProbeResult.NoTramSamples);
    }

    if (curvePosition < threshold)
    {
        return new TrackProbeSnapshot(true, curvePosition, TransitSignalPriorityTrackProbeResult.BelowThreshold);
    }

    return new TrackProbeSnapshot(
        true,
        curvePosition,
        isUpstreamLane
            ? TransitSignalPriorityTrackProbeResult.MatchOnUpstreamLane
            : TransitSignalPriorityTrackProbeResult.MatchOnApproachLane);
}
```

Also remove the now-dead track-only pieces from this file:

```csharp
// delete these old lane-object track helpers once the new path compiles cleanly:
// - TrackLaneProbeOutcome
// - ProbeTrackLane
// - EvaluateTrackLaneObject
// - IsMovingTrackVehicle
```

- [ ] **Step 4: Re-run verification and targeted tests**

Run: `powershell -ExecutionPolicy Bypass -File .\scripts\Verify-Mod.ps1`

Expected: `Build succeeded.`

Run: `dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj --filter "TspEarlyDetectionTests|TspDecisionEngineTests|TspPolicyTests"`

Expected: `PASS` and the request-lifecycle tests still prove no-grace decay and the effective short horizon remain unchanged.

- [ ] **Step 5: Commit the runtime rewrite**

```bash
git add TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs
git commit -m "feat: use tram approach index for track TSP"
```

### Task 4: Align Diagnostics and Delete the Dead Lane-Object Resolver

**Files:**
- Modify: `TrafficLightsEnhancement/Components/TransitSignalPriorityRuntimeDebugInfo.cs`
- Modify: `TrafficLightsEnhancement/Systems/TransitSignalPriorityDiagnosticsSystem.cs`
- Modify: `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
- Delete: `TrafficLightsEnhancement.Logic/Tsp/TransitApproachEntityResolver.cs`
- Delete: `TrafficLightsEnhancement.Tests/Tsp/TransitApproachEntityResolverTests.cs`

- [ ] **Step 1: Narrow the track probe enum to the tram-index states**

```csharp
public enum TransitSignalPriorityTrackProbeResult : byte
{
    None = 0,
    NoTramSamples = 1,
    BelowThreshold = 2,
    MatchOnApproachLane = 3,
    MatchOnUpstreamLane = 4,
}
```

- [ ] **Step 2: Update diagnostics and UI formatting to match the new enum**

```csharp
private static string FormatTrackProbe(byte probe)
{
    return (TransitSignalPriorityTrackProbeResult)probe switch
    {
        TransitSignalPriorityTrackProbeResult.NoTramSamples => "no-tram-samples",
        TransitSignalPriorityTrackProbeResult.BelowThreshold => "below-threshold",
        TransitSignalPriorityTrackProbeResult.MatchOnApproachLane => "match-approach",
        TransitSignalPriorityTrackProbeResult.MatchOnUpstreamLane => "match-upstream",
        _ => "none",
    };
}
```

- [ ] **Step 3: Remove the dead resolver and its test file**

```bash
git rm TrafficLightsEnhancement.Logic/Tsp/TransitApproachEntityResolver.cs TrafficLightsEnhancement.Tests/Tsp/TransitApproachEntityResolverTests.cs
```

- [ ] **Step 4: Run the verification build and the focused test suite**

Run: `powershell -ExecutionPolicy Bypass -File .\scripts\Verify-Mod.ps1`

Expected: `Build succeeded.`

Run: `dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj --filter "TspEarlyDetectionTests|TspDecisionEngineTests|TspPolicyTests|TspStatusFormatterTests"`

Expected: `PASS` and the updated debug strings compile cleanly through the UI/diagnostics layers.

- [ ] **Step 5: Commit diagnostics cleanup**

```bash
git add TrafficLightsEnhancement/Components/TransitSignalPriorityRuntimeDebugInfo.cs TrafficLightsEnhancement/Systems/TransitSignalPriorityDiagnosticsSystem.cs TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs
git commit -m "refactor: align TSP diagnostics with tram index"
```

### Task 5: Final Verification, Deploy, and Repro

**Files:**
- Modify: none
- Deploy: `scripts/Verify-Mod.ps1`
- Deploy: `scripts/Deploy-Mod.ps1`

- [ ] **Step 1: Run the full automated verification set**

Run: `dotnet test TrafficLightsEnhancement.Tests\TrafficLightsEnhancement.Tests.csproj --filter "TspEarlyDetectionTests|TspDecisionEngineTests|TspPolicyTests|TspStatusFormatterTests|MainPanelRefreshPolicyTests"`

Expected: `PASS` for all selected tests.

Run: `powershell -ExecutionPolicy Bypass -File .\scripts\Verify-Mod.ps1`

Expected: `Build succeeded.` and the warning that this is not a playable deploy.

- [ ] **Step 2: Build and deploy the playable mod with the game closed**

Run: `powershell -ExecutionPolicy Bypass -File .\scripts\Deploy-Mod.ps1`

Expected: `Playable mod deployed to C:\Users\Shadow\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\C2VM.TrafficLightsEnhancement` and confirmation that the managed, native, and UI artifacts were found.

- [ ] **Step 3: Perform the manual tram-loop repro**

Manual checklist:
- open the single-intersection tram test city
- enable TSP on the tram crossing
- watch the first approach and confirm `Debug Source` becomes `Fresh early request` before the stop line
- confirm `Debug State` shows tram-index results such as `track[signal=no-tram-samples, approach=match-approach, upstream=no-tram-samples]` or `track[signal=no-tram-samples, approach=below-threshold, upstream=match-upstream]` instead of `no-objects`
- confirm the tram does not come to a complete stop before the light changes
- confirm the request still clears after the tram passes and roadway traffic resumes

- [ ] **Step 4: Capture any remaining mismatch before changing code again**

If the tram still stops, pause once and record:

```text
Debug Source:
Debug State:
Tram position relative to the stop line:
Whether the tram was on the approach lane or upstream lane:
```

Expected: one screenshot or note that points to a specific threshold or lane-resolution mismatch, not another guess-driven fix.

- [ ] **Step 5: Confirm the branch is ready for the execution handoff or final ship commit**

```bash
git status
git log --oneline -5
```

Expected: the working tree is clean or contains only the exact files from this plan, and the per-task commits are present before any squash, merge, or push step.
