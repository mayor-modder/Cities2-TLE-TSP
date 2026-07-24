using TrafficLightsEnhancement.Logic.TrafficGroups;
using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Components;

/// <summary>
/// Runtime-only map from the group leader's physical movements to this member's phases.
/// </summary>
public struct TrafficGroupPhaseMapping : IComponentData
{
    public TrafficGroupPhaseMap m_Map;
}
