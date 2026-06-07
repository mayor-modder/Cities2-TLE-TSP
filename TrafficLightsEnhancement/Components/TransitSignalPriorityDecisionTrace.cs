using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Components;

public struct TransitSignalPriorityDecisionTrace : IComponentData
{
    public byte m_RequestTargetSignalGroup;
    public byte m_SelectedSignalGroup;
    public byte m_BaseSignalGroup;
    public byte m_SourceType;
    public bool m_OnDedicatedLane;
    public byte m_Reason;
    public bool m_ExclusivePedestrianEnabled;
    public bool m_ActiveExclusivePedestrianPhase;
    public bool m_PendingPedestrianFairness;
    public byte m_PendingPedestrianSignalGroup;
    public bool m_PendingVehicleFairness;
    public byte m_PendingVehicleSignalGroup;
}
