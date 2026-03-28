using TrafficLightsEnhancement.Logic.Tsp;
using Xunit;

namespace TrafficLightsEnhancement.Tests.Tsp;

public class TspPolicyTests
{
    [Fact]
    public void Grouped_intersection_is_not_runtime_eligible_even_when_tsp_is_enabled()
    {
        var availability = TspPolicy.GetAvailability(
            settings: new TransitSignalPrioritySettings { m_Enabled = true },
            isGroupedIntersection: true);

        Assert.False(availability.IsRuntimeEligible);
        Assert.Equal(TspAvailabilityReason.GroupedIntersection, availability.Reason);
    }

    [Fact]
    public void Standalone_enabled_intersection_is_runtime_eligible()
    {
        var availability = TspPolicy.GetAvailability(
            settings: new TransitSignalPrioritySettings { m_Enabled = true },
            isGroupedIntersection: false);

        Assert.True(availability.IsRuntimeEligible);
        Assert.Equal(TspAvailabilityReason.None, availability.Reason);
    }

    [Fact]
    public void Disabled_default_settings_do_not_require_persistence()
    {
        Assert.False(TspPolicy.HasPersistedUserValue(new TransitSignalPrioritySettings()));
    }

    [Fact]
    public void Default_disabled_settings_do_not_force_mod_state_creation()
    {
        TransitSignalPrioritySettings settings = new();
        Assert.False(TspPolicy.HasPersistedUserValue(settings));
    }

    [Fact]
    public void Enabled_tsp_settings_require_persistence()
    {
        var settings = new TransitSignalPrioritySettings { m_Enabled = true };
        Assert.True(TspPolicy.HasPersistedUserValue(settings));
    }

    [Fact]
    public void Grouped_intersection_settings_can_be_persisted_without_runtime_eligibility()
    {
        var settings = new TransitSignalPrioritySettings { m_Enabled = true };
        var availability = TspPolicy.GetAvailability(settings, isGroupedIntersection: true);

        Assert.True(TspPolicy.HasPersistedUserValue(settings));
        Assert.False(availability.IsRuntimeEligible);
    }
}
