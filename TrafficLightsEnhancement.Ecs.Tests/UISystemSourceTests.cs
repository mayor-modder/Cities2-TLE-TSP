using System;
using System.IO;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests;

public sealed class UISystemSourceTests
{
    [Fact]
    public void ChangeSelectedEntity_assigns_entity_and_custom_lights_before_updating_bindings()
    {
        string source = File.ReadAllText(GetRepoPath("TrafficLightsEnhancement", "Systems", "UI", "UISystem.cs"));
        string changeSelectedSource = ExtractMethod(source, "public void ChangeSelectedEntity");

        int selectedEntityAssignIndex = changeSelectedSource.IndexOf("m_SelectedEntity = entity;", StringComparison.Ordinal);
        int customTrafficLightsAssignIndex = changeSelectedSource.IndexOf("m_CustomTrafficLights =", StringComparison.Ordinal);
        int updateEdgeInfoIndex = changeSelectedSource.IndexOf("UpdateEdgeInfo(entity);", StringComparison.Ordinal);
        int setMainPanelIndex = changeSelectedSource.IndexOf("SetMainPanelState(MainPanelState.Main);", StringComparison.Ordinal);

        Assert.True(selectedEntityAssignIndex >= 0, "m_SelectedEntity assignment was not found.");
        Assert.True(customTrafficLightsAssignIndex >= 0, "m_CustomTrafficLights assignment was not found.");
        Assert.True(updateEdgeInfoIndex >= 0, "UpdateEdgeInfo(entity) was not found.");
        Assert.True(setMainPanelIndex >= 0, "SetMainPanelState(MainPanelState.Main) was not found.");

        Assert.True(selectedEntityAssignIndex < updateEdgeInfoIndex, "m_SelectedEntity must be assigned before UpdateEdgeInfo(entity).");
        Assert.True(customTrafficLightsAssignIndex < updateEdgeInfoIndex, "m_CustomTrafficLights must be assigned before UpdateEdgeInfo(entity).");
        Assert.True(updateEdgeInfoIndex < setMainPanelIndex, "UpdateEdgeInfo(entity) must occur before SetMainPanelState(MainPanelState.Main).");
    }

    [Fact]
    public void GetSelectedJunctionDiagnosticsSnapshot_gates_tram_tsp_visibility_by_tram_track_presence()
    {
        string source = File.ReadAllText(GetRepoPath("TrafficLightsEnhancement", "Systems", "UI", "UISystem.UIBIndings.cs"));
        string getSnapshotSource = ExtractMethod(source, "private SelectedJunctionDiagnosticsSnapshot GetSelectedJunctionDiagnosticsSnapshot");

        Assert.Contains("TramTransitPriority = new SelectedJunctionTspControlSnapshot(", getSnapshotSource);
        Assert.Contains("isVisible: HasTramTrack(edgeInfoArray),", getSnapshotSource);
        Assert.Contains("private bool HasTramTrack(NativeArray<NodeUtils.EdgeInfo> edgeInfoArray)", source);
    }

    [Fact]
    public void GetSelectedJunctionDiagnosticsSnapshot_gates_bus_tsp_visibility_by_car_lane_presence()
    {
        string source = File.ReadAllText(GetRepoPath("TrafficLightsEnhancement", "Systems", "UI", "UISystem.UIBIndings.cs"));
        string getSnapshotSource = ExtractMethod(source, "private SelectedJunctionDiagnosticsSnapshot GetSelectedJunctionDiagnosticsSnapshot");

        Assert.Contains("BusTransitPriority = new SelectedJunctionTspControlSnapshot(", getSnapshotSource);
        Assert.Contains("isVisible: hasCarLane,", getSnapshotSource);
    }

    [Fact]
    public void GetSelectedJunctionDiagnosticsSnapshot_gates_vehicle_turn_options_by_car_lane_presence()
    {
        string source = File.ReadAllText(GetRepoPath("TrafficLightsEnhancement", "Systems", "UI", "UISystem.UIBIndings.cs"));
        string getSnapshotSource = ExtractMethod(source, "private SelectedJunctionDiagnosticsSnapshot GetSelectedJunctionDiagnosticsSnapshot");

        Assert.Contains("bool hasCarLane = PredefinedPatternsProcessor.HasCarLane(edgeInfoArray);", getSnapshotSource);
        Assert.Contains(
            "bool vehicleTurnOptionsVisible = PredefinedPatternsProcessor.IsVehicleTurnOptionVisible(extraOptionsVisible, hasCarLane);",
            getSnapshotSource);
    }

    [Fact]
    public void SelectedJunctionDiagnosticsSnapshot_exposes_car_lane_counts_to_prove_the_gate()
    {
        string source = File.ReadAllText(GetRepoPath("TrafficLightsEnhancement", "Systems", "UI", "UISystem.UIBIndings.cs"));
        string getSnapshotSource = ExtractMethod(source, "private SelectedJunctionDiagnosticsSnapshot GetSelectedJunctionDiagnosticsSnapshot");

        Assert.Contains("HasCarLane = hasCarLane,", getSnapshotSource);
        Assert.Contains("TotalCarLaneCount = totalCarLaneCount,", getSnapshotSource);
        Assert.Contains("hasCarLane = HasCarLane,", source);
        Assert.Contains("totalCarLaneCount = TotalCarLaneCount,", source);
    }

    [Fact]
    public void Traffic_group_trace_exposes_read_only_group_timing_fields()
    {
        string source = File.ReadAllText(GetRepoPath("TrafficLightsEnhancement", "Systems", "UI", "UISystem.UIBIndings.cs"));
        string traceSource = ExtractMethod(source, "private object GetTspTrafficGroupTrace");

        Assert.Contains("role =", traceSource);
        Assert.Contains("mode =", traceSource);
        Assert.Contains("tspSuspended = true", traceSource);
        Assert.Contains("cycleLength = group.m_CycleLength", traceSource);
        Assert.Contains("memberCycleTimer = member.m_MemberCycleTimer", traceSource);
        Assert.Contains("phaseOffset = member.m_PhaseOffset", traceSource);
        Assert.Contains("masterPhase = group.m_MasterPhase", traceSource);
        Assert.Contains("masterNextPhase = group.m_MasterNextPhase", traceSource);
        Assert.Contains("masterState = group.m_MasterState.ToString()", traceSource);
        Assert.Contains("masterTimer = group.m_MasterTimer", traceSource);
        Assert.Contains("masterCustomTimer = group.m_MasterCustomTimer", traceSource);
        Assert.Contains("masterSignalGroupCount = group.m_MasterSignalGroupCount", traceSource);
    }

    [Fact]
    public void Diagnostics_trace_captures_actual_leader_state_with_the_selected_follower()
    {
        string source = File.ReadAllText(GetRepoPath(
            "TrafficLightsEnhancement",
            "Systems",
            "UI",
            "UISystem.UIBIndings.cs"));
        string writerSource = ExtractMethod(
            source,
            "private void WriteTspDiagnosticsTraceEvent");
        string leaderTraceSource = ExtractMethod(
            source,
            "private object GetTspTrafficGroupLeaderTrace");

        Assert.Contains(
            "leaderTrafficLights = GetTspTrafficGroupLeaderTrace(entity)",
            writerSource);
        Assert.Contains("member.m_LeaderEntity", leaderTraceSource);
        Assert.Contains(
            "EntityManager.TryGetComponent(leaderEntity, out TrafficLights leaderLights)",
            leaderTraceSource);
        Assert.Contains("state = leaderLights.m_State.ToString()", leaderTraceSource);
        Assert.Contains("currentGroup = leaderLights.m_CurrentSignalGroup", leaderTraceSource);
        Assert.Contains("nextGroup = leaderLights.m_NextSignalGroup", leaderTraceSource);
        Assert.Contains("updateFrameIndex = GetUpdateFrameIndex(leaderEntity)", leaderTraceSource);
    }

    [Fact]
    public void Lockstep_trace_expands_every_member_and_every_output_boundary()
    {
        string writer = File.ReadAllText(GetRepoPath(
            "TrafficLightsEnhancement",
            "Systems",
            "UI",
            "UISystem.UIBIndings.cs"));
        string lockstep = File.ReadAllText(GetRepoPath(
            "TrafficLightsEnhancement",
            "Systems",
            "UI",
            "UISystem.TrafficGroupLockstepDiagnostics.cs"));
        string writerSource = ExtractMethod(
            writer,
            "private void WriteTspDiagnosticsTraceEvent");
        string groupTraceSource = ExtractMethod(
            lockstep,
            "private object GetTrafficGroupLockstepTrace");
        string memberTraceSource = ExtractMethod(
            lockstep,
            "private object GetTrafficGroupLockstepMemberTrace");
        string renderedTraceSource = ExtractMethod(
            lockstep,
            "private ArrayList GetRenderedTrafficLightTrace");
        string warningSource = ExtractMethod(
            lockstep,
            "private void WarnLockstepVerdictIfChanged");

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

        Assert.Contains("subObject.m_SubObject", renderedTraceSource);
        Assert.Contains("rendered.m_GroupMask0", renderedTraceSource);
        Assert.Contains("rendered.m_GroupMask1", renderedTraceSource);
        Assert.Contains("rendered.m_State.ToString()", renderedTraceSource);

        string laneTraceSource = ExtractMethod(
            writer,
            "private ArrayList GetTspLaneSignalTrace");
        Assert.Contains("laneSignal.m_Petitioner", laneTraceSource);
        Assert.Contains("laneSignal.m_Blocker", laneTraceSource);
        Assert.Contains("laneSignal.m_Priority", laneTraceSource);
        Assert.Contains("laneSignal.m_Default", laneTraceSource);
        Assert.Contains("laneSignal.m_Flags.ToString()", laneTraceSource);
        Assert.DoesNotContain(
            "$\"|{debugState.SimulationFrame}",
            warningSource);
    }

    private static string GetRepoPath(params string[] segments)
    {
        string path = AppContext.BaseDirectory;
        for (int i = 0; i < 4; i++)
        {
            path = Path.GetDirectoryName(path) ?? throw new DirectoryNotFoundException(path);
        }

        path = Path.Combine(path, Path.Combine(segments));
        Assert.True(File.Exists(path), $"Could not find expected repo file at {path}");
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
}
