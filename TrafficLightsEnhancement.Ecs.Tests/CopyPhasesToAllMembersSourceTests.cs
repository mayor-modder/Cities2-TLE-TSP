using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests;

// Guards the fix for issue #131: "Copy phases to all members" must show a single
// consolidated dialog instead of one "Phase sync not allowed" modal per incompatible
// target. The per-target copy must be UI-free so it can be looped without spamming
// dialogs (used by both the batch path and the load-time migration).
public sealed class CopyPhasesToAllMembersSourceTests
{
    [Fact]
    public void Per_target_copy_core_shows_no_dialog()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string core = ExtractMethod(source, "public bool TryCopyPhasesToJunction");

        Assert.DoesNotContain("ShowMessageDialog", core);
        Assert.Contains("ValidatePhaseSyncCompatibility", core);
    }

    [Fact]
    public void Single_target_copy_delegates_to_core_and_keeps_its_dialog()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string single = ExtractMethod(source, "public bool CopyPhasesToJunction");

        Assert.Contains("TryCopyPhasesToJunction(", single);
        Assert.Contains("ShowMessageDialog", single);
    }

    [Fact]
    public void Copy_to_all_members_shows_exactly_one_dialog_and_loops_via_core()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string batch = ExtractMethod(source, "public void CopyPhasesToAllMembers");

        Assert.Contains("TryCopyPhasesToJunction(", batch);
        Assert.Single(Regex.Matches(batch, "ShowMessageDialog"));
        // Members come from a Temp NativeList that must be released.
        Assert.Contains(".Dispose()", batch);
    }

    [Fact]
    public void Missing_phases_migration_copies_without_a_dialog()
    {
        string source = File.ReadAllText(GetMigrationSystemPath());
        string handler = ExtractMethod(source, "private void OnMissingPhasesDialogResult");

        // Must use the UI-free core so load-time copy-from-leader cannot pop per-member dialogs.
        Assert.Contains("TryCopyPhasesToJunction(leaderEntity, memberEntity", handler);
    }

    [Fact]
    public void UISystem_registers_a_single_copy_to_all_members_trigger()
    {
        string source = File.ReadAllText(GetUiBindingsPath());

        Assert.Contains("CreateTrigger<string>(\"CallCopyPhasesToAllMembers\", CallCopyPhasesToAllMembers);", source);
        Assert.Contains("protected void CallCopyPhasesToAllMembers(string input)", source);
        Assert.Contains("CopyPhasesToAllMembers(sourceJunction)", source);
    }

    [Fact]
    public void Group_member_custom_phase_navigation_initializes_an_editable_phase()
    {
        string source = File.ReadAllText(GetUiBindingsPath());
        string handler = ExtractMethod(source, "protected void CallUpdateMemberPattern");

        Assert.Contains(
            "customTrafficLights.SetMode(CustomTrafficLights.TrafficMode.Dynamic);",
            handler);
        Assert.Contains("customPhaseDataBuffer.Length == 0", handler);
        Assert.Contains("customPhaseDataBuffer.Add(new CustomPhaseData())", handler);
        Assert.Contains("UpdateActiveEditingCustomPhaseIndex(0)", handler);
    }

    [Fact]
    public void Copy_validation_recovers_custom_phases_with_the_legacy_invalid_mode()
    {
        string source = File.ReadAllText(GetTrafficGroupSystemPath());
        string validation = ExtractMethod(source, "private bool ValidatePhaseSyncCompatibility");

        Assert.Equal(
            2,
            Regex.Matches(
                validation,
                @"GetPatternOnly\(\)\s*==\s*CustomTrafficLights\.Patterns\.CustomPhase")
                .Count);
        Assert.Contains(
            "sourceMode = CustomTrafficLights.TrafficMode.Dynamic;",
            validation);
        Assert.Contains(
            "targetMode = CustomTrafficLights.TrafficMode.Dynamic;",
            validation);
    }

    private static string GetTrafficGroupSystemPath() =>
        GetRepositorySourcePath("TrafficLightsEnhancement", "Systems", "TrafficGroupSystem.cs");

    private static string GetMigrationSystemPath() =>
        GetRepositorySourcePath("TrafficLightsEnhancement", "Systems", "Serialization", "TLEDataMigrationSystem.cs");

    private static string GetUiBindingsPath() =>
        GetRepositorySourcePath("TrafficLightsEnhancement", "Systems", "UI", "UISystem.UIBIndings.cs");

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
}
