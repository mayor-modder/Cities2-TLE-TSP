using System;
using System.IO;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests;

public sealed class PredefinedPatternOptionSupportSourceTests
{
    [Fact]
    public void Extra_options_share_topology_gate_with_simple_predefined_patterns()
    {
        string source = File.ReadAllText(GetRepoPath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficLightSystems",
            "Initialisation",
            "PredefinedPatternsProcessor.cs"));
        string supportMethod = ExtractMethod(source, "public static bool AreExtraOptionsSupported");

        Assert.Contains("!HasTrainTrack(edgeInfoArray)", supportMethod);
        Assert.Contains("edgeInfoArray.Length <= 7", supportMethod);
    }

    [Fact]
    public void Split_phasing_protected_left_uses_protected_turn_topology_gate()
    {
        string source = File.ReadAllText(GetRepoPath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficLightSystems",
            "Initialisation",
            "PredefinedPatternsProcessor.cs"));
        string validPatternSource = ExtractMethod(source, "public static bool IsValidPattern");

        Assert.Contains("case (uint)CustomTrafficLights.Patterns.SplitPhasingProtectedLeft:", validPatternSource);
        Assert.Contains("IsValidPattern(edgeInfoArray, CustomTrafficLights.Patterns.ProtectedCentreTurn)", validPatternSource);
    }

    [Fact]
    public void Main_panel_hides_extra_options_when_topology_gate_fails()
    {
        string source = File.ReadAllText(GetRepoPath(
            "TrafficLightsEnhancement",
            "Systems",
            "UI",
            "UISystem.UIBIndings.cs"));
        string mainPanelSource = ExtractMethod(source, "protected string GetMainPanel");

        Assert.Contains("PredefinedPatternsProcessor.AreExtraOptionsSupported(m_EdgeInfoDictionary[m_SelectedEntity])", mainPanelSource);
        Assert.DoesNotContain("bool showOptions = patternOnly < (uint)CustomTrafficLights.Patterns.ModDefault && !hasTrainTrack", mainPanelSource);
    }

    [Fact]
    public void Initialization_clears_extra_options_when_topology_gate_fails()
    {
        string source = File.ReadAllText(GetRepoPath(
            "TrafficLightsEnhancement",
            "Systems",
            "TrafficLightSystems",
            "Initialisation",
            "PatchedTrafficLightInitializationSystem.cs"));

        Assert.Contains("bool extraOptionsSupported = PredefinedPatternsProcessor.AreExtraOptionsSupported(edgeInfoArray)", source);
        Assert.Contains("customTrafficLights.SetPattern(PredefinedPatternsProcessor.ClearExtraOptions(pattern))", source);
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
