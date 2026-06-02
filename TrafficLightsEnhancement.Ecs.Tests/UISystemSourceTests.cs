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
