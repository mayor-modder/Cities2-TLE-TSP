using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Components;

public enum TransitSignalPriorityRequestKind : byte
{
    None = 0,
    FreshEarly = 1,
    FreshPetitioner = 2,
    LatchedExisting = 3,
    GroupedPropagation = 4,
}

public enum TransitSignalPriorityApproachLaneRole : byte
{
    None = 0,
    ApproachLane = 1,
    UpstreamLane = 2,
}

public enum TransitSignalPriorityTrackProbeResult : byte
{
    None = 0,
    NoTramSamples = 1,
    BelowThreshold = 2,
    MatchOnApproachLane = 3,
    MatchOnUpstreamLane = 4,
    MatchOnConnectedApproachLane = 5,
}

public struct TransitSignalPriorityRuntimeDebugInfo : IComponentData
{
    public byte m_RequestKind;
    public byte m_ApproachLaneRole;
    public byte m_SourceType;
    public byte m_TargetSignalGroup;
    public float m_Strength;
    public uint m_ExpiryTimer;
    public bool m_ExtendCurrentPhase;
    public bool m_HasEarlyCandidate;
    public bool m_HasPetitionerCandidate;
    public bool m_HadExistingRequest;
    // Encoded BusEarlyProbeResult.
    public byte m_BusEarlyProbeResult;
    // Encoded BusPetitionerProbeResult.
    public byte m_BusPetitionerProbeResult;
    // Encoded TransitApproachSuppressionFlags.
    public byte m_BusSuppressionFlags;
    public int m_BusLaneObjectCount;
    public int m_BusPublicTransportObjectCount;
    public int m_BusCurrentLaneFlags;
    public Entity m_BusSignaledLaneEntity;
    public Entity m_BusApproachLaneEntity;
    public Entity m_BusCurrentLaneEntity;
    public Entity m_BusMatchedVehicleEntity;
    public Entity m_BusPetitionerEntity;
    public byte m_TrackSignaledLaneProbe;
    public byte m_TrackApproachLaneProbe;
    public byte m_TrackUpstreamLaneProbe;
    public int m_TramApproachIndexLaneCount;
    public Entity m_TrackSignaledLaneEntity;
    public Entity m_TrackApproachLaneEntity;
    public Entity m_TrackUpstreamLaneEntity;
    public Entity m_TrackSignaledLaneOwnerEntity;
    public Entity m_TrackApproachLaneOwnerEntity;
    public Entity m_TrackUpstreamLaneOwnerEntity;
    public byte m_TrackSignaledSiblingSampleCount;
    public byte m_TrackApproachSiblingSampleCount;
    public byte m_TrackUpstreamSiblingSampleCount;
    public bool m_TrackSignaledLaneIsMaster;
    public bool m_TrackApproachLaneIsMaster;
    public bool m_TrackUpstreamLaneIsMaster;
    public byte m_FallbackConnectedEdgeCount;
    public byte m_FallbackTramSublaneCount;
    public byte m_FallbackPathNodeMatchCount;
    public byte m_FallbackIndexHitCount;
    public float m_FallbackBestCurvePosition;
}
