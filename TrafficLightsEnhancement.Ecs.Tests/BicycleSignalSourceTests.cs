using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests;

public sealed class BicycleSignalSourceTests
{
    [Fact]
    public void Edge_info_counts_dedicated_bicycle_road_lanes_from_prefab_road_types()
    {
        string source = File.ReadAllText(GetRepoPath("TrafficLightsEnhancement", "Utils", "NodeUtils.cs"));

        Assert.Contains("IsBicycleOnlyRoadLane", source);
        Assert.Contains("RoadTypes.Bicycle", source);
        Assert.Contains("RoadTypes.Car", source);
        Assert.Matches(
            new Regex(@"isBicycleLane\s*=\s*isSecondaryBicycleLane\s*\|\|\s*isBicycleOnlyRoadLane", RegexOptions.Singleline),
            source);
    }

    [Fact]
    public void Custom_phase_processor_applies_bicycle_mask_to_dedicated_bicycle_road_lanes()
    {
        string source = File.ReadAllText(GetRepoPath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficLightSystems",
            "Initialisation",
            "CustomPhaseProcessor.cs"));

        string processSource = ExtractMethod(source, "public static void ProcessLanes");

        Assert.Contains("IsBicycleOnlyRoadLane", processSource);
        Assert.Contains("groupMask.m_Bicycle.m_GoGroupMask", processSource);
        Assert.Matches(
            new Regex(@"bicycleGoGroupMask\s*=\s*groupMask\.m_Bicycle\.m_GoGroupMask", RegexOptions.Singleline),
            processSource);
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
