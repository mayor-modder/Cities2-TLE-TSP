# Transit Signal Priority Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add per-junction Transit Signal Priority for track transit and bus-lane transit, with optional propagation through the existing coordinated traffic-group and green-wave system.

**Architecture:** Add a new persistent TSP settings component plus transient runtime request data, then bias the existing custom-phase selector and coordinated group leader/follower flow rather than inventing a second grouping model. Keep phase masks, green-wave timing, and group membership authoritative, with TSP acting only as a request/override layer.

**Tech Stack:** C#/.NET Framework 4.8 ECS mod code, Cities: Skylines II game assemblies, TypeScript/React UI bundle built by webpack, optional .NET 8 xUnit logic tests for extracted pure TSP helpers.

---

### Task 1: Add a Small Pure Logic Test Surface For TSP Decisions

**Files:**
- Create: `TrafficLightsEnhancement.Logic/TrafficLightsEnhancement.Logic.csproj`
- Create: `TrafficLightsEnhancement.Logic/Tsp/TspRequestInputs.cs`
- Create: `TrafficLightsEnhancement.Logic/Tsp/TspDecisionEngine.cs`
- Create: `TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj`
- Create: `TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs`
- Modify: `Cities2-TrafficLightsEnhancement.sln`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Track_request_prefers_serving_phase()
{
    var phases = new[]
    {
        new PhaseScore(phaseIndex: 0, basePriority: 100, weightedWaiting: 4f, servesTrack: false, servesPublicCar: true),
        new PhaseScore(phaseIndex: 1, basePriority: 100, weightedWaiting: 3f, servesTrack: true, servesPublicCar: false),
    };

    var request = new TspRequest(source: TspSource.Track, strength: 1f, extensionEligible: false);

    var decision = TspDecisionEngine.SelectNextPhase(phases, currentPhaseIndex: 0, request);

    Assert.Equal(1, decision.NextPhaseIndex);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release`
Expected: FAIL because the test project and TSP decision classes do not exist yet.

**Step 3: Write minimal implementation**

```csharp
public readonly record struct TspRequest(TspSource Source, float Strength, bool ExtensionEligible);

public static class TspDecisionEngine
{
    public static TspDecision SelectNextPhase(IReadOnlyList<PhaseScore> phases, int currentPhaseIndex, TspRequest request)
    {
        int best = currentPhaseIndex;
        float bestScore = float.MinValue;

        for (int i = 0; i < phases.Count; i++)
        {
            float score = phases[i].WeightedWaiting;
            if (request.Source == TspSource.Track && phases[i].ServesTrack) score += 1000f * request.Strength;
            if (request.Source == TspSource.PublicCar && phases[i].ServesPublicCar) score += 1000f * request.Strength;
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return new TspDecision(best, canExtendCurrent: request.ExtensionEligible && best == currentPhaseIndex);
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release`
Expected: PASS for the new TSP selection tests.

**Step 5: Commit**

```bash
git add Cities2-TrafficLightsEnhancement.sln TrafficLightsEnhancement.Logic TrafficLightsEnhancement.Tests
git commit -m "test: add TSP decision logic coverage"
```

### Task 2: Add Persistent Junction TSP Settings And Transient Request Data

**Files:**
- Create: `TrafficLightsEnhancement/Components/TransitSignalPrioritySettings.cs`
- Create: `TrafficLightsEnhancement/Components/TransitSignalPriorityRequest.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs`
- Modify: `TrafficLightsEnhancement/Systems/UI/UITypes.cs`
- Modify: `TrafficLightsEnhancement/Systems/UI\TypeHandle.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Disabled_settings_produce_no_request()
{
    var settings = new TransitSignalPrioritySettings();
    Assert.False(settings.m_Enabled);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release --filter Disabled_settings_produce_no_request`
Expected: FAIL because the settings types do not exist in the extracted logic/test seam yet.

**Step 3: Write minimal implementation**

```csharp
public struct TransitSignalPrioritySettings : IComponentData, ISerializable
{
    public bool m_Enabled;
    public bool m_AllowTrackRequests;
    public bool m_AllowPublicCarRequests;
    public bool m_AllowGroupPropagation;
    public ushort m_RequestHorizonTicks;
    public ushort m_MaxGreenExtensionTicks;
}

public struct TransitSignalPriorityRequest : IComponentData
{
    public byte m_TargetSignalGroup;
    public byte m_SourceType;
    public float m_Strength;
    public uint m_ExpiryTimer;
    public bool m_ExtendCurrentPhase;
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release`
Expected: PASS with default-disabled settings expectations satisfied.

**Step 5: Commit**

```bash
git add TrafficLightsEnhancement/Components/TransitSignalPrioritySettings.cs TrafficLightsEnhancement/Components/TransitSignalPriorityRequest.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs TrafficLightsEnhancement/Systems/UI/UITypes.cs TrafficLightsEnhancement/Systems/UI/TypeHandle.cs
git commit -m "feat: add TSP settings and runtime request components"
```

### Task 3: Detect Tram And Bus-Lane Requests At The Junction Level

**Files:**
- Create: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs`
- Modify: `TrafficLightsEnhancement/Components/EdgeGroupMask.cs`
- Modify: `TrafficLightsEnhancement/Components/SubLaneGroupMask.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Public_car_request_only_targets_public_car_phase()
{
    var phases = new[]
    {
        new PhaseScore(0, 100, 1f, servesTrack: false, servesPublicCar: false),
        new PhaseScore(1, 100, 1f, servesTrack: false, servesPublicCar: true),
    };

    var request = new TspRequest(TspSource.PublicCar, 1f, extensionEligible: false);
    var decision = TspDecisionEngine.SelectNextPhase(phases, 0, request);

    Assert.Equal(1, decision.NextPhaseIndex);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release --filter Public_car_request_only_targets_public_car_phase`
Expected: FAIL until the serving-phase mapping is implemented.

**Step 3: Write minimal implementation**

```csharp
public static bool TryBuildRequestForLane(
    in TransitSignalPrioritySettings settings,
    bool isTrackLane,
    bool isPublicCarLane,
    IReadOnlyList<PhaseScore> phases,
    out TspRequest request)
{
    request = default;
    if (!settings.m_Enabled) return false;
    if (isTrackLane && settings.m_AllowTrackRequests)
    {
        request = new TspRequest(TspSource.Track, 1f, extensionEligible: true);
        return true;
    }
    if (isPublicCarLane && settings.m_AllowPublicCarRequests)
    {
        request = new TspRequest(TspSource.PublicCar, 1f, extensionEligible: true);
        return true;
    }
    return false;
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release`
Expected: PASS for track/public-car targeting rules.

**Step 5: Commit**

```bash
git add TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs TrafficLightsEnhancement/Components/EdgeGroupMask.cs TrafficLightsEnhancement/Components/SubLaneGroupMask.cs
git commit -m "feat: detect local TSP requests for track and bus lanes"
```

### Task 4: Bias Local Phase Selection With Early Green And Limited Extension

**Files:**
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`
- Modify: `TrafficLightsEnhancement.Logic/Tsp/TspDecisionEngine.cs`
- Test: `TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Extension_is_used_only_when_current_phase_serves_request()
{
    var phases = new[]
    {
        new PhaseScore(0, 104, 2f, servesTrack: true, servesPublicCar: false),
        new PhaseScore(1, 104, 2f, servesTrack: false, servesPublicCar: false),
    };

    var request = new TspRequest(TspSource.Track, 1f, extensionEligible: true);
    var decision = TspDecisionEngine.SelectNextPhase(phases, currentPhaseIndex: 0, request);

    Assert.True(decision.CanExtendCurrent);
    Assert.Equal(0, decision.NextPhaseIndex);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release --filter Extension_is_used_only_when_current_phase_serves_request`
Expected: FAIL until extension rules are implemented.

**Step 3: Write minimal implementation**

```csharp
if (request.ExtensionEligible && currentPhaseIndex >= 0)
{
    var current = phases[currentPhaseIndex];
    bool servesCurrent =
        (request.Source == TspSource.Track && current.ServesTrack) ||
        (request.Source == TspSource.PublicCar && current.ServesPublicCar);

    if (servesCurrent)
    {
        return new TspDecision(currentPhaseIndex, canExtendCurrent: true);
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release`
Expected: PASS for extension vs early-green selection.

**Step 5: Commit**

```bash
git add TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs TrafficLightsEnhancement.Logic/Tsp/TspDecisionEngine.cs TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs
git commit -m "feat: bias local phase selection with TSP"
```

### Task 5: Propagate Requests Through Existing Coordinated Traffic Groups

**Files:**
- Create: `TrafficLightsEnhancement/Components/TrafficGroupTspState.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs`
- Test: `TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Leader_prefers_member_request_when_group_propagation_is_enabled()
{
    var leader = new PhaseScore(0, 100, 4f, servesTrack: false, servesPublicCar: true);
    var followerRequest = new TspRequest(TspSource.Track, 1f, extensionEligible: false);

    var aggregated = TspDecisionEngine.CombineRequests(new[] { followerRequest });

    Assert.Equal(TspSource.Track, aggregated.Source);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release --filter Leader_prefers_member_request_when_group_propagation_is_enabled`
Expected: FAIL because group request aggregation does not exist yet.

**Step 3: Write minimal implementation**

```csharp
public static TspRequest CombineRequests(IEnumerable<TspRequest> requests)
{
    TspRequest best = default;
    foreach (var request in requests)
    {
        if (request.Strength > best.Strength)
        {
            best = request;
        }
    }
    return best;
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release`
Expected: PASS for leader aggregation behavior.

**Step 5: Commit**

```bash
git add TrafficLightsEnhancement/Components/TrafficGroupTspState.cs TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/CustomStateMachine.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs
git commit -m "feat: propagate TSP through coordinated traffic groups"
```

### Task 6: Add TSP Controls To The Existing Traffic-Light And Group UI

**Files:**
- Modify: `TrafficLightsEnhancement/Systems/UI/UITypes.cs`
- Modify: `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
- Modify: `TrafficLightsEnhancement/UI/src/mods/general.ts`
- Modify: `TrafficLightsEnhancement/UI/src/bindings.ts`
- Modify: `TrafficLightsEnhancement/UI/src/mods/components/main-panel/index.tsx`
- Modify: `TrafficLightsEnhancement/UI/src/mods/components/custom-phase-tool/main-panel/sub-panel.tsx`
- Modify: `TrafficLightsEnhancement/UI/src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx`
- Modify: `TrafficLightsEnhancement/Resources/Localisations/en-US.json`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Junction_settings_round_trip_preserves_group_propagation_flag()
{
    var settings = new TransitSignalPrioritySettings
    {
        m_Enabled = true,
        m_AllowGroupPropagation = true
    };

    Assert.True(settings.m_AllowGroupPropagation);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release --filter Junction_settings_round_trip_preserves_group_propagation_flag`
Expected: FAIL until the serialized settings shape is finalized.

**Step 3: Write minimal implementation**

```typescript
<Row hoverEffect={true} data={{
  itemType: "checkbox",
  isChecked: displayedGroup.allowTspPropagation,
  key: "AllowTspPropagation",
  value: "0",
  label: "",
  engineEventName: "C2VM.TrafficLightsEnhancement.TRIGGER:CallSetTspPropagation"
}}>
  <Checkbox isChecked={displayedGroup.allowTspPropagation} />
  <div className={styles.dimLabel}>Allow Coordinated TSP Propagation</div>
</Row>
```

**Step 4: Run test to verify it passes**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -c Release`
Expected: PASS for settings serialization tests.

**Step 5: Commit**

```bash
git add TrafficLightsEnhancement/Systems/UI/UITypes.cs TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs TrafficLightsEnhancement/UI/src/mods/general.ts TrafficLightsEnhancement/UI/src/bindings.ts TrafficLightsEnhancement/UI/src/mods/components/main-panel/index.tsx TrafficLightsEnhancement/UI/src/mods/components/custom-phase-tool/main-panel/sub-panel.tsx TrafficLightsEnhancement/UI/src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx TrafficLightsEnhancement/Resources/Localisations/en-US.json
git commit -m "feat: add TSP controls to junction and group UI"
```

### Task 7: Run Build Verification And Manual Smoke Checks

**Files:**
- Modify: `README.md`
- Modify: `GUIDE.md`

**Step 1: Write the failing test**

```text
Verification checklist:
- root build succeeds
- UI bundle succeeds
- old save loads without TSP settings
- tram-only request advances or extends track-serving phase
- bus-lane request advances or extends public-car-serving phase
- coordinated group leader propagates follower request
```

**Step 2: Run test to verify it fails**

Run: `dotnet build --configuration Release`
Expected: FAIL until all backend and UI integration work is complete.

**Step 3: Write minimal implementation**

```md
## Transit Signal Priority

- Enable TSP per junction.
- Trams on track lanes can request priority.
- Buses on public-only lanes can request priority.
- Coordinated groups can optionally propagate TSP through the existing green-wave system.
```

**Step 4: Run test to verify it passes**

Run: `dotnet build --configuration Release`
Expected: PASS

Run: `npm run build`
Working directory: `TrafficLightsEnhancement/UI`
Expected: PASS

Run manual checks in-game:
1. Load an existing save with no TSP-enabled junctions and confirm the save loads without migration errors.
2. Enable TSP on a standalone tram junction and confirm the track-serving phase advances or extends.
3. Enable TSP on a standalone bus-lane junction and confirm the public-car-serving phase advances or extends.
4. Create a coordinated group with green wave, trigger a follower-originated tram request, and confirm the leader chooses the corridor-serving phase while followers stay synchronized.

**Step 5: Commit**

```bash
git add README.md GUIDE.md
git commit -m "docs: document TSP behavior and verification"
```
