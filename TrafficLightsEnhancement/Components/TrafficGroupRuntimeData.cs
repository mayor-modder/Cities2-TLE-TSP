using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Components;

/// <summary>
/// Runtime-only traffic-group scheduling data reconstructed after loading.
/// </summary>
public struct TrafficGroupRuntimeData : IComponentData
{
    public uint m_LeaderUpdateFrameIndex;
}
