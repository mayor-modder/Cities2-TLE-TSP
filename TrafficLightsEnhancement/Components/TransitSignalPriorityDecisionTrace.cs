using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Components;

public enum TransitSignalPriorityRequestOrigin : byte
{
    Local = 0,
    GroupedPropagation = 1,
}

public struct TransitSignalPriorityDecisionTrace : IComponentData
{
    public byte m_RequestTargetSignalGroup;
    public byte m_SelectedSignalGroup;
    public byte m_BaseSignalGroup;
    public byte m_SourceType;
    public byte m_Reason;
    public byte m_RequestOrigin;
}
