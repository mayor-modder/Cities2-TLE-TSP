using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Vehicles;
using TrafficLightsEnhancement.Logic.Tsp;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using NetCarLane = Game.Net.CarLane;
using VehicleCarLaneFlags = Game.Vehicles.CarLaneFlags;

namespace C2VM.TrafficLightsEnhancement.Systems.TrafficLightSystems.Simulation;

public struct BusApproachSample
{
    public Entity VehicleEntity;
    public Entity LaneEntity;
    public float CurvePosition;
    public float ChangeProgress;
    public float Speed;
    public byte HitCount;
    public byte IsBusOnlyLane;
    public byte HasChangeLane;
    public byte IsChangeLaneSample;
    public byte HasNavigation;
    public byte NavigationLaneCount;
    public PublicTransportFlags PublicTransportState;
    public VehicleCarLaneFlags VehicleLaneFlags;
}

internal static class BusApproachIndex
{
    public static NativeParallelHashMap<Entity, BusApproachSample> Build(
        EntityQuery busTransitQuery,
        ExtraTypeHandle extraTypeHandle,
        Allocator allocator)
    {
        using NativeArray<Entity> busEntities = busTransitQuery.ToEntityArray(Allocator.Temp);
        int capacity = math.max(1, busEntities.Length * 2);
        var index = new NativeParallelHashMap<Entity, BusApproachSample>(capacity, allocator);

        for (int i = 0; i < busEntities.Length; i++)
        {
            Entity vehicleEntity = busEntities[i];
            if (!extraTypeHandle.m_PublicTransport.TryGetComponent(vehicleEntity, out var publicTransport)
                || !extraTypeHandle.m_CarCurrentLane.TryGetComponent(vehicleEntity, out var carCurrentLane)
                || !extraTypeHandle.m_PrefabRef.TryGetComponent(vehicleEntity, out var prefabRef)
                || !extraTypeHandle.m_PublicTransportVehicleData.TryGetComponent(prefabRef.m_Prefab, out var vehicleData)
                || vehicleData.m_TransportType != TransportType.Bus)
            {
                continue;
            }

            float speed = extraTypeHandle.m_Moving.TryGetComponent(vehicleEntity, out Moving moving)
                ? math.length(moving.m_Velocity)
                : 0f;
            bool hasChangeLane = carCurrentLane.m_ChangeLane != Entity.Null;
            bool hasNavigation = extraTypeHandle.m_CarNavigation.HasComponent(vehicleEntity);
            byte navigationLaneCount = extraTypeHandle.m_CarNavigationLane.TryGetBuffer(vehicleEntity, out var navigationLanes)
                ? BusApproachSampleSelectionPolicy.ToByteCount(navigationLanes.Length)
                : (byte)0;

            TryRecordLaneSample(
                index,
                vehicleEntity,
                carCurrentLane.m_Lane,
                carCurrentLane.m_CurvePosition.x,
                carCurrentLane.m_ChangeProgress,
                speed,
                hasChangeLane,
                isChangeLaneSample: false,
                hasNavigation,
                navigationLaneCount,
                publicTransport.m_State,
                carCurrentLane.m_LaneFlags,
                extraTypeHandle);

            if (hasChangeLane)
            {
                TryRecordLaneSample(
                    index,
                    vehicleEntity,
                    carCurrentLane.m_ChangeLane,
                    carCurrentLane.m_CurvePosition.x,
                    carCurrentLane.m_ChangeProgress,
                    speed,
                    hasChangeLane,
                    isChangeLaneSample: true,
                    hasNavigation,
                    navigationLaneCount,
                    publicTransport.m_State,
                    carCurrentLane.m_LaneFlags,
                    extraTypeHandle);
            }
        }

        return index;
    }

    private static void TryRecordLaneSample(
        NativeParallelHashMap<Entity, BusApproachSample> index,
        Entity vehicleEntity,
        Entity laneEntity,
        float curvePosition,
        float changeProgress,
        float speed,
        bool hasChangeLane,
        bool isChangeLaneSample,
        bool hasNavigation,
        byte navigationLaneCount,
        PublicTransportFlags publicTransportState,
        VehicleCarLaneFlags vehicleLaneFlags,
        ExtraTypeHandle extraTypeHandle)
    {
        if (laneEntity == Entity.Null || !extraTypeHandle.m_CarLane.HasComponent(laneEntity))
        {
            return;
        }

        BusApproachSample sample = new()
        {
            VehicleEntity = vehicleEntity,
            LaneEntity = laneEntity,
            CurvePosition = curvePosition,
            ChangeProgress = changeProgress,
            Speed = speed,
            HitCount = 1,
            IsBusOnlyLane = IsBusOnlyLane(extraTypeHandle, laneEntity) ? (byte)1 : (byte)0,
            HasChangeLane = hasChangeLane ? (byte)1 : (byte)0,
            IsChangeLaneSample = isChangeLaneSample ? (byte)1 : (byte)0,
            HasNavigation = hasNavigation ? (byte)1 : (byte)0,
            NavigationLaneCount = navigationLaneCount,
            PublicTransportState = publicTransportState,
            VehicleLaneFlags = vehicleLaneFlags,
        };
        if (!TransitSignalPriorityRuntime.IsBusPrioritySampleEligible(sample))
        {
            return;
        }

        if (!index.TryGetValue(laneEntity, out BusApproachSample existing))
        {
            index[laneEntity] = sample;
            return;
        }

        bool keepExistingSample = existing.CurvePosition > sample.CurvePosition;
        BusApproachSampleSelectionState selection = BusApproachSampleSelectionPolicy.RecordSample(
            ToSelectionState(existing),
            ToSelectionInput(sample));

        if (keepExistingSample)
        {
            ApplySelection(ref existing, selection);
            index[laneEntity] = existing;
            return;
        }

        ApplySelection(ref sample, selection);
        index[laneEntity] = sample;
    }

    private static bool IsBusOnlyLane(ExtraTypeHandle extraTypeHandle, Entity laneEntity)
    {
        return extraTypeHandle.m_CarLane.TryGetComponent(laneEntity, out NetCarLane carLane)
            && (carLane.m_Flags & Game.Net.CarLaneFlags.PublicOnly) != 0;
    }

    private static BusApproachSampleSelectionInput ToSelectionInput(BusApproachSample sample)
    {
        return new BusApproachSampleSelectionInput(
            sample.CurvePosition,
            sample.IsBusOnlyLane != 0,
            sample.HasChangeLane != 0,
            sample.IsChangeLaneSample != 0,
            sample.HasNavigation != 0,
            sample.NavigationLaneCount);
    }

    private static BusApproachSampleSelectionState ToSelectionState(BusApproachSample sample)
    {
        return new BusApproachSampleSelectionState(
            hasSample: true,
            curvePosition: sample.CurvePosition,
            hitCount: sample.HitCount,
            isBusOnlyLane: sample.IsBusOnlyLane != 0,
            hasChangeLane: sample.HasChangeLane != 0,
            isChangeLaneSample: sample.IsChangeLaneSample != 0,
            hasNavigation: sample.HasNavigation != 0,
            navigationLaneCount: sample.NavigationLaneCount);
    }

    private static void ApplySelection(ref BusApproachSample sample, BusApproachSampleSelectionState selection)
    {
        sample.CurvePosition = selection.CurvePosition;
        sample.HitCount = selection.HitCount;
        sample.IsBusOnlyLane = selection.IsBusOnlyLane ? (byte)1 : (byte)0;
        sample.HasChangeLane = selection.HasChangeLane ? (byte)1 : (byte)0;
        sample.IsChangeLaneSample = selection.IsChangeLaneSample ? (byte)1 : (byte)0;
        sample.HasNavigation = selection.HasNavigation ? (byte)1 : (byte)0;
        sample.NavigationLaneCount = selection.NavigationLaneCount;
    }
}
