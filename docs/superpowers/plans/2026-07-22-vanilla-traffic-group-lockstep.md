# Vanilla traffic-group lockstep implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Make coordinated vanilla-pattern traffic-light followers continuously mirror their leader's state and timer in TLE Extended.

**Architecture:** Keep CustomStateMachine.ShouldFollowLeader and SyncSignalGroupWithLeader as the sole authority for follower eligibility and copying. In UpdateTrafficLightsJob.Execute, calculate custom-phase priority/flow only for custom patterns, then dispatch every eligible follower through the shared synchronization path before choosing either signal state machine. Add a source-level ECS regression test because the runtime job depends on Cities II ECS types that the current test project does not instantiate directly.

**Tech stack:** C# / .NET Framework 4.8; Unity ECS job code; xUnit source-level ECS tests; Cities: Skylines II local mod toolchain.

## Global constraints

- Work only on branch codex/fix-vanilla-group-lockstep in this isolated worktree.
- Preserve assembly, namespace, mod ID, save data, settings, localization, UI contracts, green-wave behavior, and TSP group suspension.
- Signal groups remain 1-based at the ECS boundary.
- Do not make any upstream repository change.
- Close Cities: Skylines II before deploying the Release build.
- Do not call the change fixed until automated verification, installed-artifact verification, and the paused same-frame game check all pass.

---

### Task 1: Add the vanilla follower routing regression test

**Files:**

- Modify: TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs
- Test: TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs

**Interfaces:**

- Consumes: PatchedTrafficLightSystem.UpdateTrafficLightsJob.Execute, CustomStateMachine.ShouldFollowLeader, and CustomStateMachine.SyncSignalGroupWithLeader.
- Produces: Coordinated_followers_sync_before_custom_or_vanilla_dispatch, a regression test that defines Task 2's routing contract.

- [ ] **Step 1: Write the failing test**

Add this test beside the existing traffic-group source tests and add GetPatchedTrafficLightSystemPath beside the existing source-path helpers.

    [Fact]
    public void Coordinated_followers_sync_before_custom_or_vanilla_dispatch()
    {
        string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
        string executeSource = ExtractSection(
            source,
            "public void Execute(in ArchetypeChunk chunk",
            "private void FillLaneSignals");

        int followerDispatch = executeSource.IndexOf(
            "if (CustomStateMachine.ShouldFollowLeader(this, currentEntity, out Entity groupEntity))",
            StringComparison.Ordinal);
        int customDispatch = executeSource.IndexOf(
            "else if (usesCustomPhase)",
            StringComparison.Ordinal);
        int vanillaDispatch = executeSource.IndexOf(
            "else\n                {\n                    bool trafficLightStateUpdated = UpdateTrafficLightState(",
            StringComparison.Ordinal);

        Assert.True(followerDispatch >= 0, "Expected one shared follower dispatch.");
        Assert.True(customDispatch > followerDispatch, "Custom dispatch must follow follower dispatch.");
        Assert.True(vanillaDispatch > customDispatch, "Vanilla dispatch must follow the custom non-follower path.");

        string followerSource = executeSource.Substring(
            followerDispatch,
            customDispatch - followerDispatch);
        Assert.Contains(
            "CustomStateMachine.SyncSignalGroupWithLeader",
            followerSource);
    }

    private static string GetPatchedTrafficLightSystemPath()
    {
        return GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficLightSystems",
            "Simulation",
            "PatchedTrafficLightSystem.cs");
    }

The test must require one shared follower branch. It must not accept a second copied follower check inside the vanilla branch.

- [ ] **Step 2: Run the test and verify red**

Run:

    dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj --filter FullyQualifiedName~TrafficGroupSystemSourceTests.Coordinated_followers_sync_before_custom_or_vanilla_dispatch

Expected: one failing test because the current source nests ShouldFollowLeader inside the custom-phase branch and has no usesCustomPhase dispatch variable.

- [ ] **Step 3: Commit the red test**

    git add TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs
    git commit -m "test: cover vanilla traffic group lockstep routing"

### Task 2: Route all coordinated followers through the existing synchronization path

**Files:**

- Modify: TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs:267-360
- Test: TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs

**Interfaces:**

- Consumes: CustomStateMachine.ShouldFollowLeader(UpdateTrafficLightsJob, Entity, out Entity) and SyncSignalGroupWithLeader(UpdateTrafficLightsJob, Entity, Entity, ref TrafficLights, ref CustomTrafficLights).
- Produces: one runtime dispatch rule: eligible coordinated followers synchronize and skip both state machines; every other intersection uses its existing custom or vanilla path.

- [ ] **Step 1: Introduce the explicit custom-pattern flag and retain custom-only prework**

Replace the current CustomPhase condition opening with this setup. The usesCustomPhase condition must retain all three existing checks.

    bool usesCustomPhase =
        customTrafficLights.GetPatternOnly() == CustomTrafficLights.Patterns.CustomPhase
        && i < customPhaseDataBufferAccessor.Length
        && (trafficLights.m_Flags & TrafficLightFlags.MoveableBridge) == 0;

    DynamicBuffer<CustomPhaseData> customPhaseDataBuffer = default;
    if (usesCustomPhase)
    {
        customPhaseDataBuffer = customPhaseDataBufferAccessor[i];
        CustomStateMachine.CalculatePriority(this, subLanes, customPhaseDataBuffer);
        CustomStateMachine.CalculateFlow(
            this,
            unfilteredChunkIndex,
            subLanes,
            trafficLights,
            customPhaseDataBuffer);
    }

- [ ] **Step 2: Put follower synchronization ahead of both state-machine branches**

Immediately after that setup, use this three-way dispatch.

    if (CustomStateMachine.ShouldFollowLeader(this, currentEntity, out Entity groupEntity))
    {
        CustomStateMachine.SyncSignalGroupWithLeader(
            this,
            currentEntity,
            groupEntity,
            ref trafficLights,
            ref customTrafficLights);
        UpdateLaneSignals(laneSignals, trafficLights);
        UpdateTrafficLightObjects(subObjects, trafficLights);
    }
    else if (usesCustomPhase)
    {
        bool trafficLightStateUpdated = CustomStateMachine.UpdateTrafficLightState(
            ref trafficLights,
            ref customTrafficLights,
            customPhaseDataBuffer,
            customPhaseDataBuffer,
            activeTspSettings,
            hasTspRequest,
            activeTspRequest,
            ref pedestrianFairnessState,
            ref vehicleFairnessState,
            out var tspSelection);

        if (tspSelection.Applied
            && (trafficLightStateUpdated || tspSelection.Reason == TspSelectionReason.ExtendedCurrentPhase))
        {
            WriteTspDecisionTrace(
                unfilteredChunkIndex,
                currentEntity,
                trafficLights,
                activeTspRequest,
                tspSelection,
                customTrafficLights,
                pedestrianFairnessState,
                vehicleFairnessState);
            tspTraceWritten = true;
        }

        if (trafficLightStateUpdated)
        {
            UpdateLaneSignals(laneSignals, trafficLights);
            UpdateTrafficLightObjects(subObjects, trafficLights);
        }
    }
    else
    {
        bool trafficLightStateUpdated = UpdateTrafficLightState(
            laneSignals,
            moveableBridgeData,
            ref trafficLights,
            ref customTrafficLights,
            activeTspSettings,
            hasTspRequest,
            activeTspRequest,
            ref pedestrianFairnessState,
            ref vehicleFairnessState,
            out var tspSelection);

        if (tspSelection.Applied)
        {
            WriteTspDecisionTrace(
                unfilteredChunkIndex,
                currentEntity,
                trafficLights,
                activeTspRequest,
                tspSelection,
                customTrafficLights,
                pedestrianFairnessState,
                vehicleFairnessState);
            tspTraceWritten = true;
        }

        if (trafficLightStateUpdated)
        {
            UpdateLaneSignals(laneSignals, trafficLights);
            UpdateTrafficLightObjects(subObjects, trafficLights);
            if (entity != Entity.Null)
            {
                ref PointOfInterest valueRW = ref m_PointOfInterestData.GetRefRW(entity).ValueRW;
                UpdateMoveableBridge(
                    trafficLights,
                    m_TransformData[entity],
                    moveableBridgeData,
                    ref valueRW);
                m_CommandBuffer.AddComponent<EffectsUpdated>(unfilteredChunkIndex, currentEntity);
            }
        }
    }

Do not move the later stale-TSP-trace cleanup, fairness refresh, component writes, or laneSignals.Clear statements. They must continue to run for every branch.

- [ ] **Step 3: Run focused and complete automated verification**

Run:

    dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj --filter FullyQualifiedName~TrafficGroupSystemSourceTests.Coordinated_followers_sync_before_custom_or_vanilla_dispatch
    dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj
    git diff --check

Expected: the focused test passes; the full ECS suite passes with 82 tests and 0 failures; git diff --check prints no output.

- [ ] **Step 4: Commit the runtime fix**

    git add TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs
    git commit -m "fix: synchronize vanilla traffic group followers"

### Task 3: Build, verify the installed artifact, and replay the live reproduction

**Files:**

- Verify: TrafficLightsEnhancement/bin/Release/net48/C2VM.TrafficLightsEnhancement.dll
- Verify: C:\Users\matt\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\C2VM.TrafficLightsEnhancement\C2VM.TrafficLightsEnhancement.dll
- Verify in game: Copeland traffic group leader 2060541 and follower 2060544

**Interfaces:**

- Consumes: the Release build output, deployed local-mod DLL, and selected-junction diagnostics.
- Produces: evidence that the installed TLE Extended build keeps vanilla-pattern lockstep timers equal at one paused simulation frame.

- [ ] **Step 1: Create a recoverable playtest baseline and close the game**

Use the CS2 bridge to create a manual save named Copeland-before-vanilla-lockstep, then close Cities: Skylines II completely. Do not overwrite the installed mod while the game is running.

- [ ] **Step 2: Build the Release mod**

Run:

    dotnet build Cities2-TrafficLightsEnhancement.sln --configuration Release -p:LangVersion=latest

Expected: successful post-processing and deployment to the local TLE Extended mod directory.

- [ ] **Step 3: Verify that the installed managed DLL is the build output**

Run:

    $buildDll = 'TrafficLightsEnhancement\bin\Release\net48\C2VM.TrafficLightsEnhancement.dll'
    $installedDll = Join-Path $env:USERPROFILE 'AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\C2VM.TrafficLightsEnhancement\C2VM.TrafficLightsEnhancement.dll'
    Test-Path $buildDll
    Test-Path $installedDll
    (Get-FileHash -Algorithm SHA256 $buildDll).Hash
    (Get-FileHash -Algorithm SHA256 $installedDll).Hash

Expected: both paths exist and their SHA-256 hashes are identical.

- [ ] **Step 4: Run the controlled in-game check**

Launch Cities: Skylines II, confirm the local TLE Extended mod is enabled, and load Copeland-before-vanilla-lockstep. Keep the existing group configuration unchanged: both intersections use the vanilla pattern, green wave is disabled, and the follower has delay 0 and phase offset 0.

Let the simulation run until leader 2060541 is in an ongoing phase, pause the simulation, select the leader and record its current/next signal group, state, and timer. Without resuming, select follower 2060544 and record the same fields at that exact frame.

Expected: leader and follower report equal state, current/next group, and timer. A timer mismatch like the pre-fix 38 versus 24 result is a failure.

- [ ] **Step 5: Record verification evidence in the handoff**

Report the build command result, both DLL hashes, the paused simulation frame, and the leader/follower diagnostic values. State explicitly whether gameplay validation passed; do not describe the source change as fixed solely because tests or compilation pass.

## Plan self-review

- Spec coverage: Task 1 establishes the missing regression contract; Task 2 performs only the approved runtime routing change; Task 3 verifies build, deployed artifact, and the original live symptom.
- Scope: no serialized components, UI bindings, TSP policy, or upstream repository files are changed.
- Type consistency: the plan uses existing ShouldFollowLeader, SyncSignalGroupWithLeader, CustomStateMachine.UpdateTrafficLightState, and normal UpdateTrafficLightState signatures.
- Placeholder scan: no unresolved requirements or alternative implementations remain.
