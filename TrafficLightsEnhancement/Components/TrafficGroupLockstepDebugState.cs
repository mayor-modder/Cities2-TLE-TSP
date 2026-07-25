using TrafficLightsEnhancement.Logic.TrafficGroups;
using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Components;

/// <summary>
/// Transient evidence captured around one grouped traffic-light simulation update.
/// This component is intentionally not serialized.
/// </summary>
public struct TrafficGroupLockstepDebugState : IComponentData
{
    public uint SimulationFrame;
    public uint MemberUpdateFrame;
    public uint LeaderUpdateFrame;
    public TrafficGroupLockstepPassFlags PassFlags;
    public TrafficGroupLockstepSyncDisposition SyncDisposition;
    public bool IsCoordinated;
    public bool IsGreenWave;
    public bool HasCompleteMapping;
    public byte MappedCurrentGroup;
    public byte MappedNextGroup;
    public TrafficGroupLockstepControllerSnapshot Before;
    public TrafficGroupLockstepControllerSnapshot Master;
    public TrafficGroupLockstepControllerSnapshot After;
    public ulong LaneHashBefore;
    public ulong LaneHashAfter;
    public ulong RenderedHashBefore;
    public ulong RenderedHashAfter;
    public int LaneCount;
    public int RenderedCount;
}
