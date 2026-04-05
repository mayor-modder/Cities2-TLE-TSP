using TrafficLightsEnhancement.Logic.Tsp;
using Xunit;

namespace TrafficLightsEnhancement.Tests.Tsp;

public class TspEarlyDetectionTests
{
    [Fact]
    public void Approach_lane_resolution_prefers_source_lane_when_present()
    {
        int resolvedLane = EarlyApproachDetection.ResolveApproachLane(
            signaledLane: 10,
            sourceLane: 22,
            nullLane: 0);

        Assert.Equal(22, resolvedLane);
    }

    [Fact]
    public void Tram_detection_matches_immediate_upstream_lane()
    {
        bool matched = EarlyApproachDetection.IsEligibleTramApproachLane(
            currentLane: 10,
            approachLane: 20,
            upstreamLane: 10,
            nullLane: 0);

        Assert.True(matched);
    }

    [Fact]
    public void Tram_detection_requires_being_close_to_the_end_of_the_upstream_lane()
    {
        bool triggered = EarlyApproachDetection.IsEligibleTramApproachState(
            frontMatchesApproachLane: false,
            frontMatchesUpstreamLane: true,
            frontCurvePosition: 0.6f,
            rearMatchesApproachLane: false,
            rearMatchesUpstreamLane: false,
            rearCurvePosition: 0f,
            isVehicleMoving: true,
            approachLaneThreshold: 0.2f,
            upstreamLaneThreshold: 0.9f);

        Assert.False(triggered);
    }

    [Fact]
    public void Tram_detection_can_trigger_early_on_the_actual_approach_lane()
    {
        bool triggered = EarlyApproachDetection.IsEligibleTramApproachState(
            frontMatchesApproachLane: true,
            frontMatchesUpstreamLane: false,
            frontCurvePosition: 0.25f,
            rearMatchesApproachLane: false,
            rearMatchesUpstreamLane: false,
            rearCurvePosition: 0f,
            isVehicleMoving: true,
            approachLaneThreshold: 0.2f,
            upstreamLaneThreshold: 0.9f);

        Assert.True(triggered);
    }

    [Fact]
    public void Indexed_track_detection_prefers_approach_lane_before_upstream_lane()
    {
        var match = EarlyApproachDetection.EvaluateIndexedTrackTramSamples(
            hasApproachSample: true,
            approachCurvePosition: 0.35f,
            hasUpstreamSample: true,
            upstreamCurvePosition: 0.95f,
            approachLaneThreshold: 0.2f,
            upstreamLaneThreshold: 0.9f);

        Assert.Equal(IndexedTrackProbeMatch.MatchOnApproachLane, match);
    }

    [Fact]
    public void Indexed_track_detection_reports_below_threshold_when_samples_exist_but_are_too_early()
    {
        var match = EarlyApproachDetection.EvaluateIndexedTrackTramSamples(
            hasApproachSample: true,
            approachCurvePosition: 0.1f,
            hasUpstreamSample: true,
            upstreamCurvePosition: 0.6f,
            approachLaneThreshold: 0.2f,
            upstreamLaneThreshold: 0.9f);

        Assert.Equal(IndexedTrackProbeMatch.BelowThreshold, match);
    }

    [Fact]
    public void Indexed_track_detection_reports_no_samples_when_index_is_empty()
    {
        var match = EarlyApproachDetection.EvaluateIndexedTrackTramSamples(
            hasApproachSample: false,
            approachCurvePosition: 0f,
            hasUpstreamSample: false,
            upstreamCurvePosition: 0f,
            approachLaneThreshold: 0.2f,
            upstreamLaneThreshold: 0.9f);

        Assert.Equal(IndexedTrackProbeMatch.NoTramSamples, match);
    }

    [Fact]
    public void Moving_vehicle_without_stop_suppression_triggers_early_detection()
    {
        bool triggered = EarlyApproachDetection.IsMovingEligibleApproachState(
            isEligibleLane: true,
            isVehicleMoving: true,
            suppressionFlags: TransitApproachSuppressionFlags.None);

        Assert.True(triggered);
    }

    [Fact]
    public void Scan_wide_selection_falls_back_to_petitioner_when_early_request_is_absent()
    {
        TspRequest petitioner = new(TspSource.Track, strength: 0.8f, extensionEligible: true);
        TransitApproachScanState scanState = default;

        scanState = EarlyApproachDetection.RecordLaneRequests(
            scanState,
            earlyRequest: null,
            petitionerRequest: petitioner);

        TspRequest? selected = EarlyApproachDetection.PreferEarlyRequest(
            scanState.EarlyRequest,
            scanState.PetitionerRequest);

        Assert.True(selected.HasValue);
        Assert.Equal(petitioner.Source, selected.Value.Source);
        Assert.Equal(petitioner.Strength, selected.Value.Strength);
        Assert.Equal(petitioner.ExtensionEligible, selected.Value.ExtensionEligible);
    }

    [Fact]
    public void Scan_wide_selection_prefers_later_early_request_over_earlier_petitioner()
    {
        TspRequest earlyRequest = new(TspSource.PublicCar, strength: 1f, extensionEligible: false);
        TspRequest petitionerRequest = new(TspSource.Track, strength: 0.25f, extensionEligible: true);
        TransitApproachScanState scanState = default;

        scanState = EarlyApproachDetection.RecordLaneRequests(
            scanState,
            earlyRequest: null,
            petitionerRequest: petitionerRequest);
        scanState = EarlyApproachDetection.RecordLaneRequests(
            scanState,
            earlyRequest: earlyRequest,
            petitionerRequest: null);

        TspRequest? selected = EarlyApproachDetection.PreferEarlyRequest(
            scanState.EarlyRequest,
            scanState.PetitionerRequest);

        Assert.True(selected.HasValue);
        Assert.Equal(earlyRequest.Source, selected.Value.Source);
        Assert.Equal(earlyRequest.Strength, selected.Value.Strength);
        Assert.Equal(earlyRequest.ExtensionEligible, selected.Value.ExtensionEligible);
    }
}
