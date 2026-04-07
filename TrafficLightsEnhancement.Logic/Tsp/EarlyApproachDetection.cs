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

public enum BusEarlyProbeResult : byte
{
    None = 0,
    NoLaneObjects = 1,
    NoPublicTransportLaneObjects = 2,
    CurrentLaneMismatch = 3,
    Suppressed = 4,
    BelowThreshold = 5,
    Match = 6,
}

public enum BusUpstreamDiscovery : byte
{
    None = 0,
    NoOwner = 1,
    NoLaneData = 2,
    SiblingMatch = 3,
    ConnectedEdgeMatch = 4,
    BothMatch = 5,
    NoCandidates = 6,
}

public enum BusPetitionerProbeResult : byte
{
    None = 0,
    MissingPetitioner = 1,
    NotPublicTransport = 2,
    LaneMismatch = 3,
    Match = 4,
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
        return isPublicCarLane;
    }

    public static TEntity ResolveRoadTransitProbeLane<TEntity>(
        TEntity approachLane,
        TEntity siblingUpstreamLane,
        TEntity connectedEdgeUpstreamLane,
        TEntity nullLane)
        where TEntity : struct, IEquatable<TEntity>
    {
        if (!siblingUpstreamLane.Equals(nullLane))
        {
            return siblingUpstreamLane;
        }

        if (!connectedEdgeUpstreamLane.Equals(nullLane))
        {
            return connectedEdgeUpstreamLane;
        }

        return approachLane;
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

    public static BusEarlyProbeResult EvaluateBusEarlyProbe(
        int laneObjectCount,
        int publicTransportObjectCount,
        bool matchedApproachLane,
        bool reachedThreshold,
        bool blocked,
        bool reachedLaneEnd,
        TransitApproachSuppressionFlags suppressionFlags)
    {
        if (laneObjectCount == 0)
        {
            return BusEarlyProbeResult.NoLaneObjects;
        }

        if (publicTransportObjectCount == 0)
        {
            return BusEarlyProbeResult.NoPublicTransportLaneObjects;
        }

        if (!matchedApproachLane)
        {
            return BusEarlyProbeResult.CurrentLaneMismatch;
        }

        if ((suppressionFlags & (TransitApproachSuppressionFlags.Boarding
            | TransitApproachSuppressionFlags.Arriving
            | TransitApproachSuppressionFlags.RequireStop)) != 0)
        {
            return BusEarlyProbeResult.Suppressed;
        }

        if (!reachedThreshold || blocked || reachedLaneEnd)
        {
            return BusEarlyProbeResult.BelowThreshold;
        }

        return BusEarlyProbeResult.Match;
    }

    public static BusPetitionerProbeResult EvaluateBusPetitionerProbe(
        bool petitionerExists,
        bool petitionerHasPublicTransport,
        bool petitionerFrontLaneMatches,
        bool petitionerRearLaneMatches)
    {
        if (!petitionerExists)
        {
            return BusPetitionerProbeResult.MissingPetitioner;
        }

        if (!petitionerHasPublicTransport)
        {
            return BusPetitionerProbeResult.NotPublicTransport;
        }

        if (!petitionerFrontLaneMatches && !petitionerRearLaneMatches)
        {
            return BusPetitionerProbeResult.LaneMismatch;
        }

        return BusPetitionerProbeResult.Match;
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

    public static bool ShouldResolveSourceLaneRecursively(
        bool isTrackLane,
        bool isPedestrianCrosswalk,
        bool isRoadVehicleLane)
    {
        return isTrackLane || isPedestrianCrosswalk || isRoadVehicleLane;
    }

    public static bool IsConnectedUpstreamEdgeCandidate(
        int currentEdgeIndex,
        int candidateEdgeIndex,
        int candidateLaneEndOwnerIndex,
        int baseLaneStartOwnerIndex)
    {
        return candidateEdgeIndex != currentEdgeIndex
            && candidateLaneEndOwnerIndex == baseLaneStartOwnerIndex;
    }

    public static bool TryResolvePathNodeOwnerEntityIndex(
        int pathNodeOwnerIndex,
        int edgeStartNodeIndex,
        int edgeEndNodeIndex,
        out int nodeIndex)
    {
        if (pathNodeOwnerIndex == edgeStartNodeIndex)
        {
            nodeIndex = edgeStartNodeIndex;
            return true;
        }

        if (pathNodeOwnerIndex == edgeEndNodeIndex)
        {
            nodeIndex = edgeEndNodeIndex;
            return true;
        }

        nodeIndex = -1;
        return false;
    }
}
