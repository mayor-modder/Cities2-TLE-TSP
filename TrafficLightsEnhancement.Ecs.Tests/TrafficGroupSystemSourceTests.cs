using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests;

public sealed class TrafficGroupSystemSourceTests
{
    [Fact]
    public void Add_junction_to_group_reuses_stale_null_group_member_component()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string addJunctionSource = ExtractMethod(source, "public bool AddJunctionToGroup");
        string canAssignSource = ExtractMethod(source, "private static bool CanAssignTrafficGroupMember");
        string setOrAddSource = ExtractMethod(source, "private static void SetOrAddTrafficGroupMember");

        Assert.Contains("SetOrAddTrafficGroupMember(EntityManager, junctionEntity, member)", addJunctionSource);
        Assert.DoesNotContain("EntityManager.AddComponentData(junctionEntity, member)", addJunctionSource);
        Assert.Contains(
            "return existingMember.m_GroupEntity == Entity.Null",
            canAssignSource);
        Assert.Matches(
            new Regex(
                @"if\s*\(\s*entityManager\.HasComponent<TrafficGroupMember>\(junctionEntity\)\s*\).*?entityManager\.SetComponentData\(junctionEntity,\s*member\)",
                RegexOptions.Singleline),
            setOrAddSource);
    }

    [Fact]
    public void Propagating_pattern_to_members_marks_each_member_updated()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string propagateSource = ExtractMethod(source, "public void PropagatePatternToMembers");

        Assert.Contains("memberLights.SetPattern(pattern)", propagateSource);
        Assert.Contains("MarkMemberUpdated(memberEntity)", propagateSource);
        Assert.Contains(
            "EnsureMemberCustomPhaseSetup(groupEntity, memberEntity)",
            propagateSource);
    }

    [Fact]
    public void Green_wave_paths_use_shared_timing_policy()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string calculateGreenWaveSource = ExtractMethod(source, "public void CalculateGreenWaveTiming");
        string applyCoordinationSource = ExtractMethod(source, "private void ApplyCoordination");
        string enhancedGreenWaveSource = ExtractMethod(source, "public void CalculateEnhancedGreenWaveTiming");
        string forceSyncSource = ExtractMethod(source, "public void ForceSyncToLeader");
        string applyBestPhaseSource = ExtractMethod(source, "public void ApplyBestPhaseToGroup");

        Assert.Contains(
            "TrafficGroupTimingPolicy.WrapCyclePosition(group.m_CycleTimer, phaseOffset, group.m_CycleLength)",
            calculateGreenWaveSource);
        Assert.Contains(
            "TrafficGroupTimingPolicy.WrapCyclePosition(group.m_CycleTimer, memberData.m_SignalDelay, group.m_CycleLength)",
            applyCoordinationSource);
        Assert.Contains(
            "TrafficGroupTimingPolicy.CalculateZeroBasedPhaseOffset(arrivalTime, leaderCycleLength, GetPhaseCount(memberEntity))",
            enhancedGreenWaveSource);
        Assert.Contains(
            "memberData.m_MemberCycleTimer = TrafficGroupTimingPolicy.WrapCyclePosition(",
            forceSyncSource);
        Assert.Contains(
            "group.m_CycleTimer, memberData.m_SignalDelay, group.m_CycleLength",
            forceSyncSource);
        Assert.Contains(
            "if (phaseCount <= 0)",
            applyBestPhaseSource);
        Assert.Contains(
            "TryMapLeaderPhase(memberEntity, bestPhase + 1, out int mappedPhase)",
            applyBestPhaseSource);
        Assert.Contains(
            "(mappedPhase - 1) + memberData.m_PhaseOffset",
            applyBestPhaseSource);
    }

    [Fact]
    public void Custom_state_machine_requires_complete_physical_mapping()
    {
        string source = File.ReadAllText(GetCustomStateMachinePath());
        string shouldFollowSource = ExtractMethod(
            source,
            "public static bool ShouldFollowLeader");

        Assert.Contains(
            "m_TrafficGroupPhaseMapping",
            shouldFollowSource);
        Assert.Contains("m_Map.IsComplete", shouldFollowSource);
        Assert.Contains("TryMapLeaderToMember", shouldFollowSource);
    }

    [Fact]
    public void Traffic_group_member_validation_accepts_signed_phase_offsets()
    {
        string migrationJobsSource = File.ReadAllText(GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "Serialization",
            "TLEDataMigrationJobs.cs"));
        string migrationSystemSource = File.ReadAllText(GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "Serialization",
            "TLEDataMigrationSystem.cs"));

        Assert.DoesNotContain("member.m_PhaseOffset < 0", migrationJobsSource);
        Assert.DoesNotContain("member.m_PhaseOffset < 0", migrationSystemSource);
        Assert.Contains("member.m_PhaseOffset < -300 || member.m_PhaseOffset > 300", migrationJobsSource);
        Assert.Contains("member.m_PhaseOffset < -300 || member.m_PhaseOffset > 300", migrationSystemSource);
    }

    [Fact]
    public void Local_tsp_runtime_rejects_all_traffic_group_members()
    {
        string source = File.ReadAllText(GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficLightSystems",
            "Simulation",
            "TransitSignalPriorityRuntime.cs"));
        string eligibilitySource = ExtractMethod(source, "private static bool IsRuntimeEligibleJunction");

        Assert.Contains(
            "return !job.m_ExtraTypeHandle.m_TrafficGroupMember.HasComponent(junctionEntity);",
            eligibilitySource);
        Assert.DoesNotContain("m_IsGroupLeader", eligibilitySource);
    }

    [Fact]
    public void Tsp_approach_index_eligibility_rejects_group_members_for_trams_and_buses()
    {
        string source = File.ReadAllText(GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficLightSystems",
            "Simulation",
            "PatchedTrafficLightSystem.cs"));
        string jobSource = ExtractSection(
            source,
            "private struct HasApproachIndexEligibleTransitSignalPrioritySettingsJob",
            "public override int GetUpdateInterval");

        Assert.Contains("bool isGroupedIntersection = m_TrafficGroupMemberLookup.HasComponent(entity);", jobSource);
        Assert.Contains("TspPolicy.IsApproachIndexEligibleSetting(logicSettings, isGroupedIntersection)", jobSource);
        Assert.Contains("TspPolicy.IsBusApproachIndexEligibleSetting(logicSettings, isGroupedIntersection)", jobSource);
        Assert.DoesNotContain("m_IsGroupLeader", jobSource);
    }

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
    public void Grouped_base_state_machine_does_not_bypass_native_container_safety()
    {
        string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
        string jobSource = ExtractSection(
            source,
            "public struct UpdateTrafficLightsJob",
            "private const uint UPDATE_INTERVAL");
        string onUpdate = ExtractMethod(source, "protected override void OnUpdate");

        Assert.DoesNotContain("NativeDisableContainerSafetyRestriction", jobSource);
        Assert.DoesNotContain("JobChunkExtensions.ScheduleParallel(", onUpdate);
        Assert.Equal(3, CountOccurrences(onUpdate, "JobChunkExtensions.Schedule("));
    }

    [Fact]
    public void Traffic_group_system_builds_movement_maps_from_live_lane_signals()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string buildSignatures = ExtractMethod(
            source,
            "private TrafficGroupPhaseSignature[] BuildPhaseSignatures");
        string refreshMappings = ExtractMethod(
            source,
            "private void RefreshMovementMappings");

        Assert.Contains("NodeUtils.GetLaneConnectionMap", buildSignatures);
        Assert.Contains("laneSignal.m_GroupMask", buildSignatures);
        Assert.Contains("extraLaneSignal.m_YieldGroupMask", buildSignatures);
        Assert.Contains("laneConnection.m_SourceEdge", buildSignatures);
        Assert.Contains("laneConnection.m_DestEdge", buildSignatures);
        Assert.Contains("GetEdgePositionForJunction", buildSignatures);
        Assert.Contains("carLaneLookup", buildSignatures);
        Assert.Contains("trackLaneLookup", buildSignatures);
        Assert.Contains(
            "TrafficGroupMovementMappingPolicy.QuantizeUndirectedAxis",
            buildSignatures);
        Assert.Contains(
            "TrafficGroupMovementMappingPolicy.TryBuild",
            refreshMappings);
        Assert.Contains("TrafficGroupPhaseMapping", refreshMappings);
        Assert.Contains(
            "EntityManager.RemoveComponent<TrafficGroupPhaseMapping>",
            refreshMappings);
    }

    [Fact]
    public void Traffic_group_system_logs_changed_mapping_failures_with_phase_signatures()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string refreshSource = ExtractMethod(source, "private void RefreshMovementMappings");
        string logSource = ExtractMethod(source, "private void LogMovementMappingFailureIfChanged");
        string formatSource = ExtractMethod(source, "private static string FormatPhaseSignatures");

        Assert.Contains(
            "out TrafficGroupMovementMappingFailure mappingFailure",
            refreshSource);
        Assert.Contains("LogMovementMappingFailureIfChanged", refreshSource);
        Assert.Contains("leaderSignatures", refreshSource);
        Assert.Contains("memberSignatures", refreshSource);
        Assert.Contains(
            "m_LastMovementMappingFailureReports.TryGetValue",
            logSource);
        Assert.Contains("m_Log.Warn", logSource);
        Assert.Contains("ToDiagnosticString", formatSource);
    }

    [Fact]
    public void Lockstep_diagnostic_state_is_runtime_only_and_follows_diagnostic_toggle()
    {
        string componentSource = File.ReadAllText(GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Components",
            "TrafficGroupLockstepDebugState.cs"));
        string groupSource = File.ReadAllText(GetTrafficGroupSystemPath());
        string onUpdate = ExtractMethod(groupSource, "protected override void OnUpdate");
        string maintenance = ExtractMethod(
            groupSource,
            "private void MaintainLockstepDiagnosticsComponents");

        Assert.Contains(
            "struct TrafficGroupLockstepDebugState : IComponentData",
            componentSource);
        Assert.DoesNotContain("ISerializable", componentSource);
        Assert.DoesNotContain("Serialize<", componentSource);
        Assert.DoesNotContain("Deserialize<", componentSource);
        Assert.Contains(
            "Mod.m_Setting.m_ShowTransitSignalPriorityDiagnostics",
            onUpdate);
        Assert.Contains("MaintainLockstepDiagnosticsComponents", onUpdate);
        Assert.Contains("m_MemberQuery.ToEntityArray", maintenance);
        Assert.Contains("EntityManager.AddComponentData", maintenance);
        Assert.Contains("default(TrafficGroupLockstepDebugState)", maintenance);
        Assert.Contains(
            "EntityManager.RemoveComponent<TrafficGroupLockstepDebugState>",
            maintenance);
    }

    [Fact]
    public void Lockstep_simulation_records_every_boundary_without_mutating_gameplay_state()
    {
        string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
        string execute = ExtractSection(
            source,
            "public void Execute(in ArchetypeChunk chunk",
            "private void FillLaneSignals");
        string helper = File.ReadAllText(GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficLightSystems",
            "Simulation",
            "TrafficGroupLockstepRuntimeDiagnostics.cs"));
        string extraTypeHandle = File.ReadAllText(GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficLightSystems",
            "Simulation",
            "ExtraTypeHandle.cs"));

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
        Assert.Contains("m_TransitSignalPriorityDiagnosticsEnabled", execute);
        Assert.Contains(
            "ComponentLookup<TrafficGroupLockstepDebugState>",
            extraTypeHandle);
        Assert.Contains("isReadOnly: false", extraTypeHandle);
        Assert.Contains("TrafficGroupLockstepDiagnostics.AddHash", helper);
        Assert.DoesNotContain("m_LaneSignalData[", helper);
        Assert.DoesNotContain("m_TrafficLightData[", helper);
        Assert.DoesNotContain("ref TrafficLights", helper);
        Assert.DoesNotContain("ref LaneSignal", helper);
        Assert.DoesNotContain("ref TrafficLight", helper);
    }

    [Fact]
    public void Lockstep_simulation_preserves_cross_shard_evidence_until_next_matching_pass()
    {
        string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
        string execute = ExtractSection(
            source,
            "public void Execute(in ArchetypeChunk chunk",
            "private void FillLaneSignals");
        string component = File.ReadAllText(GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Components",
            "TrafficGroupLockstepDebugState.cs"));

        Assert.DoesNotContain(
            "if (lockstepDebug.SimulationFrame != m_SimulationFrame)",
            execute);
        Assert.Contains("IndependentSimulationFrame", component);
        Assert.Contains("IndependentBefore", component);
        Assert.Contains("IndependentAfter", component);
        Assert.Contains("lockstepDebug.IndependentBefore", execute);
        Assert.Contains("lockstepDebug.IndependentAfter", execute);
        Assert.DoesNotContain(
            "lockstepDebug.Before = independentBefore",
            execute);
        Assert.DoesNotContain(
            "lockstepDebug.After = independentAfter",
            execute);
    }

    [Fact]
    public void Leader_update_shard_routes_all_group_members_without_discovery_map()
    {
        string groupSource = File.ReadAllText(GetTrafficGroupSystemPath());
        string refreshRuntimeState = ExtractMethod(
            groupSource,
            "private void RefreshGroupRuntimeState");
        string simulationSource = File.ReadAllText(GetPatchedTrafficLightSystemPath());
        string onCreate = ExtractMethod(simulationSource, "protected override void OnCreate");
        string onUpdate = ExtractMethod(simulationSource, "protected override void OnUpdate");
        string execute = ExtractSection(
            simulationSource,
            "public void Execute(in ArchetypeChunk chunk",
            "private void FillLaneSignals");

        Assert.Contains("m_GroupedTrafficLightQuery = GetEntityQuery", onCreate);
        Assert.Contains("ComponentType.ReadOnly<TrafficGroupMember>()", onCreate);
        Assert.Contains(
            "EntityManager.TryGetSharedComponent<UpdateFrame>",
            refreshRuntimeState);
        Assert.Contains("updateFrame.m_Index", refreshRuntimeState);
        Assert.Contains("TrafficGroupRuntimeData", refreshRuntimeState);
        Assert.Contains("m_UpdateFrameIndex = updateFrameIndex", onUpdate);
        Assert.Contains("IsActiveCoordinatedGroup", execute);
        Assert.DoesNotContain("DiscoverActiveGroupedBaseDemandJob", simulationSource);
        Assert.DoesNotContain("m_ActiveGroupedBaseDemand", simulationSource);
        Assert.DoesNotContain("activeGroupedBaseDemand", simulationSource);

        int collectPass = onUpdate.IndexOf(
            "TrafficLightUpdatePass.CollectGroupedBaseDemand",
            StringComparison.Ordinal);
        int leaderPass = onUpdate.IndexOf(
            "TrafficLightUpdatePass.UpdateLeadersAndIndependent",
            StringComparison.Ordinal);
        int followerPass = onUpdate.IndexOf(
            "TrafficLightUpdatePass.SynchronizeGroupedBaseFollowers",
            StringComparison.Ordinal);

        Assert.Contains("m_GroupedTrafficLightQuery", onUpdate.Substring(collectPass, leaderPass - collectPass));
        Assert.Contains("m_TrafficLightQuery", onUpdate.Substring(leaderPass, followerPass - leaderPass));
        Assert.Contains("m_GroupedTrafficLightQuery", onUpdate.Substring(followerPass));
    }

    [Fact]
    public void Grouped_base_demand_is_consumed_only_in_collection_pass()
    {
        string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
        string execute = ExtractSection(
            source,
            "public void Execute(in ArchetypeChunk chunk",
            "private void FillLaneSignals");

        Assert.Contains("CollectAndResetGroupedBaseDemand", execute);
        Assert.Contains("m_LocalGroupedDemand", execute);
        Assert.Contains("m_GroupedDemand", execute);
        Assert.Contains("UseCollectedDemand", execute);

        string collectMethod = ExtractMethod(source, "private void CollectAndResetGroupedBaseDemand");
        string updateMethod = ExtractMethod(source, "private bool UpdateTrafficLightState");
        Assert.Contains("laneSignal.m_Petitioner = Entity.Null", collectMethod);
        Assert.Contains("laneSignal.m_Priority = laneSignal.m_Default", collectMethod);
        Assert.Contains("if (!demandSource.UseCollectedDemand)", updateMethod);
        Assert.Contains("ClearPriority(laneSignals)", updateMethod);
    }

    [Fact]
    public void Missing_same_tick_master_holds_lockstep_follower()
    {
        string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
        string execute = ExtractSection(
            source,
            "public void Execute(in ArchetypeChunk chunk",
            "private void FillLaneSignals");

        Assert.Contains("TryGetValue(groupEntity, out var masterState)", execute);
        Assert.DoesNotContain("UpdateGroupedBaseFollowerIndependently", execute);
    }

    [Fact]
    public void Leader_without_complete_map_reuses_collected_local_demand()
    {
        string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
        string execute = ExtractSection(
            source,
            "public void Execute(in ArchetypeChunk chunk",
            "private void FillLaneSignals");

        Assert.Contains("HasCompletePhaseMapping", execute);
        Assert.Contains("GetLocalGroupedDemand", execute);
        Assert.Contains(
            "publishSameTickMaster = hasCompleteLeaderMapping",
            execute);
    }

    [Fact]
    public void Custom_followers_use_the_same_tick_leader_then_follower_path()
    {
        string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
        string execute = ExtractSection(
            source,
            "public void Execute(in ArchetypeChunk chunk",
            "private void FillLaneSignals");

        Assert.Contains(
            "bool isCoordinatedMember = TryGetCoordinatedMember(",
            execute);
        Assert.Contains(
            "bool isCoordinatedBaseMember = isCoordinatedMember && !usesCustomPhase;",
            execute);
        Assert.Contains(
            "bool deferCoordinatedFollower = isCoordinatedMember",
            execute);
        Assert.Contains(
            "bool canSynchronizeFollower = usesCustomPhase",
            execute);
        Assert.Contains("CustomStateMachine.SyncSignalGroupWithLeader", execute);
        Assert.Contains(
            "PublishSameTickMasterState(groupEntity, trafficLights, customTrafficLights);",
            execute);
    }

    [Fact]
    public void Follower_sync_uses_physical_map_and_preserves_optional_zero()
    {
        string source = File.ReadAllText(GetCustomStateMachinePath());
        string syncSource = ExtractMethod(source, "public static void SyncSignalGroupWithLeader");
        string patchedSource = File.ReadAllText(GetPatchedTrafficLightSystemPath());
        string execute = ExtractSection(
            patchedSource,
            "public void Execute(in ArchetypeChunk chunk",
            "private void FillLaneSignals");

        Assert.Contains(
            "m_TrafficGroupPhaseMapping",
            syncSource);
        Assert.Contains(
            "TryMapLeaderToMember",
            syncSource);
        Assert.Matches(
            new Regex(
                @"m_CurrentSignalGroup\s*=.*?mappedPhase.*?m_NextSignalGroup\s*=.*?mappedNext",
                RegexOptions.Singleline),
            syncSource);
        Assert.Contains("masterState.NextSignalGroup == 0", syncSource);
        Assert.DoesNotContain("UpdateGroupedBaseFollowerIndependently", execute);
        Assert.DoesNotContain("MapRequiredOneBasedPhase", syncSource);
        Assert.DoesNotContain("MapOptionalOneBasedPhase", syncSource);
        Assert.DoesNotContain("WrapPhase", syncSource);
    }

    [Fact]
    public void Coordination_containers_do_not_use_temp_job_allocator()
    {
        string source = File.ReadAllText(GetPatchedTrafficLightSystemPath());
        string onUpdate = ExtractMethod(source, "protected override void OnUpdate");
        string allocations = ExtractSection(
            onUpdate,
            "var localGroupedDemand",
            "var updateJob");

        Assert.Contains("Allocator.Persistent", allocations);
        Assert.DoesNotContain("Allocator.TempJob", allocations);
    }

    [Fact]
    public void Traffic_group_diagnostics_report_physical_mapping_or_unavailable()
    {
        string source = File.ReadAllText(GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "UI",
            "UISystem.UIBIndings.cs"));
        string formatter = ExtractMethod(
            source,
            "private string FormatTrafficGroupMasterPhase");

        Assert.Contains("TrafficGroupPhaseMapping", formatter);
        Assert.Contains("TryMapLeaderToMember", formatter);
        Assert.Contains("Movement mapping unavailable; follower held", formatter);
        Assert.DoesNotContain("selected G", formatter);
    }

    private static string GetTrafficGroupSystemPath()
    {
        return GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficGroupSystem.cs");
    }

    private static string GetCustomStateMachinePath()
    {
        return GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficLightSystems",
            "Simulation",
            "CustomStateMachine.cs");
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

    private static string ExtractSection(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find section start: {startMarker}");

        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find section end: {endMarker}");

        return source.Substring(start, end - start);
    }

    private static string GetRepositorySourcePath(params string[] segments)
    {
        string baseDirectory = AppContext.BaseDirectory;
        string[] pathSegments = new string[segments.Length + 5];
        pathSegments[0] = baseDirectory;
        pathSegments[1] = "..";
        pathSegments[2] = "..";
        pathSegments[3] = "..";
        pathSegments[4] = "..";
        Array.Copy(segments, 0, pathSegments, 5, segments.Length);
        string path = Path.GetFullPath(Path.Combine(pathSegments));

        Assert.True(File.Exists(path), $"Could not find source file at {path}");
        return path;
    }

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find method signature: {signature}");

        int braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"Could not find method body: {signature}");

        int depth = 0;
        for (int i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(start, i - start + 1);
                }
            }
        }

        throw new InvalidOperationException($"Could not parse method body: {signature}");
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int start = 0;
        while ((start = source.IndexOf(value, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += value.Length;
        }

        return count;
    }
}
