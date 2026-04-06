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
    public byte m_TrackSignaledLaneProbe;
    public byte m_TrackApproachLaneProbe;
    public byte m_TrackUpstreamLaneProbe;
}
