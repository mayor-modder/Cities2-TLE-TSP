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

    public static bool IsEligibleTramApproachLane<TEntity>(
        TEntity currentLane,
        TEntity approachLane,
        TEntity upstreamLane,
        TEntity nullLane)
        where TEntity : struct, IEquatable<TEntity>
    {
        return currentLane.Equals(approachLane)
            || (!upstreamLane.Equals(nullLane) && currentLane.Equals(upstreamLane));
    }

    public static bool IsEligibleTramApproachState(
        bool frontMatchesApproachLane,
        bool frontMatchesUpstreamLane,
        float frontCurvePosition,
        bool rearMatchesApproachLane,
        bool rearMatchesUpstreamLane,
        float rearCurvePosition,
        bool isVehicleMoving,
        float approachLaneThreshold,
        float upstreamLaneThreshold)
    {
        if (!isVehicleMoving)
        {
            return false;
        }

        return HasReachedEligibleTramApproachThreshold(
                frontMatchesApproachLane,
                frontMatchesUpstreamLane,
                frontCurvePosition,
                approachLaneThreshold,
                upstreamLaneThreshold)
            || HasReachedEligibleTramApproachThreshold(
                rearMatchesApproachLane,
                rearMatchesUpstreamLane,
                rearCurvePosition,
                approachLaneThreshold,
                upstreamLaneThreshold);
    }

    public static bool HasReachedEligibleTramApproachThreshold(
        bool matchesApproachLane,
        bool matchesUpstreamLane,
        float curvePosition,
        float approachLaneThreshold,
        float upstreamLaneThreshold)
    {
        return (matchesApproachLane && curvePosition >= approachLaneThreshold)
            || (matchesUpstreamLane && curvePosition >= upstreamLaneThreshold);
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
