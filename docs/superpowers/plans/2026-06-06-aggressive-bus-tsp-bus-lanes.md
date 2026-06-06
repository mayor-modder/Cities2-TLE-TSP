# Aggressive Bus TSP on Marked Bus Lanes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give buses the same aggressive conflicting-group signal preemption that trams get, but only when the bus is detected on a marked (PublicOnly) bus lane.

**Architecture:** An `OnDedicatedLane` flag is carried from the existing bus lane detection (`BusApproachSample.IsBusOnlyLane`, set from `CarLaneFlags.PublicOnly`) through the pure `TspRequest`, the runtime `TransitSignalPriorityRequest` component, and the pure `TspSignalRequest`, into the single pure-policy predicate that already drives tram-style aggressive preemption. The predicate is broadened from "Track only" to "Track, or PublicCar on a dedicated lane." Everything is runtime-only: no saved-payload change, no migration, no new UI toggle. A diagnostics row surfaces aggressive-vs-soft for playtesting.

**Tech Stack:** C# (netstandard2.0 pure logic + net48 ECS/Unity DOTS), xUnit, Node test runner for the React panel, `Locale.json` for UI strings.

**Repo skills to keep loaded:** `tle-csharp-ecs` (layer boundaries, TSP contracts), `tle-testing-release` (verification commands), `tle-ui-localization` (Locale.json + diagnostics row), `tle-code-documentation` (doc updates), `tle-diagnostic-review` (sanity-check the aggressive-vs-soft signal).

---

## Background: the single behavioral delta

All tram-aggressive behavior flows through one private predicate in
`TrafficLightsEnhancement.Logic/Tsp/TspPreemptionPolicy.cs`,
`IsTrackPreemptionToDifferentGroup`, which drives both public entry points:

- `ShouldAggressivelyPreemptToConflictingGroup(...)`, and
- `GetMinimumGreenDurationTicks(...)` (returns the 1-tick `AggressivePreemptionMinimumGreenTicks`).

Today that predicate requires `request.Source == TspSource.Track`. The only behavior
change in this plan is broadening it to also accept a `PublicCar` request whose new
`OnDedicatedLane` flag is set. The other two source-relevant predicates
(`ShouldHoldCurrentGroup`, `ShouldApplyTargetGroupSelection`) already accept
`PublicCar` and are not touched. The min-green call site in
`PatchedTrafficLightSystem.cs:576` and the custom state machine path need no change —
they already route through `GetMinimumGreenDurationTicks`.

## File structure

Pure logic (`TrafficLightsEnhancement.Logic/Tsp/`):
- `TspRequestInputs.cs` — add `OnDedicatedLane` to `TspRequest`.
- `TspPreemptionPolicy.cs` — add `OnDedicatedLane` to `TspSignalRequest`; broaden + rename the aggressive predicate; preserve the flag through latching.

Runtime ECS (`TrafficLightsEnhancement/`):
- `Components/TransitSignalPriorityRequest.cs` — add transient `m_OnDedicatedLane`.
- `Components/TransitSignalPriorityDecisionTrace.cs` — add transient `m_OnDedicatedLane`.
- `Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs` — thread the flag through `CreateRequest`, `ToSignalRequest`, `FromSignalRequest`, and set it at the bus build site.
- `Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs` — copy the flag into the decision trace.
- `Systems/UI/UISystem.UIBIndings.cs` — add a diagnostics row.

UI strings:
- `TrafficLightsEnhancement/Locale.json` — add the diagnostics row label.

Tests:
- `TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs` — policy + latching.
- `TrafficLightsEnhancement.Ecs.Tests/TransitSignalPriorityRuntimeBusRequestTests.cs` — bus build flag.

Docs:
- `docs/tsp-architecture.md`, `.agents/skills/tle-csharp-ecs/SKILL.md`, `docs/save-format-contract.md`.

---

## Task 1: Add `OnDedicatedLane` to the pure DTOs

This is structural plumbing. The flag defaults to `false`, so every existing call
site keeps compiling and keeps today's behavior. No behavior change yet.

**Files:**
- Modify: `TrafficLightsEnhancement.Logic/Tsp/TspRequestInputs.cs:28-40`
- Modify: `TrafficLightsEnhancement.Logic/Tsp/TspPreemptionPolicy.cs:3-19`

- [ ] **Step 1: Add the field to `TspRequest`**

Replace the `TspRequest` struct (`TspRequestInputs.cs:28-40`) with:

```csharp
public struct TspRequest
{
    public TspRequest(TspSource source, float strength, bool extensionEligible, bool onDedicatedLane = false)
    {
        Source = source;
        Strength = strength;
        ExtensionEligible = extensionEligible;
        OnDedicatedLane = onDedicatedLane;
    }

    public TspSource Source { get; }
    public float Strength { get; }
    public bool ExtensionEligible { get; }
    public bool OnDedicatedLane { get; }
}
```

- [ ] **Step 2: Add the field to `TspSignalRequest`**

Replace the `TspSignalRequest` struct (`TspPreemptionPolicy.cs:3-19`) with:

```csharp
public readonly struct TspSignalRequest
{
    public TspSignalRequest(int targetSignalGroup, TspSource source, float strength, uint expiryTimer, bool extendCurrentPhase, bool onDedicatedLane = false)
    {
        TargetSignalGroup = targetSignalGroup;
        Source = source;
        Strength = strength;
        ExpiryTimer = expiryTimer;
        ExtendCurrentPhase = extendCurrentPhase;
        OnDedicatedLane = onDedicatedLane;
    }

    public int TargetSignalGroup { get; }
    public TspSource Source { get; }
    public float Strength { get; }
    public uint ExpiryTimer { get; }
    public bool ExtendCurrentPhase { get; }
    public bool OnDedicatedLane { get; }
}
```

- [ ] **Step 3: Build the pure logic project to confirm it compiles**

Run: `dotnet build TrafficLightsEnhancement.Logic/TrafficLightsEnhancement.Logic.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add TrafficLightsEnhancement.Logic/Tsp/TspRequestInputs.cs TrafficLightsEnhancement.Logic/Tsp/TspPreemptionPolicy.cs
git commit -m "feat(tsp): add OnDedicatedLane flag to TspRequest and TspSignalRequest"
```

---

## Task 2: Broaden and rename the aggressive preemption predicate

This is the core behavior change. TDD: write the failing tests first.

**Files:**
- Test: `TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs`
- Modify: `TrafficLightsEnhancement.Logic/Tsp/TspPreemptionPolicy.cs:111-150`

- [ ] **Step 1: Write the failing tests**

Append these `[Fact]` methods inside the existing test class in
`TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs` (the file already has
`using TrafficLightsEnhancement.Logic.Tsp;` and `using Xunit;`):

```csharp
[Fact]
public void AggressivePreemption_BusOnDedicatedLane_PreemptsConflictingGroup()
{
    Assert.True(TspPreemptionPolicy.ShouldAggressivelyPreemptToConflictingGroup(
        currentSignalGroup: 1,
        request: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 10, extendCurrentPhase: false, onDedicatedLane: true)));
}

[Fact]
public void AggressivePreemption_BusOnDedicatedLane_ShortensMinimumGreen()
{
    Assert.Equal(
        TspPreemptionPolicy.AggressivePreemptionMinimumGreenTicks,
        TspPreemptionPolicy.GetMinimumGreenDurationTicks(
            defaultMinimumGreenTicks: 30,
            currentSignalGroup: 1,
            request: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 10, extendCurrentPhase: false, onDedicatedLane: true)));
}

[Fact]
public void AggressivePreemption_BusOnMixedLane_StaysSoft()
{
    Assert.False(TspPreemptionPolicy.ShouldAggressivelyPreemptToConflictingGroup(
        currentSignalGroup: 1,
        request: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 10, extendCurrentPhase: false, onDedicatedLane: false)));

    Assert.Equal(
        30,
        TspPreemptionPolicy.GetMinimumGreenDurationTicks(
            defaultMinimumGreenTicks: 30,
            currentSignalGroup: 1,
            request: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 10, extendCurrentPhase: false, onDedicatedLane: false)));
}

[Fact]
public void AggressivePreemption_BusOnDedicatedLane_BlockedByPedestrianProtection()
{
    Assert.False(TspPreemptionPolicy.ShouldAggressivelyPreemptToConflictingGroup(
        currentSignalGroup: 1,
        request: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 10, extendCurrentPhase: false, onDedicatedLane: true),
        protectActivePedestrianPhase: true));
}

[Fact]
public void AggressivePreemption_TramUnchanged_StillAggressiveWithoutFlag()
{
    Assert.True(TspPreemptionPolicy.ShouldAggressivelyPreemptToConflictingGroup(
        currentSignalGroup: 1,
        request: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 10, extendCurrentPhase: false, onDedicatedLane: false)));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj --filter "FullyQualifiedName~AggressivePreemption_Bus"`
Expected: FAIL — `AggressivePreemption_BusOnDedicatedLane_PreemptsConflictingGroup` and `..._ShortensMinimumGreen` fail because today the predicate only accepts `TspSource.Track`.

- [ ] **Step 3: Broaden and rename the predicate**

In `TrafficLightsEnhancement.Logic/Tsp/TspPreemptionPolicy.cs`, replace the private
`IsTrackPreemptionToDifferentGroup` method (lines 139-150) with the renamed,
broadened version:

```csharp
    private static bool IsAggressivePreemptionToDifferentGroup(
        byte currentSignalGroup,
        TspSignalRequest request,
        bool protectActivePedestrianPhase)
    {
        return !protectActivePedestrianPhase
            && currentSignalGroup > 0
            && IsAggressiveEligibleSource(request)
            && request.TargetSignalGroup > 0
            && request.TargetSignalGroup != currentSignalGroup
            && request.ExpiryTimer > 0;
    }

    private static bool IsAggressiveEligibleSource(TspSignalRequest request)
    {
        return request.Source == TspSource.Track
            || (request.Source == TspSource.PublicCar && request.OnDedicatedLane);
    }
```

- [ ] **Step 4: Update the two callers to the renamed method**

In the same file, in `GetMinimumGreenDurationTicks` (line 117) and
`ShouldAggressivelyPreemptToConflictingGroup` (line 127), change the call
`IsTrackPreemptionToDifferentGroup(...)` to
`IsAggressivePreemptionToDifferentGroup(...)`. Both call sites keep identical
arguments — only the method name changes. Confirm no other references remain:

Run: `grep -rn "IsTrackPreemptionToDifferentGroup" TrafficLightsEnhancement.Logic TrafficLightsEnhancement TrafficLightsEnhancement.Tests`
Expected: no matches.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj`
Expected: PASS — all new tests pass and the existing suite (including the existing
soft-bus test at `TspDecisionEngineTests.cs:785`, which uses no flag) stays green.

- [ ] **Step 6: Commit**

```bash
git add TrafficLightsEnhancement.Logic/Tsp/TspPreemptionPolicy.cs TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs
git commit -m "feat(tsp): aggressive preemption for buses on dedicated lanes"
```

---

## Task 3: Preserve `OnDedicatedLane` through request latching

A latched request keeps decrementing for its horizon. The flag must survive the
fresh-build and decrement paths so a latched bus-lane request stays aggressive.

**Files:**
- Test: `TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs`
- Modify: `TrafficLightsEnhancement.Logic/Tsp/TspPreemptionPolicy.cs:50-56,87-95`

- [ ] **Step 1: Write the failing tests**

Append to `TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs`:

```csharp
[Fact]
public void Latch_FreshBusOnDedicatedLane_PreservesFlag()
{
    bool latched = TspPreemptionPolicy.TryRefreshOrLatchRequest(
        freshRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 1, extendCurrentPhase: true, onDedicatedLane: true),
        existingRequest: null,
        requestHorizonTicks: 10,
        currentSignalGroup: 1,
        out TspSignalRequest request);

    Assert.True(latched);
    Assert.True(request.OnDedicatedLane);
}

[Fact]
public void Latch_DecrementExistingBusOnDedicatedLane_PreservesFlag()
{
    bool latched = TspPreemptionPolicy.TryRefreshOrLatchRequest(
        freshRequest: null,
        existingRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 5, extendCurrentPhase: true, onDedicatedLane: true),
        requestHorizonTicks: 10,
        currentSignalGroup: 1,
        out TspSignalRequest request);

    Assert.True(latched);
    Assert.True(request.OnDedicatedLane);
    Assert.Equal(4u, request.ExpiryTimer);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj --filter "FullyQualifiedName~Latch_"`
Expected: FAIL — both assert `request.OnDedicatedLane` is true but it comes back false
(the fresh-build constructor and `DecrementLatchedRequest` drop the flag).

- [ ] **Step 3: Carry the flag in the fresh-build path**

In `TspPreemptionPolicy.cs`, in `TryRefreshOrLatchRequest`, replace the fresh-request
construction (lines 50-55) with:

```csharp
            request = new TspSignalRequest(
                fresh.TargetSignalGroup,
                fresh.Source,
                fresh.Strength,
                requestHorizonTicks,
                fresh.ExtendCurrentPhase,
                fresh.OnDedicatedLane);
```

- [ ] **Step 4: Carry the flag in `DecrementLatchedRequest`**

In `TspPreemptionPolicy.cs`, replace `DecrementLatchedRequest` (lines 87-95) with:

```csharp
    private static TspSignalRequest DecrementLatchedRequest(TspSignalRequest request)
    {
        return new TspSignalRequest(
            request.TargetSignalGroup,
            request.Source,
            request.Strength,
            request.ExpiryTimer - 1,
            request.ExtendCurrentPhase,
            request.OnDedicatedLane);
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj`
Expected: PASS — new latching tests pass, full suite green.

- [ ] **Step 6: Commit**

```bash
git add TrafficLightsEnhancement.Logic/Tsp/TspPreemptionPolicy.cs TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs
git commit -m "feat(tsp): preserve OnDedicatedLane across request latching"
```

---

## Task 4: Thread the flag through the runtime request component

The runtime `TransitSignalPriorityRequest` is a transient ECS component, not save
data, so adding a field needs no version bump and no migration.

**Files:**
- Modify: `TrafficLightsEnhancement/Components/TransitSignalPriorityRequest.cs:5-12`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs:1446-1484`

- [ ] **Step 1: Add the transient field to the component**

Replace `TransitSignalPriorityRequest.cs` body with:

```csharp
public struct TransitSignalPriorityRequest : IComponentData
{
    public byte m_TargetSignalGroup;
    public byte m_SourceType;
    public float m_Strength;
    public uint m_ExpiryTimer;
    public bool m_ExtendCurrentPhase;
    public bool m_OnDedicatedLane;
}
```

- [ ] **Step 2: Copy the flag in `CreateRequest`**

In `TransitSignalPriorityRuntime.cs`, in `CreateRequest` (lines 1453-1461), add the
field after `m_ExtendCurrentPhase`:

```csharp
        return new TransitSignalPriorityRequest
        {
            m_TargetSignalGroup = targetSignalGroup,
            m_SourceType = (byte)request.Source,
            m_Strength = request.Strength,
            m_ExpiryTimer = expiryTimer,
            m_ExtendCurrentPhase = request.ExtensionEligible
                && (laneSignal.m_Flags & LaneSignalFlags.CanExtend) != 0,
            m_OnDedicatedLane = request.OnDedicatedLane,
        };
```

- [ ] **Step 3: Copy the flag in `ToSignalRequest`**

In `TransitSignalPriorityRuntime.cs`, replace `ToSignalRequest` (lines 1464-1472) with:

```csharp
    private static TspSignalRequest ToSignalRequest(TransitSignalPriorityRequest request)
    {
        return new TspSignalRequest(
            request.m_TargetSignalGroup,
            (TspSource)request.m_SourceType,
            request.m_Strength,
            request.m_ExpiryTimer,
            request.m_ExtendCurrentPhase,
            request.m_OnDedicatedLane);
    }
```

- [ ] **Step 4: Copy the flag in `FromSignalRequest`**

In `TransitSignalPriorityRuntime.cs`, replace `FromSignalRequest` (lines 1474-1484) with:

```csharp
    private static TransitSignalPriorityRequest FromSignalRequest(TspSignalRequest request)
    {
        return new TransitSignalPriorityRequest
        {
            m_TargetSignalGroup = (byte)request.TargetSignalGroup,
            m_SourceType = (byte)request.Source,
            m_Strength = request.Strength,
            m_ExpiryTimer = request.ExpiryTimer,
            m_ExtendCurrentPhase = request.ExtendCurrentPhase,
            m_OnDedicatedLane = request.OnDedicatedLane,
        };
    }
```

- [ ] **Step 5: Commit (the field is plumbed but not yet set from a bus sample until Task 5; a full ECS build runs in Task 5)**

```bash
git add TrafficLightsEnhancement/Components/TransitSignalPriorityRequest.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs
git commit -m "feat(tsp): carry OnDedicatedLane through runtime request component"
```

---

## Task 5: Set the flag at the bus request build site

The bus build calls the pure builder, then stamps the dedicated-lane flag from the
already-known sample (`BusApproachSample.IsBusOnlyLane`). This keeps the pure builder
untouched and matches the design (the bus path owns the flag).

**Files:**
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs:300-310`
- Test: `TrafficLightsEnhancement.Ecs.Tests/TransitSignalPriorityRuntimeBusRequestTests.cs`

- [ ] **Step 1: Write the failing ECS test**

Add to `TrafficLightsEnhancement.Ecs.Tests/TransitSignalPriorityRuntimeBusRequestTests.cs`.
Mirror the existing tests' construction of `logicSettings` and `BusApproachSample`
(read a neighboring test in that file for the exact helper/setup used to enable bus
requests and to make a sample eligible — moving speed, no DummyTraffic, no ambiguous
lane change). The new assertions:

```csharp
[Fact]
public void BusRequest_OnBusOnlyLane_SetsOnDedicatedLane()
{
    var sample = CreateEligibleBusSample();   // existing helper pattern in this file
    sample.IsBusOnlyLane = 1;

    bool built = TransitSignalPriorityRuntime.TryBuildBusApproachRequestFromSample(
        EnabledBusLogicSettings(),            // existing helper pattern in this file
        sample,
        TransitSignalPriorityBusProbeResult.MatchOnSignaledLane,
        out TspRequest request,
        out _,
        out _);

    Assert.True(built);
    Assert.Equal(TspSource.PublicCar, request.Source);
    Assert.True(request.OnDedicatedLane);
}

[Fact]
public void BusRequest_OnMixedLane_LeavesOnDedicatedLaneFalse()
{
    var sample = CreateEligibleBusSample();
    sample.IsBusOnlyLane = 0;

    bool built = TransitSignalPriorityRuntime.TryBuildBusApproachRequestFromSample(
        EnabledBusLogicSettings(),
        sample,
        TransitSignalPriorityBusProbeResult.MatchOnSignaledLane,
        out TspRequest request,
        out _,
        out _);

    Assert.True(built);
    Assert.False(request.OnDedicatedLane);
}
```

If this test file has no `CreateEligibleBusSample`/`EnabledBusLogicSettings` helpers,
build the `BusApproachSample` and `TransitSignalPrioritySettings` inline by copying the
field assignments from the nearest existing passing test in the same file. Do not
invent new helper names that the file does not already define.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj --filter "FullyQualifiedName~BusRequest_On"`
Expected: FAIL — `BusRequest_OnBusOnlyLane_SetsOnDedicatedLane` fails because the built
request has `OnDedicatedLane == false`.

- [ ] **Step 3: Stamp the flag after the pure build**

In `TransitSignalPriorityRuntime.cs`, in `TryBuildBusApproachRequestFromSample`,
replace the build block (lines 300-310) with:

```csharp
        if (!global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPriorityRuntime.TryBuildRequestForLane(
                logicSettings,
                isTrackLane: false,
                isPublicCarLane: true,
                out request))
        {
            return false;
        }

        if (sample.IsBusOnlyLane != 0)
        {
            request = new TspRequest(
                request.Source,
                request.Strength,
                request.ExtensionEligible,
                onDedicatedLane: true);
        }

        decision = TransitSignalPriorityBusDecision.RequestEmitted;
        return request.Source == TspSource.PublicCar;
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj`
Expected: PASS — new tests pass, existing ECS regressions green.

- [ ] **Step 5: Commit**

```bash
git add TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs TrafficLightsEnhancement.Ecs.Tests/TransitSignalPriorityRuntimeBusRequestTests.cs
git commit -m "feat(tsp): set OnDedicatedLane for buses detected on bus-only lanes"
```

---

## Task 6: Surface aggressive-vs-soft in diagnostics

Add a diagnostics row so playtesting can confirm the aggressive path fires on bus
lanes and stays off for mixed-lane buses. The flag rides on the decision trace.

**Files:**
- Modify: `TrafficLightsEnhancement/Components/TransitSignalPriorityDecisionTrace.cs:5-18`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs:443-464`
- Modify: `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs:1197-1210`
- Modify: `TrafficLightsEnhancement/Locale.json:168-175`

- [ ] **Step 1: Add the flag to the decision trace component**

In `TransitSignalPriorityDecisionTrace.cs`, add the field after `m_SourceType`:

```csharp
    public byte m_SourceType;
    public bool m_OnDedicatedLane;
    public byte m_Reason;
```

- [ ] **Step 2: Set the flag where the trace is built**

In `PatchedTrafficLightSystem.cs`, in the `TransitSignalPriorityDecisionTrace`
initializer (line 452), add after `m_SourceType = activeTspRequest.m_SourceType,`:

```csharp
                m_SourceType = activeTspRequest.m_SourceType,
                m_OnDedicatedLane = activeTspRequest.m_OnDedicatedLane,
```

- [ ] **Step 3: Add the diagnostics row**

In `UISystem.UIBIndings.cs`, inside the `if (hasDecisionTrace)` block, immediately
after the `TSPDiagnosticsDecisionSource` row (line 1203), add a bus-priority-mode row.
`TspSource` here is `global::TrafficLightsEnhancement.Logic.Tsp.TspSource`, matching
the existing usage at line 1132:

```csharp
            rows.Add(new { label = "TSPDiagnosticsDecisionSource", value = GetTspSourceName(decisionTrace.m_SourceType) });
            if ((global::TrafficLightsEnhancement.Logic.Tsp.TspSource)decisionTrace.m_SourceType == global::TrafficLightsEnhancement.Logic.Tsp.TspSource.PublicCar)
            {
                rows.Add(new { label = "TSPDiagnosticsBusPriorityMode", value = decisionTrace.m_OnDedicatedLane ? "Aggressive (bus lane)" : "Soft" });
            }
```

- [ ] **Step 4: Add the Locale.json label**

In `TrafficLightsEnhancement/Locale.json`, add next to the other bus diagnostics
labels (after `TSPDiagnosticsBusLaneChange` at line 169):

```json
    "UI.LABEL[C2VM.TrafficLightsEnhancement.TSPDiagnosticsBusPriorityMode]": "Bus priority mode",
```

- [ ] **Step 5: Run the UI panel tests**

Run: `npm test --prefix TrafficLightsEnhancement/UI`
Expected: PASS — the panel test asserts the `rows` array shape (`{ label, value }`),
which the new row satisfies; no assertion needs changing.

- [ ] **Step 6: Commit**

```bash
git add TrafficLightsEnhancement/Components/TransitSignalPriorityDecisionTrace.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs TrafficLightsEnhancement/Locale.json
git commit -m "feat(tsp): show aggressive-vs-soft bus priority mode in diagnostics"
```

---

## Task 7: Documentation

Update maintainer docs and the agent skill so the new behavior and the runtime-only
nature are recorded. Use `tle-code-documentation` for tone (maintainer-facing,
evidence-backed, no marketing).

**Files:**
- Modify: `docs/tsp-architecture.md`
- Modify: `.agents/skills/tle-csharp-ecs/SKILL.md`
- Modify: `docs/save-format-contract.md`

- [ ] **Step 1: Update `docs/tsp-architecture.md`**

Make these edits:
- In the Pure Logic boundary conventions bullet about `TspSource.PublicCar`
  (around line 39), note that bus requests on marked (PublicOnly) bus lanes now carry
  `OnDedicatedLane` and receive tram-style aggressive conflicting-group preemption,
  while mixed-lane buses stay soft.
- In "Applying Requests To Signals" (around line 129), update the
  `ShouldAggressivelyPreemptToConflictingGroup` description to say aggressive
  preemption now fires for `Track` requests and for `PublicCar` requests on a
  dedicated lane, and note the predicate is named `IsAggressivePreemptionToDifferentGroup`.
- In "Caveats For Future Work" (around line 207), revise the "soft release-ready MVP"
  bullet to record that dedicated-lane buses are now aggressive; mixed-lane buses
  remain the conservative refinement area.

- [ ] **Step 2: Update `.agents/skills/tle-csharp-ecs/SKILL.md`**

In "TSP Contracts", change the bus bullet
("Bus priority is intentionally softer and should not gain tram-style aggressive
preemption without an explicit design.") to record the approved exception: buses on
marked (PublicOnly) bus lanes carry `OnDedicatedLane` and get tram-style aggressive
preemption; mixed-lane buses stay soft.

- [ ] **Step 3: Update `docs/save-format-contract.md`**

Add a one-line note that the aggressive-bus-lane feature is runtime-only: the
`OnDedicatedLane` flag lives on the transient `TransitSignalPriorityRequest` and
`TransitSignalPriorityDecisionTrace` components, so there is no saved-payload change
and no migration. (Place it wherever the contract lists runtime-only TSP state; match
the file's existing structure.)

- [ ] **Step 4: Commit**

```bash
git add docs/tsp-architecture.md .agents/skills/tle-csharp-ecs/SKILL.md docs/save-format-contract.md
git commit -m "docs(tsp): document aggressive bus priority on dedicated lanes"
```

---

## Task 8: Full verification

- [ ] **Step 1: Run all affected .NET test projects**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj`
Run: `dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj`
Expected: PASS for both.

- [ ] **Step 2: Run the UI panel tests**

Run: `npm test --prefix TrafficLightsEnhancement/UI`
Expected: PASS.

- [ ] **Step 3: Release build (if the modding toolchain is available)**

Run: `dotnet build Cities2-TrafficLightsEnhancement.sln --configuration Release`
Expected: Build succeeded. If the modding toolchain is unavailable, record that the
narrower test projects above were run instead, and do not claim full build coverage.

- [ ] **Step 4: Manual playtest sanity check (maintainer retesting)**

With diagnostics enabled, select an intersection served by a bus-only lane and confirm
the `Bus priority mode` row reads `Aggressive (bus lane)` when a bus approaches on the
bus lane, and `Soft` for a bus approaching on a mixed lane. Confirm a tram still wins
arbitration over a bus-lane bus for the same conflicting group. Use
`tle-diagnostic-review` to interpret the JSONL trace if needed.

---

## Self-review notes

- **Spec coverage:** goal/non-goals (Tasks 2, 5, 7), lenient trigger via any
  bus-only sample (Task 5 stamps from `sample.IsBusOnlyLane`, which already includes
  the change-lane sample per `BusApproachIndex`), single policy delta (Task 2),
  Approach A flag through the chain (Tasks 1, 4, 5), latching preservation (Task 3),
  diagnostics (Task 6), docs incl. save-format note (Task 7), no save-format change
  (Tasks 4, 6 use transient components), guardrails unchanged (Task 2 pedestrian test;
  source priority table untouched). All covered.
- **Type consistency:** `OnDedicatedLane` (pure DTOs `TspRequest`/`TspSignalRequest`),
  `m_OnDedicatedLane` (ECS components), `IsAggressivePreemptionToDifferentGroup`
  (renamed predicate), `IsAggressiveEligibleSource` (new helper) used consistently
  across tasks. The renamed predicate is private; public entry points
  (`ShouldAggressivelyPreemptToConflictingGroup`, `GetMinimumGreenDurationTicks`) keep
  their names, so ECS callers and existing tests need no rename.
- **No placeholders:** every code step shows concrete code; the only deliberately
  deferred detail is the existing ECS test helper names in Task 5, with explicit
  instructions to copy the neighboring test's setup rather than invent names.
