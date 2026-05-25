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
            new PhaseScore(phaseIndex: 0, basePriority: 100, weightedWaiting: 4f, servesTrack: false, servesPublicCar: false),
            new PhaseScore(phaseIndex: 1, basePriority: 100, weightedWaiting: 3f, servesTrack: true, servesPublicCar: false),
        };

        var request = new TspRequest(source: TspSource.Track, strength: 1f, extensionEligible: false);

        var decision = TspDecisionEngine.SelectNextPhase(phases, currentPhaseIndex: 0, request);

        Assert.Equal(1, decision.NextPhaseIndex);
    }

    [Fact]
    public void Public_car_request_selects_public_car_phase()
    {
        var phases = new[]
        {
            new PhaseScore(phaseIndex: 0, basePriority: 100, weightedWaiting: 4f, servesTrack: false, servesPublicCar: false),
            new PhaseScore(phaseIndex: 1, basePriority: 100, weightedWaiting: 3f, servesTrack: false, servesPublicCar: true),
        };

        var request = new TspRequest(source: TspSource.PublicCar, strength: 1f, extensionEligible: false);

        var decision = TspDecisionEngine.SelectNextPhase(phases, currentPhaseIndex: 0, request);

        Assert.Equal(1, decision.NextPhaseIndex);
    }

    [Fact]
    public void Public_car_request_extends_public_car_phase()
    {
        var phases = new[]
        {
            new PhaseScore(phaseIndex: 0, basePriority: 100, weightedWaiting: 4f, servesTrack: false, servesPublicCar: true),
            new PhaseScore(phaseIndex: 1, basePriority: 100, weightedWaiting: 3f, servesTrack: true, servesPublicCar: false),
        };

        var request = new TspRequest(source: TspSource.PublicCar, strength: 1f, extensionEligible: true);

        var decision = TspDecisionEngine.SelectNextPhase(phases, currentPhaseIndex: 0, request);

        Assert.True(decision.CanExtendCurrent);
        Assert.Equal(0, decision.NextPhaseIndex);
    }

    [Fact]
    public void Public_car_request_is_not_selected_from_candidate_requests()
    {
        var selected = TspDecisionEngine.CombineRequests(new[]
        {
            new TspRequest(TspSource.Track, 0.5f, extensionEligible: true),
            new TspRequest(TspSource.PublicCar, 1f, extensionEligible: false),
        });

        Assert.NotNull(selected);
        Assert.Equal(TspSource.Track, selected.Value.Source);
        Assert.Equal(0.5f, selected.Value.Strength);
        Assert.True(selected.Value.ExtensionEligible);
    }

    [Fact]
    public void Combine_requests_rejects_null_request_collection()
    {
        Assert.Throws<ArgumentNullException>(() => TspDecisionEngine.CombineRequests(null!));
    }

    [Fact]
    public void Public_car_only_candidates_produce_combined_request()
    {
        var selected = TspDecisionEngine.CombineRequests(new[]
        {
            new TspRequest(TspSource.PublicCar, 1f, extensionEligible: false),
        });

        Assert.NotNull(selected);
        Assert.Equal(TspSource.PublicCar, selected.Value.Source);
    }

    [Fact]
    public void Disabled_settings_produce_no_request()
    {
        var settings = new TransitSignalPrioritySettings();

        Assert.False(settings.m_Enabled);

        bool built = TransitSignalPriorityRuntime.TryBuildRequestForLane(
            settings,
            isTrackLane: true,
            out _);

        Assert.False(built);
    }

    [Fact]
    public void Null_settings_produce_no_request()
    {
        bool built = TransitSignalPriorityRuntime.TryBuildRequestForLane(
            null!,
            isTrackLane: true,
            out _);

        Assert.False(built);
    }

    [Fact]
    public void Empty_phase_list_selects_no_phase()
    {
        var decision = TspDecisionEngine.SelectNextPhase(
            Array.Empty<PhaseScore>(),
            currentPhaseIndex: 3,
            new TspRequest(TspSource.Track, 1f, extensionEligible: true));

        Assert.Equal(-1, decision.NextPhaseIndex);
        Assert.False(decision.CanExtendCurrent);
    }

    [Fact]
    public void Select_next_phase_rejects_null_phase_collection()
    {
        Assert.Throws<ArgumentNullException>(() => TspDecisionEngine.SelectNextPhase(
            null!,
            currentPhaseIndex: 0,
            new TspRequest(TspSource.Track, 1f, extensionEligible: true)));
    }

    [Fact]
    public void Track_lane_builds_tram_request_with_default_request_settings()
    {
        var settings = new TransitSignalPrioritySettings(enabled: true);

        bool built = TransitSignalPriorityRuntime.TryBuildRequestForLane(
            settings,
            isTrackLane: true,
            out var request);

        Assert.True(built);
        Assert.Equal(TspSource.Track, request.Source);
        Assert.Equal(1f, request.Strength);
        Assert.True(request.ExtensionEligible);
    }

    [Fact]
    public void Public_car_lane_builds_bus_request_only_when_explicitly_enabled()
    {
        var defaultSettings = new TransitSignalPrioritySettings(enabled: true);
        bool defaultBuilt = TransitSignalPriorityRuntime.TryBuildRequestForLane(
            defaultSettings,
            isTrackLane: false,
            isPublicCarLane: true,
            out _);

        var busSettings = new TransitSignalPrioritySettings(
            enabled: true,
            allowPublicCarRequests: true);
        bool busBuilt = TransitSignalPriorityRuntime.TryBuildRequestForLane(
            busSettings,
            isTrackLane: false,
            isPublicCarLane: true,
            out var request);

        Assert.False(defaultBuilt);
        Assert.True(busBuilt);
        Assert.Equal(TspSource.PublicCar, request.Source);
        Assert.Equal(1f, request.Strength);
        Assert.True(request.ExtensionEligible);
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
    public void Extension_is_not_used_when_current_phase_wins_without_serving_track()
    {
        var phases = new[]
        {
            new PhaseScore(0, 104, 5000f, servesTrack: false, servesPublicCar: false),
            new PhaseScore(1, 104, 2f, servesTrack: false, servesPublicCar: false),
        };

        var request = new TspRequest(TspSource.Track, 1f, extensionEligible: true);
        var decision = TspDecisionEngine.SelectNextPhase(phases, currentPhaseIndex: 0, request);

        Assert.Equal(0, decision.NextPhaseIndex);
        Assert.False(decision.CanExtendCurrent);
    }

    [Fact]
    public void Stronger_tram_request_is_selected_from_candidate_requests()
    {
        var aggregated = TspDecisionEngine.CombineRequests(new[]
        {
            new TspRequest(TspSource.Track, 0.5f, extensionEligible: false),
            new TspRequest(TspSource.Track, 1f, extensionEligible: false),
        });

        Assert.NotNull(aggregated);
        Assert.Equal(TspSource.Track, aggregated.Value.Source);
        Assert.Equal(1f, aggregated.Value.Strength);
    }

    [Fact]
    public void Grouped_enabled_intersection_is_not_runtime_eligible_for_local_request()
    {
        var availability = TspPolicy.GetAvailability(
            settings: new TransitSignalPrioritySettings(enabled: true),
            isGroupedIntersection: true);

        Assert.False(availability.IsRuntimeEligible);
        Assert.Equal(TspAvailabilityReason.GroupedIntersection, availability.Reason);
    }

    [Fact]
    public void Stronger_tram_request_preserves_extension_eligibility()
    {
        var selected = TspDecisionEngine.CombineRequests(new[]
        {
            new TspRequest(TspSource.Track, 0.5f, extensionEligible: false),
            new TspRequest(TspSource.Track, 1f, extensionEligible: true),
        });

        Assert.NotNull(selected);
        Assert.Equal(TspSource.Track, selected.Value.Source);
        Assert.Equal(1f, selected.Value.Strength);
        Assert.True(selected.Value.ExtensionEligible);
    }

    [Fact]
    public void Equal_strength_tram_request_keeps_existing_candidate()
    {
        var selected = TspDecisionEngine.CombineRequests(new[]
        {
            new TspRequest(TspSource.Track, 1f, extensionEligible: false),
            new TspRequest(TspSource.Track, 1f, extensionEligible: true),
        });

        Assert.NotNull(selected);
        Assert.Equal(TspSource.Track, selected.Value.Source);
        Assert.Equal(1f, selected.Value.Strength);
        Assert.False(selected.Value.ExtensionEligible);
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
    public void Override_selection_targets_requested_group_when_current_group_differs()
    {
        var overrideSelection = TspOverrideEngine.ApplyRequestOverride(
            basePhaseIndex: 0,
            currentPhaseIndex: 0,
            phaseCount: 3,
            targetPhaseIndex: 2,
            new TspRequest(TspSource.Track, 1f, extensionEligible: true));

        Assert.True(overrideSelection.Applied);
        Assert.False(overrideSelection.CanExtendCurrent);
        Assert.Equal(TspSelectionReason.SelectedTargetPhase, overrideSelection.Reason);
        Assert.Equal(2, overrideSelection.SelectedPhaseIndex);
    }

    [Fact]
    public void Override_selection_does_not_reselect_current_group_without_extension_eligibility()
    {
        var overrideSelection = TspOverrideEngine.ApplyRequestOverride(
            basePhaseIndex: 5,
            currentPhaseIndex: 4,
            phaseCount: 8,
            targetPhaseIndex: 4,
            new TspRequest(TspSource.Track, 1f, extensionEligible: false));

        Assert.False(overrideSelection.Applied);
        Assert.False(overrideSelection.CanExtendCurrent);
        Assert.False(overrideSelection.ChangedBaseSelection);
        Assert.Equal(TspSelectionReason.None, overrideSelection.Reason);
        Assert.Equal(5, overrideSelection.SelectedPhaseIndex);
    }

    [Fact]
    public void Override_selection_protects_active_exclusive_pedestrian_phase()
    {
        var overrideSelection = TspOverrideEngine.ApplyRequestOverride(
            basePhaseIndex: 1,
            currentPhaseIndex: 2,
            phaseCount: 3,
            targetPhaseIndex: 0,
            new TspRequest(TspSource.Track, 1f, extensionEligible: false),
            protectActivePedestrianPhase: true);

        Assert.False(overrideSelection.Applied);
        Assert.False(overrideSelection.ChangedBaseSelection);
        Assert.Equal(1, overrideSelection.SelectedPhaseIndex);
    }

    [Fact]
    public void Override_selection_applies_public_car_extension_request()
    {
        var overrideSelection = TspOverrideEngine.ApplyRequestOverride(
            basePhaseIndex: 1,
            currentPhaseIndex: 1,
            phaseCount: 3,
            targetPhaseIndex: 1,
            new TspRequest(TspSource.PublicCar, 1f, extensionEligible: true));

        Assert.True(overrideSelection.Applied);
        Assert.True(overrideSelection.CanExtendCurrent);
        Assert.Equal(1, overrideSelection.SelectedPhaseIndex);
    }

    [Fact]
    public void Override_selection_applies_public_car_target_request()
    {
        var overrideSelection = TspOverrideEngine.ApplyRequestOverride(
            basePhaseIndex: 0,
            currentPhaseIndex: 0,
            phaseCount: 3,
            targetPhaseIndex: 2,
            new TspRequest(TspSource.PublicCar, 1f, extensionEligible: false));

        Assert.True(overrideSelection.Applied);
        Assert.Equal(2, overrideSelection.SelectedPhaseIndex);
    }

    [Fact]
    public void Override_selection_without_base_does_not_report_changed_base()
    {
        var overrideSelection = TspOverrideEngine.ApplyRequestOverride(
            basePhaseIndex: -1,
            currentPhaseIndex: -1,
            phaseCount: 3,
            targetPhaseIndex: 1,
            new TspRequest(TspSource.Track, 1f, extensionEligible: false));

        Assert.True(overrideSelection.Applied);
        Assert.False(overrideSelection.ChangedBaseSelection);
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
    public void Signal_group_override_protects_active_exclusive_pedestrian_phase()
    {
        var overrideSelection = TspOverrideEngine.ApplySignalGroupOverride(
            baseSignalGroup: 2,
            currentSignalGroup: 3,
            signalGroupCount: 4,
            targetSignalGroup: 1,
            new TspRequest(TspSource.Track, 1f, extensionEligible: false),
            protectActivePedestrianPhase: true);

        Assert.False(overrideSelection.Applied);
        Assert.Equal(2, overrideSelection.SelectedPhaseIndex + 1);
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
    public void Fresh_public_car_signal_request_latches()
    {
        bool active = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 1, extendCurrentPhase: true),
            existingRequest: null,
            requestHorizonTicks: 10,
            currentSignalGroup: 2,
            out var request);

        Assert.True(active);
        Assert.Equal(2, request.TargetSignalGroup);
        Assert.Equal(TspSource.PublicCar, request.Source);
        Assert.Equal(10u, request.ExpiryTimer);
        Assert.True(request.ExtendCurrentPhase);
    }

    [Fact]
    public void Track_existing_signal_request_latches_without_fresh_refresh_when_still_valid()
    {
        bool active = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: null,
            existingRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 3, extendCurrentPhase: false),
            requestHorizonTicks: 10,
            currentSignalGroup: 1,
            out var request);

        Assert.True(active);
        Assert.Equal(2, request.TargetSignalGroup);
        Assert.Equal(TspSource.Track, request.Source);
        Assert.Equal(1f, request.Strength);
        Assert.Equal(2u, request.ExpiryTimer);
        Assert.False(request.ExtendCurrentPhase);
    }

    [Fact]
    public void Fresh_track_signal_request_latches_on_next_update_without_fresh_refresh()
    {
        const ushort horizonTicks = 10;

        bool freshActive = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 1, extendCurrentPhase: true),
            existingRequest: null,
            requestHorizonTicks: horizonTicks,
            currentSignalGroup: 2,
            out var persistedRequest);

        bool latchedActive = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: null,
            existingRequest: persistedRequest,
            requestHorizonTicks: horizonTicks,
            currentSignalGroup: 2,
            out var latchedRequest);

        Assert.True(freshActive);
        Assert.True(latchedActive);
        Assert.Equal(2, latchedRequest.TargetSignalGroup);
        Assert.Equal(TspSource.Track, latchedRequest.Source);
        Assert.Equal(1f, latchedRequest.Strength);
        Assert.Equal(9u, latchedRequest.ExpiryTimer);
        Assert.True(latchedRequest.ExtendCurrentPhase);
    }

    [Fact]
    public void Public_car_existing_signal_request_latches_without_fresh_refresh_when_still_valid()
    {
        bool active = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: null,
            existingRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 3, extendCurrentPhase: false),
            requestHorizonTicks: 10,
            currentSignalGroup: 1,
            out var request);

        Assert.True(active);
        Assert.Equal(2, request.TargetSignalGroup);
        Assert.Equal(TspSource.PublicCar, request.Source);
        Assert.Equal(2u, request.ExpiryTimer);
        Assert.False(request.ExtendCurrentPhase);
    }

    [Fact]
    public void Existing_track_request_outranks_fresh_public_car_request()
    {
        bool active = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: new TspSignalRequest(targetSignalGroup: 3, TspSource.PublicCar, strength: 1f, expiryTimer: 1, extendCurrentPhase: true),
            existingRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 3, extendCurrentPhase: false),
            requestHorizonTicks: 10,
            currentSignalGroup: 1,
            out var request);

        Assert.True(active);
        Assert.Equal(2, request.TargetSignalGroup);
        Assert.Equal(TspSource.Track, request.Source);
        Assert.Equal(2u, request.ExpiryTimer);
        Assert.False(request.ExtendCurrentPhase);
    }

    [Fact]
    public void Fresh_track_request_replaces_existing_public_car_request()
    {
        bool active = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 1, extendCurrentPhase: false),
            existingRequest: new TspSignalRequest(targetSignalGroup: 3, TspSource.PublicCar, strength: 1f, expiryTimer: 3, extendCurrentPhase: true),
            requestHorizonTicks: 10,
            currentSignalGroup: 1,
            out var request);

        Assert.True(active);
        Assert.Equal(2, request.TargetSignalGroup);
        Assert.Equal(TspSource.Track, request.Source);
        Assert.Equal(10u, request.ExpiryTimer);
        Assert.False(request.ExtendCurrentPhase);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(11u)]
    public void Expired_or_stale_existing_signal_request_does_not_latch(uint expiryTimer)
    {
        bool active = TspPreemptionPolicy.TryRefreshOrLatchRequest(
            freshRequest: null,
            existingRequest: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer, extendCurrentPhase: true),
            requestHorizonTicks: 10,
            currentSignalGroup: 2,
            out var request);

        Assert.False(active);
        Assert.Equal(default, request);
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
    public void Hold_policy_allows_public_car_request()
    {
        var request = new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 30, extendCurrentPhase: true);

        Assert.True(TspPreemptionPolicy.ShouldHoldCurrentGroup(
            currentSignalGroup: 2,
            request,
            signalTimer: 12,
            maxGreenExtensionTicks: 45));
    }

    [Fact]
    public void Conflicting_transit_request_uses_shorter_minimum_green()
    {
        Assert.Equal(
            TspPreemptionPolicy.AggressivePreemptionMinimumGreenTicks,
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

    [Fact]
    public void Active_exclusive_pedestrian_phase_keeps_default_minimum_green()
    {
        Assert.Equal(
            6,
            TspPreemptionPolicy.GetMinimumGreenDurationTicks(
                defaultMinimumGreenTicks: 6,
                currentSignalGroup: 3,
                request: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 10, extendCurrentPhase: false),
                protectActivePedestrianPhase: true));
    }

    [Fact]
    public void Public_car_request_keeps_default_minimum_green()
    {
        Assert.Equal(
            2,
            TspPreemptionPolicy.GetMinimumGreenDurationTicks(
                defaultMinimumGreenTicks: 2,
                currentSignalGroup: 1,
                request: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 10, extendCurrentPhase: false)));
    }

    [Fact]
    public void Conflicting_transit_request_allows_aggressive_preemption()
    {
        Assert.True(TspPreemptionPolicy.ShouldAggressivelyPreemptToConflictingGroup(
            currentSignalGroup: 1,
            request: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 10, extendCurrentPhase: false)));
    }

    [Fact]
    public void Active_exclusive_pedestrian_phase_blocks_aggressive_preemption()
    {
        Assert.False(TspPreemptionPolicy.ShouldAggressivelyPreemptToConflictingGroup(
            currentSignalGroup: 3,
            request: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 10, extendCurrentPhase: false),
            protectActivePedestrianPhase: true));
    }

    [Fact]
    public void First_tram_override_of_due_pedestrian_phase_records_pending_pedestrian()
    {
        var state = TspPedestrianFairnessState.None;

        state = TspPedestrianFairnessPolicy.UpdateAfterSelection(
            state,
            exclusivePedestrianEnabled: true,
            pedestrianPhaseGroupMask: 1 << 2,
            currentSignalGroup: 1,
            baseSignalGroup: 3,
            selectedSignalGroup: 2,
            tspOverrideApplied: true);

        Assert.True(state.HasPendingPedestrianPhase);
        Assert.Equal(3, state.PendingPedestrianSignalGroup);
    }

    [Fact]
    public void Pending_pedestrian_phase_blocks_next_conflicting_tsp_override()
    {
        var state = new TspPedestrianFairnessState(pendingPedestrianSignalGroup: 3);

        Assert.True(TspPedestrianFairnessPolicy.ShouldDeferToPendingPedestrianPhase(
            state,
            exclusivePedestrianEnabled: true,
            pedestrianPhaseGroupMask: 1 << 2,
            currentSignalGroup: 2,
            requestedSignalGroup: 1));
    }

    [Fact]
    public void Pending_pedestrian_phase_blocks_current_group_hold()
    {
        var state = new TspPedestrianFairnessState(pendingPedestrianSignalGroup: 3);

        Assert.True(TspPedestrianFairnessPolicy.ShouldSuppressCurrentGroupHold(
            state,
            exclusivePedestrianEnabled: true,
            pedestrianPhaseGroupMask: 1 << 2,
            currentSignalGroup: 2));
    }

    [Fact]
    public void Pending_pedestrian_phase_allows_in_flight_skipped_tram_phase_to_start()
    {
        var state = new TspPedestrianFairnessState(pendingPedestrianSignalGroup: 3);

        Assert.False(TspPedestrianFairnessPolicy.ShouldDeferToPendingPedestrianPhase(
            state,
            exclusivePedestrianEnabled: true,
            pedestrianPhaseGroupMask: 1 << 2,
            currentSignalGroup: 1,
            requestedSignalGroup: 2,
            inFlightSignalGroup: 2));
    }

    [Fact]
    public void Running_pending_pedestrian_phase_clears_fairness_state()
    {
        var state = new TspPedestrianFairnessState(pendingPedestrianSignalGroup: 3);

        state = TspPedestrianFairnessPolicy.Refresh(
            state,
            exclusivePedestrianEnabled: true,
            pedestrianPhaseGroupMask: 1 << 2,
            currentSignalGroup: 3);

        Assert.False(state.HasPendingPedestrianPhase);
    }

    [Fact]
    public void Exclusive_pedestrian_phase_is_protected_only_while_current_and_ongoing()
    {
        Assert.True(TspPreemptionPolicy.ShouldProtectActivePedestrianPhase(
            exclusivePedestrianEnabled: true,
            currentSignalGroup: 3,
            pedestrianPhaseGroupMask: 1 << 2,
            isOngoing: true));

        Assert.False(TspPreemptionPolicy.ShouldProtectActivePedestrianPhase(
            exclusivePedestrianEnabled: true,
            currentSignalGroup: 2,
            pedestrianPhaseGroupMask: 1 << 2,
            isOngoing: true));

        Assert.False(TspPreemptionPolicy.ShouldProtectActivePedestrianPhase(
            exclusivePedestrianEnabled: true,
            currentSignalGroup: 3,
            pedestrianPhaseGroupMask: 1 << 2,
            isOngoing: false));

        Assert.False(TspPreemptionPolicy.ShouldProtectActivePedestrianPhase(
            exclusivePedestrianEnabled: false,
            currentSignalGroup: 3,
            pedestrianPhaseGroupMask: 1 << 2,
            isOngoing: true));
    }

    [Fact]
    public void Public_car_request_does_not_allow_aggressive_preemption()
    {
        Assert.False(TspPreemptionPolicy.ShouldAggressivelyPreemptToConflictingGroup(
            currentSignalGroup: 1,
            request: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 10, extendCurrentPhase: false)));
    }

    [Fact]
    public void Public_car_request_can_apply_target_selection_without_aggressive_preemption()
    {
        var request = new TspSignalRequest(
            targetSignalGroup: 2,
            TspSource.PublicCar,
            strength: 1f,
            expiryTimer: 10,
            extendCurrentPhase: false);

        Assert.True(TspPreemptionPolicy.ShouldApplyTargetGroupSelection(request));
        Assert.False(TspPreemptionPolicy.ShouldAggressivelyPreemptToConflictingGroup(
            currentSignalGroup: 1,
            request));
    }

    [Fact]
    public void Public_car_request_can_hold_current_serving_group()
    {
        Assert.True(TspPreemptionPolicy.ShouldHoldCurrentGroup(
            currentSignalGroup: 2,
            request: new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 1f, expiryTimer: 10, extendCurrentPhase: true),
            signalTimer: 4,
            maxGreenExtensionTicks: 10));
    }

    [Fact]
    public void Same_group_or_expired_request_does_not_allow_aggressive_preemption()
    {
        Assert.False(TspPreemptionPolicy.ShouldAggressivelyPreemptToConflictingGroup(
            currentSignalGroup: 2,
            request: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 10, extendCurrentPhase: true)));

        Assert.False(TspPreemptionPolicy.ShouldAggressivelyPreemptToConflictingGroup(
            currentSignalGroup: 1,
            request: new TspSignalRequest(targetSignalGroup: 2, TspSource.Track, strength: 1f, expiryTimer: 0, extendCurrentPhase: false)));
    }
}
