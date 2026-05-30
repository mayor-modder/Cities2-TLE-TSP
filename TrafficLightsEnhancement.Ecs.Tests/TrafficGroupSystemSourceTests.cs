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
    public void Green_wave_paths_use_shared_timing_policy()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string calculateGreenWaveSource = ExtractMethod(source, "public void CalculateGreenWaveTiming");
        string applyCoordinationSource = ExtractMethod(source, "private void ApplyCoordination");
        string enhancedGreenWaveSource = ExtractMethod(source, "public void CalculateEnhancedGreenWaveTiming");
        string forceSyncSource = ExtractMethod(source, "public void ForceSyncToLeader");
        string wrapPhaseSource = ExtractMethod(source, "private static int WrapPhase");

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
            "TrafficGroupTimingPolicy.WrapOneBasedPhase(phase, phaseCount)",
            wrapPhaseSource);
    }

    private static string GetTrafficGroupSystemPath()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string path = Path.GetFullPath(Path.Combine(
            baseDirectory,
            "..",
            "..",
            "..",
            "..",
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficGroupSystem.cs"));

        Assert.True(File.Exists(path), $"Could not find TrafficGroupSystem.cs at {path}");
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
