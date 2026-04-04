using TrafficLightsEnhancement.Logic.Tsp;
using Xunit;

namespace TrafficLightsEnhancement.Tests.Tsp;

public class TspDecisionEngineTests
{
    [Fact]
    public void Track_request_prefers_serving_phase()
    {
        var phases = new[]
        {
            new PhaseScore(phaseIndex: 0, basePriority: 100, weightedWaiting: 4f, servesTrack: false, servesPublicCar: true),
            new PhaseScore(phaseIndex: 1, basePriority: 100, weightedWaiting: 3f, servesTrack: true, servesPublicCar: false),
        };

        var request = new TspRequest(source: TspSource.Track, strength: 1f, extensionEligible: false);

        var decision = TspDecisionEngine.SelectNextPhase(phases, currentPhaseIndex: 0, request);

        Assert.Equal(1, decision.NextPhaseIndex);
    }

    [Fact]
    public void Disabled_settings_produce_no_request()
    {
        var settings = new TransitSignalPrioritySettings();

        Assert.False(settings.m_Enabled);
    }

    [Fact]
    public void Public_car_request_only_targets_public_car_phase()
    {
        var settings = new TransitSignalPrioritySettings
        {
            m_Enabled = true,
            m_AllowTrackRequests = false,
            m_AllowPublicCarRequests = true,
        };

        var phases = new[]
        {
            new PhaseScore(0, 100, 1f, servesTrack: false, servesPublicCar: false),
            new PhaseScore(1, 100, 1f, servesTrack: false, servesPublicCar: true),
        };

        bool built = TransitSignalPriorityRuntime.TryBuildRequestForLane(
            settings,
            isTrackLane: false,
            isPublicCarLane: true,
            out var request);

        Assert.True(built);

        var decision = TspDecisionEngine.SelectNextPhase(phases, 0, request);

        Assert.Equal(1, decision.NextPhaseIndex);
    }

    [Fact]
    public void Extension_is_used_only_when_current_phase_serves_request()
    {
        var phases = new[]
        {
            new PhaseScore(0, 104, 2f, servesTrack: true, servesPublicCar: false),
            new PhaseScore(1, 104, 5000f, servesTrack: false, servesPublicCar: false),
        };

        var request = new TspRequest(TspSource.Track, 1f, extensionEligible: true);
        var decision = TspDecisionEngine.SelectNextPhase(phases, currentPhaseIndex: 0, request);

        Assert.True(decision.CanExtendCurrent);
        Assert.Equal(0, decision.NextPhaseIndex);
    }

    [Fact]
    public void Leader_prefers_member_request_when_group_propagation_is_enabled()
    {
        var aggregated = TspDecisionEngine.CombineRequests(new[]
        {
            new TspRequest(TspSource.PublicCar, 0.5f, extensionEligible: false),
            new TspRequest(TspSource.Track, 1f, extensionEligible: false),
        });

        Assert.Equal(TspSource.Track, aggregated.Source);
        Assert.Equal(1f, aggregated.Strength);
    }

    [Fact]
    public void Grouped_enabled_intersection_is_runtime_eligible_for_local_request()
    {
        var availability = TspPolicy.GetAvailability(
            settings: new TransitSignalPrioritySettings { m_Enabled = true },
            isGroupedIntersection: true);

        Assert.True(availability.IsRuntimeEligible);
        Assert.Equal(TspAvailabilityReason.None, availability.Reason);
    }

    [Fact]
    public void Stronger_grouped_request_overrides_weaker_local_request()
    {
        var selected = TspDecisionEngine.CombineRequests(new[]
        {
            new TspRequest(TspSource.PublicCar, 0.5f, extensionEligible: false),
            new TspRequest(TspSource.Track, 1f, extensionEligible: true),
        });

        Assert.Equal(TspSource.Track, selected.Source);
        Assert.Equal(1f, selected.Strength);
        Assert.True(selected.ExtensionEligible);
    }

    [Fact]
    public void Equal_strength_grouped_request_keeps_existing_local_request()
    {
        var selected = TspDecisionEngine.CombineRequests(new[]
        {
            new TspRequest(TspSource.PublicCar, 1f, extensionEligible: false),
            new TspRequest(TspSource.Track, 1f, extensionEligible: true),
        });

        Assert.Equal(TspSource.PublicCar, selected.Source);
        Assert.Equal(1f, selected.Strength);
        Assert.False(selected.ExtensionEligible);
    }

    [Fact]
    public void Override_selection_reports_target_phase_when_request_changes_choice()
    {
        var overrideSelection = TspOverrideEngine.ApplyRequestOverride(
            basePhaseIndex: 0,
            currentPhaseIndex: 0,
            phaseCount: 3,
            targetPhaseIndex: 2,
            new TspRequest(TspSource.Track, 1f, extensionEligible: false));

        Assert.True(overrideSelection.Applied);
        Assert.True(overrideSelection.ChangedBaseSelection);
        Assert.Equal(TspSelectionReason.SelectedTargetPhase, overrideSelection.Reason);
        Assert.Equal(2, overrideSelection.SelectedPhaseIndex);
        Assert.Equal(0, overrideSelection.BasePhaseIndex);
    }

    [Fact]
    public void Override_selection_reports_extension_without_changing_base_choice()
    {
        var overrideSelection = TspOverrideEngine.ApplyRequestOverride(
            basePhaseIndex: 1,
            currentPhaseIndex: 1,
            phaseCount: 3,
            targetPhaseIndex: 1,
            new TspRequest(TspSource.Track, 1f, extensionEligible: true));

        Assert.True(overrideSelection.Applied);
        Assert.False(overrideSelection.ChangedBaseSelection);
        Assert.Equal(TspSelectionReason.ExtendedCurrentPhase, overrideSelection.Reason);
        Assert.True(overrideSelection.CanExtendCurrent);
        Assert.Equal(1, overrideSelection.SelectedPhaseIndex);
    }

    [Fact]
    public void Signal_group_override_selects_requested_group_for_built_in_patterns()
    {
        var overrideSelection = TspOverrideEngine.ApplySignalGroupOverride(
            baseSignalGroup: 1,
            currentSignalGroup: 1,
            signalGroupCount: 4,
            targetSignalGroup: 3,
            new TspRequest(TspSource.Track, 1f, extensionEligible: false));

        Assert.True(overrideSelection.Applied);
        Assert.Equal(TspSelectionReason.SelectedTargetPhase, overrideSelection.Reason);
        Assert.Equal(3, overrideSelection.SelectedPhaseIndex + 1);
    }

    [Fact]
    public void Fresh_signal_request_is_latched_with_full_horizon()
    {
        bool active = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 1, extendCurrentPhase: true),
            existingRequest: null,
            requestHorizonTicks: 10,
            currentSignalGroup: 2,
            out var request);

        Assert.True(active);
        Assert.Equal(10u, request.ExpiryTimer);
        Assert.True(request.ExtendCurrentPhase);
    }

    [Fact]
    public void Fresh_signal_request_does_not_gain_extension_without_eligibility()
    {
        bool active = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 1, extendCurrentPhase: false),
            existingRequest: null,
            requestHorizonTicks: 10,
            currentSignalGroup: 2,
            out var request);

        Assert.True(active);
        Assert.Equal(10u, request.ExpiryTimer);
        Assert.False(request.ExtendCurrentPhase);
    }

    [Fact]
    public void Existing_signal_request_stays_active_while_expiry_remains()
    {
        bool active = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: null,
            existingRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 3, extendCurrentPhase: false),
            requestHorizonTicks: 10,
            currentSignalGroup: 1,
            out var request);

        Assert.True(active);
        Assert.Equal(2u, request.ExpiryTimer);
        Assert.False(request.ExtendCurrentPhase);
    }

    [Fact]
    public void Existing_signal_request_keeps_full_horizon_until_target_group_is_active()
    {
        bool active = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: null,
            existingRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 120, extendCurrentPhase: false),
            requestHorizonTicks: 120,
            currentSignalGroup: 1,
            out var request);

        Assert.True(active);
        Assert.Equal(119u, request.ExpiryTimer);
        Assert.False(request.ExtendCurrentPhase);
    }

    [Fact]
    public void Stale_signal_request_collapses_to_short_release_grace()
    {
        bool active = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: null,
            existingRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 120, extendCurrentPhase: false),
            requestHorizonTicks: 120,
            currentSignalGroup: 2,
            out var request);

        Assert.True(active);
        Assert.Equal(6u, request.ExpiryTimer);
        Assert.False(request.ExtendCurrentPhase);
    }

    [Fact]
    public void Hold_policy_keeps_current_group_green_until_hard_cap()
    {
        var request = new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 30, extendCurrentPhase: true);

        Assert.True(TspPreemptionPolicy.ShouldHoldCurrentGroup(
            currentSignalGroup: 2,
            request,
            signalTimer: 12,
            maxGreenExtensionTicks: 45));

        Assert.False(TspPreemptionPolicy.ShouldHoldCurrentGroup(
            currentSignalGroup: 2,
            request,
            signalTimer: 45,
            maxGreenExtensionTicks: 45));
    }

    [Fact]
    public void Hold_policy_requires_extension_eligibility()
    {
        var request = new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 6, extendCurrentPhase: false);

        Assert.False(TspPreemptionPolicy.ShouldHoldCurrentGroup(
            currentSignalGroup: 2,
            request,
            signalTimer: 4,
            maxGreenExtensionTicks: 45));
    }

    [Fact]
    public void Conflicting_transit_request_uses_shorter_minimum_green()
    {
        Assert.Equal(
            1,
            TspPreemptionPolicy.GetMinimumGreenDurationTicks(
                defaultMinimumGreenTicks: 2,
                currentSignalGroup: 1,
                request: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 10, extendCurrentPhase: false)));

        Assert.Equal(
            2,
            TspPreemptionPolicy.GetMinimumGreenDurationTicks(
                defaultMinimumGreenTicks: 2,
                currentSignalGroup: 2,
                request: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 10, extendCurrentPhase: true)));
    }
}
