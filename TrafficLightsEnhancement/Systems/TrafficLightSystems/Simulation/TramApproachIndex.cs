using C2VM.TrafficLightsEnhancement.Components;
using Game.Prefabs;
using Game.Vehicles;
using TrafficLightsEnhancement.Logic.Tsp;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace C2VM.TrafficLightsEnhancement.Systems.TrafficLightSystems.Simulation;

internal static class TramApproachIndex
{
    private const float MovingTrainSpeedThreshold = 0.5f;

    public static NativeParallelHashMap<Entity, float> Build(
        EntityQuery railTransitQuery,
        ExtraTypeHandle extraTypeHandle,
        Allocator allocator)
    {
        using NativeArray<Entity> railTransitEntities = railTransitQuery.ToEntityArray(Allocator.Temp);
        int capacity = math.max(1, railTransitEntities.Length * 2);
        var index = new NativeParallelHashMap<Entity, float>(capacity, allocator);

        for (int i = 0; i < railTransitEntities.Length; i++)
        {
            Entity vehicleEntity = railTransitEntities[i];
            if (!extraTypeHandle.m_PublicTransport.TryGetComponent(vehicleEntity, out var publicTransport)
                || !extraTypeHandle.m_TrainNavigation.TryGetComponent(vehicleEntity, out var trainNavigation)
                || !extraTypeHandle.m_TrainCurrentLane.TryGetComponent(vehicleEntity, out var trainCurrentLane))
            {
                continue;
            }

            TransitApproachSuppressionFlags suppressionFlags =
                GetSuppressionFlags(publicTransport.m_State);

            if (!EarlyApproachDetection.IsMovingEligibleApproachState(
                    isEligibleLane: true,
                    isVehicleMoving: trainNavigation.m_Speed > MovingTrainSpeedThreshold,
                    suppressionFlags))
            {
                continue;
            }

            TryRecordLaneSample(index, trainCurrentLane.m_Front.m_Lane, trainCurrentLane.m_Front.m_CurvePosition.x, extraTypeHandle);
            TryRecordLaneSample(index, trainCurrentLane.m_Rear.m_Lane, trainCurrentLane.m_Rear.m_CurvePosition.x, extraTypeHandle);
        }

        return index;
    }

    private static void TryRecordLaneSample(
        NativeParallelHashMap<Entity, float> index,
        Entity laneEntity,
        float curvePosition,
        ExtraTypeHandle extraTypeHandle)
    {
        if (laneEntity == Entity.Null || !IsTramTrackLane(extraTypeHandle, laneEntity))
        {
            return;
        }

        if (index.TryGetValue(laneEntity, out float existingCurvePosition) && existingCurvePosition >= curvePosition)
        {
            return;
        }

        index[laneEntity] = curvePosition;
    }

    private static bool IsTramTrackLane(ExtraTypeHandle extraTypeHandle, Entity laneEntity)
    {
        if (!extraTypeHandle.m_TrackLane.HasComponent(laneEntity))
        {
            return false;
        }

        if (!extraTypeHandle.m_PrefabRef.TryGetComponent(laneEntity, out var prefabRef))
        {
            return false;
        }

        return extraTypeHandle.m_TrackLaneData.TryGetComponent(prefabRef.m_Prefab, out var trackLaneData)
            && (trackLaneData.m_TrackTypes & Game.Net.TrackTypes.Tram) != 0;
    }

    private static TransitApproachSuppressionFlags GetSuppressionFlags(PublicTransportFlags state)
    {
        TransitApproachSuppressionFlags flags = TransitApproachSuppressionFlags.None;

        if ((state & PublicTransportFlags.Boarding) != 0) flags |= TransitApproachSuppressionFlags.Boarding;
        if ((state & PublicTransportFlags.Arriving) != 0) flags |= TransitApproachSuppressionFlags.Arriving;
        if ((state & PublicTransportFlags.RequireStop) != 0) flags |= TransitApproachSuppressionFlags.RequireStop;

        return flags;
    }
}
