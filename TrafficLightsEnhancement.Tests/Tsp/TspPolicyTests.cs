using TrafficLightsEnhancement.Logic.Tsp;
using Xunit;

namespace TrafficLightsEnhancement.Tests.Tsp;

public class TspPolicyTests
{
    [Fact]
    public void Availability_AllowsStandaloneTramSignalPriority()
    {
        var settings = new TransitSignalPrioritySettings(
            enabled: true,
            allowTrackRequests: true,
            allowPublicCarRequests: false);

        var availability = TspPolicy.GetAvailability(settings, isGroupedIntersection: false);

        Assert.True(availability.IsRuntimeEligible);
        Assert.Equal(TspAvailabilityReason.None, availability.Reason);
    }

    [Fact]
    public void Availability_rejects_any_group_member_runtime()
    {
        var settings = new TransitSignalPrioritySettings(
            enabled: true,
            allowTrackRequests: true,
            allowPublicCarRequests: false);

        var availability = TspPolicy.GetAvailability(settings, isGroupedIntersection: true);

        Assert.False(availability.IsRuntimeEligible);
        Assert.Equal(TspAvailabilityReason.GroupedIntersection, availability.Reason);
    }

    [Fact]
    public void Disabled_default_settings_do_not_require_persistence()
    {
        Assert.False(TspPolicy.HasPersistedUserValue(new TransitSignalPrioritySettings()));
    }

    [Fact]
    public void Default_request_settings_are_tram_only()
    {
        var settings = new TransitSignalPrioritySettings();

        Assert.True(settings.m_AllowTrackRequests);
        Assert.False(settings.m_AllowPublicCarRequests);
    }

    [Fact]
    public void Settings_type_is_value_type_for_burst_jobs()
    {
        Assert.True(typeof(TransitSignalPrioritySettings).IsValueType);
    }

    [Fact]
    public void Settings_constructor_clamps_excessive_max_green_extension()
    {
        var settings = new TransitSignalPrioritySettings(maxGreenExtensionTicks: ushort.MaxValue);

        Assert.Equal(
            TransitSignalPrioritySettings.MaxGreenExtensionTicksUpperBound,
            settings.m_MaxGreenExtensionTicks);
    }

    [Fact]
    public void Disabled_tsp_settings_are_not_runtime_eligible()
    {
        var availability = TspPolicy.GetAvailability(new TransitSignalPrioritySettings(), isGroupedIntersection: false);

        Assert.False(availability.IsRuntimeEligible);
        Assert.Equal(TspAvailabilityReason.Disabled, availability.Reason);
    }

    [Fact]
    public void Disabled_settings_with_custom_request_behavior_require_persistence()
    {
        var settings = new TransitSignalPrioritySettings(allowTrackRequests: false);

        Assert.True(TspPolicy.HasPersistedUserValue(settings));
    }

    [Fact]
    public void Enabled_tsp_settings_require_persistence()
    {
        var settings = new TransitSignalPrioritySettings(enabled: true);
        Assert.True(TspPolicy.HasPersistedUserValue(settings));
    }

    [Fact]
    public void Grouped_intersection_settings_can_be_persisted_but_are_not_runtime_eligible()
    {
        var settings = new TransitSignalPrioritySettings(enabled: true);
        var availability = TspPolicy.GetAvailability(settings, isGroupedIntersection: true);

        Assert.True(TspPolicy.HasPersistedUserValue(settings));
        Assert.False(availability.IsRuntimeEligible);
        Assert.Equal(TspAvailabilityReason.GroupedIntersection, availability.Reason);
    }

    [Fact]
    public void Grouped_intersection_runtime_is_rejected_even_when_enabled()
    {
        var settings = new TransitSignalPrioritySettings(enabled: true);

        var availability = TspPolicy.GetAvailability(settings, isGroupedIntersection: true);

        Assert.True(TspPolicy.HasPersistedUserValue(settings));
        Assert.False(availability.IsRuntimeEligible);
        Assert.Equal(TspAvailabilityReason.GroupedIntersection, availability.Reason);
    }

    [Fact]
    public void Availability_supports_typed_value_equality()
    {
        Assert.True(typeof(IEquatable<TspAvailability>).IsAssignableFrom(typeof(TspAvailability)));
        Assert.Equal(
            new TspAvailability(true, TspAvailabilityReason.None),
            new TspAvailability(true, TspAvailabilityReason.None));
        Assert.NotEqual(
            new TspAvailability(true, TspAvailabilityReason.None),
            new TspAvailability(false, TspAvailabilityReason.Disabled));
    }

    [Fact]
    public void Legacy_default_request_horizon_is_normalized_to_short_runtime_value()
    {
        Assert.Equal(
            10,
            TspPolicy.GetEffectiveRequestHorizonTicks(120));
    }

    [Fact]
    public void Custom_request_horizon_is_preserved_when_not_using_legacy_default()
    {
        Assert.Equal(
            8,
            TspPolicy.GetEffectiveRequestHorizonTicks(8));
    }

    [Fact]
    public void Zero_request_horizon_uses_default()
    {
        Assert.Equal(
            TransitSignalPrioritySettings.DefaultRequestHorizonTicks,
            TspPolicy.GetEffectiveRequestHorizonTicks(0));
    }

    [Fact]
    public void Excessive_request_horizon_is_clamped()
    {
        Assert.Equal(
            TransitSignalPrioritySettings.MaxRequestHorizonTicksUpperBound,
            TspPolicy.GetEffectiveRequestHorizonTicks(ushort.MaxValue));
    }

    [Fact]
    public void Settings_constructor_clamps_excessive_request_horizon()
    {
        var settings = new TransitSignalPrioritySettings(requestHorizonTicks: ushort.MaxValue);

        Assert.Equal(
            TransitSignalPrioritySettings.MaxRequestHorizonTicksUpperBound,
            settings.m_RequestHorizonTicks);
    }

    [Fact]
    public void Public_car_setting_is_preserved_as_bus_priority_input()
    {
        var settings = new TransitSignalPrioritySettings(allowPublicCarRequests: true);

        Assert.True(settings.m_AllowPublicCarRequests);
        Assert.True(TspPolicy.HasPersistedUserValue(settings));
    }

    [Fact]
    public void Zero_max_green_extension_uses_default()
    {
        Assert.Equal(
            TransitSignalPrioritySettings.DefaultMaxGreenExtensionTicks,
            TspPolicy.GetEffectiveMaxGreenExtensionTicks(0));
    }

    [Fact]
    public void Excessive_max_green_extension_is_clamped()
    {
        Assert.Equal(
            TransitSignalPrioritySettings.MaxGreenExtensionTicksUpperBound,
            TspPolicy.GetEffectiveMaxGreenExtensionTicks(ushort.MaxValue));
    }

    [Fact]
    public void Approach_index_is_not_needed_without_transit_signal_priority_settings()
    {
        Assert.False(TspPolicy.ShouldBuildApproachIndex(hasTransitSignalPrioritySettings: false));
    }

    [Fact]
    public void Approach_index_is_not_needed_without_enabled_runtime_eligible_settings()
    {
        Assert.False(TspPolicy.ShouldBuildApproachIndex(
            hasTransitSignalPrioritySettings: true,
            hasApproachIndexEligibleTransitSignalPrioritySettings: false));
    }

    [Fact]
    public void Approach_index_is_needed_when_transit_signal_priority_settings_are_index_eligible()
    {
        Assert.True(TspPolicy.ShouldBuildApproachIndex(
            hasTransitSignalPrioritySettings: true,
            hasApproachIndexEligibleTransitSignalPrioritySettings: true));
    }

    [Fact]
    public void Approach_index_setting_requires_enabled_track_requests()
    {
        var disabled = new TransitSignalPrioritySettings(enabled: false, allowTrackRequests: true);
        var trackDisabled = new TransitSignalPrioritySettings(enabled: true, allowTrackRequests: false);
        var publicCarOnly = new TransitSignalPrioritySettings(
            enabled: true,
            allowTrackRequests: false,
            allowPublicCarRequests: true);

        Assert.False(TspPolicy.IsApproachIndexEligibleSetting(disabled, isGroupedIntersection: false));
        Assert.False(TspPolicy.IsApproachIndexEligibleSetting(trackDisabled, isGroupedIntersection: false));
        Assert.False(TspPolicy.IsApproachIndexEligibleSetting(publicCarOnly, isGroupedIntersection: false));
    }

    [Fact]
    public void Approach_index_setting_rejects_grouped_intersections()
    {
        var settings = new TransitSignalPrioritySettings(enabled: true, allowTrackRequests: true);

        Assert.False(TspPolicy.IsApproachIndexEligibleSetting(settings, isGroupedIntersection: true));
    }

    [Fact]
    public void Approach_index_setting_allows_enabled_track_request_standalone_junctions()
    {
        var settings = new TransitSignalPrioritySettings(enabled: true, allowTrackRequests: true);

        Assert.True(TspPolicy.IsApproachIndexEligibleSetting(settings, isGroupedIntersection: false));
    }

    [Fact]
    public void Bus_approach_index_setting_requires_enabled_public_car_requests()
    {
        var disabled = new TransitSignalPrioritySettings(
            enabled: false,
            allowTrackRequests: false,
            allowPublicCarRequests: true);
        var busDisabled = new TransitSignalPrioritySettings(
            enabled: true,
            allowTrackRequests: false,
            allowPublicCarRequests: false);
        var busEnabled = new TransitSignalPrioritySettings(
            enabled: true,
            allowTrackRequests: false,
            allowPublicCarRequests: true);

        Assert.False(TspPolicy.IsBusApproachIndexEligibleSetting(disabled, isGroupedIntersection: false));
        Assert.False(TspPolicy.IsBusApproachIndexEligibleSetting(busDisabled, isGroupedIntersection: false));
        Assert.False(TspPolicy.IsBusApproachIndexEligibleSetting(busEnabled, isGroupedIntersection: true));
        Assert.True(TspPolicy.IsBusApproachIndexEligibleSetting(busEnabled, isGroupedIntersection: false));
    }

    [Fact]
    public void Grouped_intersections_are_not_approach_index_eligible_for_any_enabled_tsp_source()
    {
        var settings = new TransitSignalPrioritySettings(
            enabled: true,
            allowTrackRequests: true,
            allowPublicCarRequests: true);

        Assert.False(TspPolicy.IsApproachIndexEligibleSetting(settings, isGroupedIntersection: true));
        Assert.False(TspPolicy.IsBusApproachIndexEligibleSetting(settings, isGroupedIntersection: true));
    }

    [Fact]
    public void Pedestrian_phase_protection_ignores_out_of_range_signal_groups()
    {
        Assert.False(TspPreemptionPolicy.ShouldProtectActivePedestrianPhase(
            exclusivePedestrianEnabled: true,
            currentSignalGroup: 33,
            pedestrianPhaseGroupMask: 1,
            isOngoing: true));
    }
}
