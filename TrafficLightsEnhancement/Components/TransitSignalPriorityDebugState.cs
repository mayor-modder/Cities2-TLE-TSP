using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Components;

public struct TransitSignalPriorityDebugState : IComponentData
{
    public bool m_HadRequest;
    public byte m_TargetSignalGroup;
    public byte m_SourceType;
    public float m_Strength;
    public bool m_ExtendCurrentPhase;
    public byte m_RequestKind;
    public byte m_ApproachLaneRole;
    public bool m_HasEarlyCandidate;
    public bool m_HasPetitionerCandidate;
    public bool m_HadExistingRequest;
    public byte m_TrackSignaledLaneProbe;
    public byte m_TrackApproachLaneProbe;
    public byte m_TrackUpstreamLaneProbe;
}
