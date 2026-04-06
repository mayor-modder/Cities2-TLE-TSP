using System.Runtime.CompilerServices;
using C2VM.TrafficLightsEnhancement.Components;
using Game.Net;
using Game.Prefabs;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using NetCarLane = Game.Net.CarLane;
using NetPedestrianLane = Game.Net.PedestrianLane;
using NetSecondaryLane = Game.Net.SecondaryLane;
using NetTrackLane = Game.Net.TrackLane;
using PrefabRef = Game.Prefabs.PrefabRef;
using VehiclePublicTransport = Game.Vehicles.PublicTransport;

namespace C2VM.TrafficLightsEnhancement.Systems.TrafficLightSystems.Simulation;

public struct ExtraTypeHandle
{
    public ComponentTypeHandle<CustomTrafficLights> m_CustomTrafficLights;

    [ReadOnly]
    public ComponentLookup<ExtraLaneSignal> m_ExtraLaneSignal;

    public BufferTypeHandle<CustomPhaseData> m_CustomPhaseData;

    [ReadOnly]
    public BufferTypeHandle<EdgeGroupMask> m_EdgeGroupMask;

    [ReadOnly]
    public ComponentLookup<LaneFlow> m_LaneFlow;

    public ComponentLookup<LaneFlowHistory> m_LaneFlowHistory;

    [ReadOnly]
    public ComponentLookup<MasterLane> m_MasterLane;

    [ReadOnly]
    public ComponentLookup<NetCarLane> m_CarLane;

    [ReadOnly]
    public ComponentLookup<NetTrackLane> m_TrackLane;

    [ReadOnly]
    public ComponentLookup<PrefabRef> m_PrefabRef;

    [ReadOnly]
    public ComponentLookup<TrackLaneData> m_TrackLaneData;

    [ReadOnly]
    public ComponentLookup<VehiclePublicTransport> m_PublicTransport;

    [ReadOnly]
    public ComponentLookup<TrainNavigation> m_TrainNavigation;

    [ReadOnly]
    public ComponentLookup<TrainCurrentLane> m_TrainCurrentLane;

    [ReadOnly]
    public ComponentLookup<CarCurrentLane> m_CarCurrentLane;

    [ReadOnly]
    public ComponentLookup<NetPedestrianLane> m_PedestrianLane;
    
    public ComponentLookup<NetSecondaryLane> m_SecondaryLane;

    [ReadOnly]
    public ComponentLookup<TrafficGroupMember> m_TrafficGroupMember;

    [ReadOnly]
    public ComponentLookup<TrafficGroup> m_TrafficGroup;

    [ReadOnly]
    public BufferLookup<CustomPhaseData> m_CustomPhaseDataLookup;

    [ReadOnly]
    public ComponentLookup<Game.Net.TrafficLights> m_TrafficLightsLookup;

    [ReadOnly]
    public ComponentLookup<CustomTrafficLights> m_CustomTrafficLightsLookup;

    public ComponentLookup<TransitSignalPrioritySettings> m_TransitSignalPrioritySettings;

    public ComponentLookup<TransitSignalPriorityRequest> m_TransitSignalPriorityRequest;

    public ComponentLookup<TransitSignalPriorityRuntimeDebugInfo> m_TransitSignalPriorityRuntimeDebugInfo;

    [ReadOnly]
    public ComponentLookup<GroupedTransitSignalPriorityRequest> m_GroupedTransitSignalPriorityRequest;

    [ReadOnly]
    public ComponentLookup<TransitSignalPrioritySettings> m_TransitSignalPrioritySettingsLookup;

    [ReadOnly]
    public ComponentLookup<TrafficGroupTspState> m_TrafficGroupTspState;

    public ComponentLookup<TransitSignalPriorityDecisionTrace> m_TransitSignalPriorityDecisionTrace;

    [ReadOnly]
    public BufferLookup<EdgeGroupMask> m_EdgeGroupMaskLookup;

    [ReadOnly]
    public BufferLookup<SignalDelayData> m_SignalDelayLookup;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AssignHandles(ref SystemState state)
    {
        m_CustomTrafficLights = state.GetComponentTypeHandle<CustomTrafficLights>(isReadOnly: false);
        m_ExtraLaneSignal = state.GetComponentLookup<ExtraLaneSignal>(isReadOnly: true);
        m_CustomPhaseData = state.GetBufferTypeHandle<CustomPhaseData>(isReadOnly: false);
        m_EdgeGroupMask = state.GetBufferTypeHandle<EdgeGroupMask>(isReadOnly: true);
        m_CustomPhaseDataLookup = state.GetBufferLookup<CustomPhaseData>(isReadOnly: true);
        m_LaneFlow = state.GetComponentLookup<LaneFlow>(isReadOnly: true);
        m_LaneFlowHistory = state.GetComponentLookup<LaneFlowHistory>(isReadOnly: false);
        m_MasterLane = state.GetComponentLookup<MasterLane>(isReadOnly: true);
        m_CarLane = state.GetComponentLookup<NetCarLane>(isReadOnly: true);
        m_TrackLane = state.GetComponentLookup<NetTrackLane>(isReadOnly: true);
        m_PrefabRef = state.GetComponentLookup<PrefabRef>(isReadOnly: true);
        m_TrackLaneData = state.GetComponentLookup<TrackLaneData>(isReadOnly: true);
        m_PublicTransport = state.GetComponentLookup<VehiclePublicTransport>(isReadOnly: true);
        m_TrainNavigation = state.GetComponentLookup<TrainNavigation>(isReadOnly: true);
        m_TrainCurrentLane = state.GetComponentLookup<TrainCurrentLane>(isReadOnly: true);
        m_CarCurrentLane = state.GetComponentLookup<CarCurrentLane>(isReadOnly: true);
        m_PedestrianLane = state.GetComponentLookup<NetPedestrianLane>(isReadOnly: true);
        m_SecondaryLane = state.GetComponentLookup<NetSecondaryLane>(isReadOnly: true);
        m_TrafficGroupMember = state.GetComponentLookup<TrafficGroupMember>(isReadOnly: true);
        m_TrafficGroup = state.GetComponentLookup<TrafficGroup>(isReadOnly: true);
        m_CustomPhaseDataLookup = state.GetBufferLookup<CustomPhaseData>(isReadOnly: true);
        m_TrafficLightsLookup = state.GetComponentLookup<Game.Net.TrafficLights>(isReadOnly: true);
        m_CustomTrafficLightsLookup = state.GetComponentLookup<CustomTrafficLights>(isReadOnly: true);
        m_TransitSignalPrioritySettings = state.GetComponentLookup<TransitSignalPrioritySettings>(isReadOnly: false);
        m_TransitSignalPriorityRequest = state.GetComponentLookup<TransitSignalPriorityRequest>(isReadOnly: false);
        m_TransitSignalPriorityRuntimeDebugInfo = state.GetComponentLookup<TransitSignalPriorityRuntimeDebugInfo>(isReadOnly: false);
        m_GroupedTransitSignalPriorityRequest = state.GetComponentLookup<GroupedTransitSignalPriorityRequest>(isReadOnly: true);
        m_TransitSignalPrioritySettingsLookup = state.GetComponentLookup<TransitSignalPrioritySettings>(isReadOnly: true);
        m_TrafficGroupTspState = state.GetComponentLookup<TrafficGroupTspState>(isReadOnly: true);
        m_TransitSignalPriorityDecisionTrace = state.GetComponentLookup<TransitSignalPriorityDecisionTrace>(isReadOnly: false);
        m_EdgeGroupMaskLookup = state.GetBufferLookup<EdgeGroupMask>(isReadOnly: true);
        m_SignalDelayLookup = state.GetBufferLookup<SignalDelayData>(isReadOnly: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ExtraTypeHandle Update(ref SystemState state)
    {
        m_CustomTrafficLights.Update(ref state);
        m_ExtraLaneSignal.Update(ref state);
        m_CustomPhaseData.Update(ref state);
        m_EdgeGroupMask.Update(ref state);
        m_LaneFlow.Update(ref state);
        m_LaneFlowHistory.Update(ref state);
        m_MasterLane.Update(ref state);
        m_CarLane.Update(ref state);
        m_TrackLane.Update(ref state);
        m_PrefabRef.Update(ref state);
        m_TrackLaneData.Update(ref state);
        m_PublicTransport.Update(ref state);
        m_TrainNavigation.Update(ref state);
        m_TrainCurrentLane.Update(ref state);
        m_CarCurrentLane.Update(ref state);
        m_PedestrianLane.Update(ref state);
        m_SecondaryLane.Update(ref state);
        m_TrafficGroupMember.Update(ref state);
        m_TrafficGroup.Update(ref state);
        m_CustomPhaseDataLookup.Update(ref state);
        m_TrafficLightsLookup.Update(ref state);
        m_CustomTrafficLightsLookup.Update(ref state);
        m_TransitSignalPrioritySettings.Update(ref state);
        m_TransitSignalPriorityRequest.Update(ref state);
        m_TransitSignalPriorityRuntimeDebugInfo.Update(ref state);
        m_GroupedTransitSignalPriorityRequest.Update(ref state);
        m_TransitSignalPrioritySettingsLookup.Update(ref state);
        m_TrafficGroupTspState.Update(ref state);
        m_TransitSignalPriorityDecisionTrace.Update(ref state);
        m_EdgeGroupMaskLookup.Update(ref state);
        m_SignalDelayLookup.Update(ref state);
        return this;
    }
}
