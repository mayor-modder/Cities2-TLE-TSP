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
    public void Indexed_track_detection_reports_upstream_lane_match_when_only_upstream_sample_qualifies()
    {
        var match = EarlyApproachDetection.EvaluateIndexedTrackTramSamples(
            hasApproachSample: true,
            approachCurvePosition: 0.1f,
            hasUpstreamSample: true,
            upstreamCurvePosition: 0.95f,
            approachLaneThreshold: 0.2f,
            upstreamLaneThreshold: 0.9f);

        Assert.Equal(IndexedTrackProbeMatch.MatchOnUpstreamLane, match);
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
    public void Bus_early_probe_reports_no_lane_objects_when_buffer_is_empty()
    {
        BusEarlyProbeResult result = EarlyApproachDetection.EvaluateBusEarlyProbe(
            laneObjectCount: 0,
            publicTransportObjectCount: 0,
            matchedApproachLane: false,
            reachedThreshold: false,
            blocked: false,
            reachedLaneEnd: false,
            suppressionFlags: TransitApproachSuppressionFlags.None);

        Assert.Equal(BusEarlyProbeResult.NoLaneObjects, result);
    }

    [Fact]
    public void Bus_early_probe_reports_lane_mismatch_when_public_transport_exists_but_not_on_scanned_lane()
    {
        BusEarlyProbeResult result = EarlyApproachDetection.EvaluateBusEarlyProbe(
            laneObjectCount: 2,
            publicTransportObjectCount: 1,
            matchedApproachLane: false,
            reachedThreshold: false,
            blocked: false,
            reachedLaneEnd: false,
            suppressionFlags: TransitApproachSuppressionFlags.None);

        Assert.Equal(BusEarlyProbeResult.CurrentLaneMismatch, result);
    }

    [Fact]
    public void Bus_petitioner_probe_reports_missing_petitioner_when_signal_has_none()
    {
        BusPetitionerProbeResult result = EarlyApproachDetection.EvaluateBusPetitionerProbe(
            petitionerExists: false,
            petitionerHasPublicTransport: false,
            petitionerFrontLaneMatches: false,
            petitionerRearLaneMatches: false);

        Assert.Equal(BusPetitionerProbeResult.MissingPetitioner, result);
    }

    [Fact]
    public void Bus_petitioner_probe_reports_match_when_public_transport_petitioner_reaches_the_lane()
    {
        BusPetitionerProbeResult result = EarlyApproachDetection.EvaluateBusPetitionerProbe(
            petitionerExists: true,
            petitionerHasPublicTransport: true,
            petitionerFrontLaneMatches: true,
            petitionerRearLaneMatches: false);

        Assert.Equal(BusPetitionerProbeResult.Match, result);
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

    [Fact]
    public void Road_transit_early_detection_enables_for_public_only_lane()
    {
        bool enabled = EarlyApproachDetection.ShouldEvaluateRoadTransitEarlyDetection(isPublicCarLane: true);

        Assert.True(enabled);
    }

    [Fact]
    public void Road_transit_early_detection_stays_disabled_for_non_public_lane()
    {
        bool enabled = EarlyApproachDetection.ShouldEvaluateRoadTransitEarlyDetection(isPublicCarLane: false);

        Assert.False(enabled);
    }

    [Fact]
    public void Reported_track_probe_diagnostics_preserve_petitioner_fallback_snapshot()
    {
        var earlyDiagnostics = new IndexedTrackProbeDiagnostics(
            IndexedTrackProbeMatch.MatchOnApproachLane,
            IndexedTrackProbeMatch.MatchOnApproachLane,
            IndexedTrackProbeMatch.NoTramSamples);
        var petitionerDiagnostics = new IndexedTrackProbeDiagnostics(
            IndexedTrackProbeMatch.NoTramSamples,
            IndexedTrackProbeMatch.BelowThreshold,
            IndexedTrackProbeMatch.NoTramSamples);

        IndexedTrackProbeDiagnostics reported = EarlyApproachDetection.SelectReportedTrackProbeDiagnostics(
            selectedEarlyRequest: false,
            earlyDiagnostics,
            selectedPetitionerRequest: true,
            petitionerDiagnostics);

        Assert.Equal(IndexedTrackProbeMatch.NoTramSamples, reported.SignaledLane);
        Assert.Equal(IndexedTrackProbeMatch.BelowThreshold, reported.ApproachLane);
        Assert.Equal(IndexedTrackProbeMatch.NoTramSamples, reported.UpstreamLane);
    }

    [Fact]
    public void Reported_track_probe_diagnostics_clear_when_no_fresh_request_was_selected()
    {
        var earlyDiagnostics = new IndexedTrackProbeDiagnostics(
            IndexedTrackProbeMatch.NoTramSamples,
            IndexedTrackProbeMatch.BelowThreshold,
            IndexedTrackProbeMatch.NoTramSamples);
        var petitionerDiagnostics = new IndexedTrackProbeDiagnostics(
            IndexedTrackProbeMatch.NoTramSamples,
            IndexedTrackProbeMatch.BelowThreshold,
            IndexedTrackProbeMatch.NoTramSamples);

        IndexedTrackProbeDiagnostics reported = EarlyApproachDetection.SelectReportedTrackProbeDiagnostics(
            selectedEarlyRequest: false,
            earlyDiagnostics,
            selectedPetitionerRequest: false,
            petitionerDiagnostics);

        Assert.Equal(IndexedTrackProbeMatch.None, reported.SignaledLane);
        Assert.Equal(IndexedTrackProbeMatch.None, reported.ApproachLane);
        Assert.Equal(IndexedTrackProbeMatch.None, reported.UpstreamLane);
    }

    [Fact]
    public void Track_lane_source_resolution_uses_recursive_lookup()
    {
        bool recursive = EarlyApproachDetection.ShouldResolveSourceLaneRecursively(
            isTrackLane: true,
            isPedestrianCrosswalk: false);

        Assert.True(recursive);
    }

    [Fact]
    public void Crosswalk_source_resolution_stays_recursive()
    {
        bool recursive = EarlyApproachDetection.ShouldResolveSourceLaneRecursively(
            isTrackLane: false,
            isPedestrianCrosswalk: true);

        Assert.True(recursive);
    }

    [Fact]
    public void Ordinary_road_lane_source_resolution_stays_non_recursive()
    {
        bool recursive = EarlyApproachDetection.ShouldResolveSourceLaneRecursively(
            isTrackLane: false,
            isPedestrianCrosswalk: false);

        Assert.False(recursive);
    }

    [Fact]
    public void Track_crosswalk_overlap_still_resolves_recursively()
    {
        bool recursive = EarlyApproachDetection.ShouldResolveSourceLaneRecursively(
            isTrackLane: true,
            isPedestrianCrosswalk: true);

        Assert.True(recursive);
    }

    [Fact]
    public void Connected_upstream_edge_candidate_requires_a_different_edge_feeding_the_same_start_node()
    {
        bool candidate = EarlyApproachDetection.IsConnectedUpstreamEdgeCandidate(
            currentEdgeIndex: 10,
            candidateEdgeIndex: 11,
            candidateLaneEndOwnerIndex: 42,
            baseLaneStartOwnerIndex: 42);

        Assert.True(candidate);
    }

    [Fact]
    public void Connected_upstream_edge_candidate_rejects_same_edge_or_wrong_node()
    {
        Assert.False(EarlyApproachDetection.IsConnectedUpstreamEdgeCandidate(
            currentEdgeIndex: 10,
            candidateEdgeIndex: 10,
            candidateLaneEndOwnerIndex: 42,
            baseLaneStartOwnerIndex: 42));

        Assert.False(EarlyApproachDetection.IsConnectedUpstreamEdgeCandidate(
            currentEdgeIndex: 10,
            candidateEdgeIndex: 11,
            candidateLaneEndOwnerIndex: 41,
            baseLaneStartOwnerIndex: 42));
    }

    [Fact]
    public void Path_node_owner_index_resolves_to_matching_edge_node()
    {
        bool resolved = EarlyApproachDetection.TryResolvePathNodeOwnerEntityIndex(
            pathNodeOwnerIndex: 7,
            edgeStartNodeIndex: 7,
            edgeEndNodeIndex: 9,
            out int nodeIndex);

        Assert.True(resolved);
        Assert.Equal(7, nodeIndex);
    }

    [Fact]
    public void Path_node_owner_index_returns_false_when_edge_does_not_own_the_node()
    {
        bool resolved = EarlyApproachDetection.TryResolvePathNodeOwnerEntityIndex(
            pathNodeOwnerIndex: 8,
            edgeStartNodeIndex: 7,
            edgeEndNodeIndex: 9,
            out int nodeIndex);

        Assert.False(resolved);
        Assert.Equal(-1, nodeIndex);
    }
}
