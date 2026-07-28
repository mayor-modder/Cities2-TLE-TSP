using System;
using System.IO;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests;

public sealed class TrafficGroupCustomPhaseInitializationSourceTests
{
    [Fact]
    public void Member_initialization_adds_timing_structure_without_clearing_local_masks()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string initializer = ExtractMethod(
            source,
            "public void EnsureMemberCustomPhaseSetup");

        Assert.Contains("new CustomPhaseData()", initializer);
        Assert.Contains(
            "m_MinimumDuration = leaderPhase.m_MinimumDuration",
            initializer);
        Assert.Contains(
            "m_MaximumDuration = leaderPhase.m_MaximumDuration",
            initializer);
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

    [Fact]
    public void Pattern_propagation_preserves_member_movement_masks()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string propagation = ExtractMethod(
            source,
            "public void PropagatePatternToMembers");

        Assert.DoesNotContain("CopyEdgeGroupMask", propagation);
        Assert.DoesNotContain("CopySubLaneGroupMask", propagation);
        Assert.Contains(
            "EnsureMemberCustomPhaseSetup(groupEntity, memberEntity)",
            propagation);
    }

    [Fact]
    public void Missing_phase_migration_uses_topology_local_initialization()
    {
        string migration = File.ReadAllText(GetMigrationSystemPath());

        Assert.Contains(
            "EnsureMemberCustomPhaseSetup(groupEntity, memberEntity)",
            migration);
        Assert.DoesNotContain("TryCopyPhasesToJunction", migration);
        Assert.DoesNotContain("copyFromLeader", migration);
        Assert.DoesNotContain("edgeMasks.Clear()", migration);
        Assert.DoesNotContain("subLaneMasks.Clear()", migration);
    }

    [Fact]
    public void Main_panel_payload_exposes_phase_setup_completeness()
    {
        string source = File.ReadAllText(GetUiBindingsPath());

        Assert.Contains("phaseSetupComplete =", source);
        Assert.Contains("phaseMapping.m_Map.IsComplete", source);
    }

    private static string GetTrafficGroupSystemPath() =>
        GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficGroupSystem.cs");

    private static string GetMigrationSystemPath() =>
        GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "Serialization",
            "TLEDataMigrationSystem.cs");

    private static string GetUiBindingsPath() =>
        GetRepositorySourcePath(
            "TrafficLightsEnhancement",
            "Systems",
            "UI",
            "UISystem.UIBIndings.cs");

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

        throw new InvalidOperationException(
            $"Could not parse method body: {signature}");
    }
}
