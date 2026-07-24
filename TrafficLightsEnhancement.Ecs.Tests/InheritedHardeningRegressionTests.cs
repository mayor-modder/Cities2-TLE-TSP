using System;
using System.IO;
using System.Text.RegularExpressions;
using C2VM.TrafficLightsEnhancement.Utils;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests;

public sealed class InheritedHardeningRegressionTests
{
    [Fact]
    public void User_presets_clamp_malformed_durations_before_converting_to_phase_config()
    {
        var preset = new UserPreset
        {
            MinDuration = -10,
            MaxDuration = 700
        };

        var config = preset.ToConfig();

        Assert.Equal(2, config.MinDuration);
        Assert.Equal(300, config.MaxDuration);

        preset.MinDuration = 40;
        preset.MaxDuration = 5;

        config = preset.ToConfig();

        Assert.Equal(40, config.MinDuration);
        Assert.Equal(40, config.MaxDuration);
    }

    [Fact]
    public void Migration_system_disposes_pending_missing_phase_dialog_groups_on_destroy()
    {
        string source = File.ReadAllText(GetRepoPath(
            "TrafficLightsEnhancement",
            "Systems",
            "Serialization",
            "TLEDataMigrationSystem.cs"));
        string onDestroySource = ExtractMethod(source, "protected override void OnDestroy");

        Assert.Contains("_affectedGroupsForMigration.IsCreated", onDestroySource);
        Assert.Contains("_affectedGroupsForMigration.Dispose()", onDestroySource);
        Assert.True(
            onDestroySource.IndexOf("_affectedGroupsForMigration.Dispose()", StringComparison.Ordinal)
                < onDestroySource.IndexOf("base.OnDestroy()", StringComparison.Ordinal),
            "Pending migration dialog data should be disposed before base.OnDestroy().");
    }

    [Fact]
    public void Inherited_lane_system_scaffold_remains_unscheduled_and_disabled_by_tle()
    {
        string modSource = File.ReadAllText(GetRepoPath("TrafficLightsEnhancement", "Mod.cs"));
        string scaffoldSource = File.ReadAllText(GetRepoPath("CommonLibraries", "LaneSystem", "C2VMPatchedLaneSystem.cs"));
        string onLoadSource = ExtractMethod(modSource, "public void OnLoad");
        string activeOnLoadSource = RemoveLineComments(onLoadSource);
        string onUpdateSource = ExtractMethod(scaffoldSource, "protected override void OnUpdate");

        Assert.Contains("GetOrCreateSystemManaged<Game.Net.C2VMPatchedLaneSystem>().Enabled = false", activeOnLoadSource);
        Assert.DoesNotContain("UpdateBefore<Game.Net.C2VMPatchedLaneSystem", activeOnLoadSource);
        Assert.DoesNotContain("UpdateAt<Game.Net.C2VMPatchedLaneSystem", activeOnLoadSource);
        Assert.Matches(
            new Regex(@"protected\s+override\s+void\s+OnUpdate\s*\(\s*\)\s*\{\s*\}", RegexOptions.Singleline),
            onUpdateSource);
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

    private static string RemoveLineComments(string source)
    {
        return Regex.Replace(source, @"^\s*//.*$", string.Empty, RegexOptions.Multiline);
    }
}
