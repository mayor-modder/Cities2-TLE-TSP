namespace TrafficLightsEnhancement.Logic.Tsp;

[Flags]
public enum TransitApproachSuppressionFlags : byte
{
    None = 0,
    Boarding = 1 << 0,
    Arriving = 1 << 1,
    RequireStop = 1 << 2,
}

public enum IndexedTrackProbeMatch : byte
{
    None = 0,
    NoTramSamples = 1,
    BelowThreshold = 2,
    MatchOnApproachLane = 3,
    MatchOnUpstreamLane = 4,
}

public struct TransitApproachScanState
{
    public TspRequest? EarlyRequest { get; set; }
    public TspRequest? PetitionerRequest { get; set; }
}

public readonly struct IndexedTrackProbeDiagnostics
{
    public IndexedTrackProbeDiagnostics(
        IndexedTrackProbeMatch signaledLane,
        IndexedTrackProbeMatch approachLane,
        IndexedTrackProbeMatch upstreamLane)
    {
        SignaledLane = signaledLane;
        ApproachLane = approachLane;
        UpstreamLane = upstreamLane;
    }

    public IndexedTrackProbeMatch SignaledLane { get; }

    public IndexedTrackProbeMatch ApproachLane { get; }

    public IndexedTrackProbeMatch UpstreamLane { get; }
}

public static class EarlyApproachDetection
{
    public static TEntity ResolveApproachLane<TEntity>(TEntity signaledLane, TEntity sourceLane, TEntity nullLane)
        where TEntity : struct, IEquatable<TEntity>
    {
        return sourceLane.Equals(nullLane) ? signaledLane : sourceLane;
    }

    public static bool IsMovingEligibleApproachState(bool isEligibleLane, bool isVehicleMoving)
    {
        return IsMovingEligibleApproachState(
            isEligibleLane,
            isVehicleMoving,
            TransitApproachSuppressionFlags.None);
    }

    public static bool IsMovingEligibleApproachState(
        bool isEligibleLane,
        bool isVehicleMoving,
        TransitApproachSuppressionFlags suppressionFlags)
    {
        bool isSuppressed =
            (suppressionFlags & (TransitApproachSuppressionFlags.Boarding
                | TransitApproachSuppressionFlags.Arriving
                | TransitApproachSuppressionFlags.RequireStop)) != 0;

        return isEligibleLane && isVehicleMoving && !isSuppressed;
    }

    public static bool IsEligibleRoadTransitApproachState(
        bool isEligibleLane,
        bool hasReachedApproachThreshold,
        bool isBlocked,
        bool hasReachedLaneEnd,
        TransitApproachSuppressionFlags suppressionFlags)
    {
        bool isSuppressed =
            (suppressionFlags & (TransitApproachSuppressionFlags.Boarding
                | TransitApproachSuppressionFlags.Arriving
                | TransitApproachSuppressionFlags.RequireStop)) != 0;

        return isEligibleLane
            && hasReachedApproachThreshold
            && !isBlocked
            && !hasReachedLaneEnd
            && !isSuppressed;
    }

    public static IndexedTrackProbeMatch EvaluateIndexedTrackTramSamples(
        bool hasApproachSample,
        float approachCurvePosition,
        bool hasUpstreamSample,
        float upstreamCurvePosition,
        float approachLaneThreshold,
        float upstreamLaneThreshold)
    {
        if (hasApproachSample && approachCurvePosition >= approachLaneThreshold)
        {
            return IndexedTrackProbeMatch.MatchOnApproachLane;
        }

        if (hasUpstreamSample && upstreamCurvePosition >= upstreamLaneThreshold)
        {
            return IndexedTrackProbeMatch.MatchOnUpstreamLane;
        }

        if (hasApproachSample || hasUpstreamSample)
        {
            return IndexedTrackProbeMatch.BelowThreshold;
        }

        return IndexedTrackProbeMatch.NoTramSamples;
    }

    public static bool ShouldEvaluateRoadTransitEarlyDetection(bool isPublicCarLane)
    {
        // Road transit early detection is intentionally disabled in this tram-only slice.
        return false;
    }

    public static IndexedTrackProbeDiagnostics SelectReportedTrackProbeDiagnostics(
        bool selectedEarlyRequest,
        IndexedTrackProbeDiagnostics earlyDiagnostics,
        bool selectedPetitionerRequest,
        IndexedTrackProbeDiagnostics petitionerDiagnostics)
    {
        if (selectedEarlyRequest)
        {
            return earlyDiagnostics;
        }

        if (selectedPetitionerRequest)
        {
            return petitionerDiagnostics;
        }

        return default;
    }

    public static TransitApproachScanState RecordLaneRequests(
        TransitApproachScanState state,
        TspRequest? earlyRequest,
        TspRequest? petitionerRequest)
    {
        return new TransitApproachScanState
        {
            EarlyRequest = state.EarlyRequest ?? earlyRequest,
            PetitionerRequest = state.PetitionerRequest ?? petitionerRequest,
        };
    }

    public static TspRequest? PreferEarlyRequest(TspRequest? earlyRequest, TspRequest? petitionerRequest)
    {
        return earlyRequest ?? petitionerRequest;
    }
}
