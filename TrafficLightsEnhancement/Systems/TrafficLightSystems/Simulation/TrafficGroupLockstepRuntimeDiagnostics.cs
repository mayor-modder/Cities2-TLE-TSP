using C2VM.TrafficLightsEnhancement.Components;
using Colossal.Entities;
using Game.Net;
using Unity.Collections;
using Unity.Entities;
using TrafficLightsEnhancement.Logic.TrafficGroups;
using RenderedTrafficLight = Game.Objects.TrafficLight;
using SubObject = Game.Objects.SubObject;

namespace C2VM.TrafficLightsEnhancement.Systems.TrafficLightSystems.Simulation;

internal static class TrafficGroupLockstepRuntimeDiagnostics
{
    public static TrafficGroupLockstepControllerSnapshot Snapshot(
        TrafficLights trafficLights,
        CustomTrafficLights customTrafficLights)
    {
        return new TrafficGroupLockstepControllerSnapshot(
            (byte)trafficLights.m_State,
            trafficLights.m_CurrentSignalGroup,
            trafficLights.m_NextSignalGroup,
            trafficLights.m_Timer,
            customTrafficLights.m_Timer,
            trafficLights.m_SignalGroupCount);
    }

    public static ulong HashLaneSignals(
        NativeList<Entity> laneEntities,
        ComponentLookup<LaneSignal> laneSignals,
        ComponentLookup<ExtraLaneSignal> extraLaneSignals)
    {
        ulong hash = TrafficGroupLockstepDiagnostics.FnvOffsetBasis;
        for (int index = 0; index < laneEntities.Length; index++)
        {
            Entity entity = laneEntities[index];
            hash = AddEntity(hash, entity);
            if (!laneSignals.TryGetComponent(entity, out LaneSignal signal))
            {
                hash = TrafficGroupLockstepDiagnostics.AddHash(hash, ulong.MaxValue);
                continue;
            }

            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, signal.m_GroupMask);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, (byte)signal.m_Flags);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, (byte)signal.m_Signal);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, unchecked((byte)signal.m_Default));
            hash = AddEntity(hash, signal.m_Petitioner);
            hash = AddEntity(hash, signal.m_Blocker);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, unchecked((byte)signal.m_Priority));

            ExtraLaneSignal extra = extraLaneSignals.TryGetComponent(
                entity,
                out ExtraLaneSignal existing)
                ? existing
                : default;
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, extra.m_YieldGroupMask);
            hash = TrafficGroupLockstepDiagnostics.AddHash(
                hash,
                extra.m_IgnorePriorityGroupMask);
        }

        return hash;
    }

    public static ulong HashRenderedLights(
        DynamicBuffer<SubObject> subObjects,
        ComponentLookup<RenderedTrafficLight> renderedLights)
    {
        ulong hash = TrafficGroupLockstepDiagnostics.FnvOffsetBasis;
        for (int index = 0; index < subObjects.Length; index++)
        {
            Entity entity = subObjects[index].m_SubObject;
            if (!renderedLights.TryGetComponent(entity, out RenderedTrafficLight light))
            {
                continue;
            }

            hash = AddEntity(hash, entity);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, light.m_GroupMask0);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, light.m_GroupMask1);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, (ushort)light.m_State);
        }

        return hash;
    }

    public static int CountRenderedLights(
        DynamicBuffer<SubObject> subObjects,
        ComponentLookup<RenderedTrafficLight> renderedLights)
    {
        int count = 0;
        for (int index = 0; index < subObjects.Length; index++)
        {
            if (renderedLights.HasComponent(subObjects[index].m_SubObject))
            {
                count++;
            }
        }

        return count;
    }

    public static ulong HashLaneSignals(
        DynamicBuffer<SubLane> subLanes,
        EntityManager entityManager)
    {
        ulong hash = TrafficGroupLockstepDiagnostics.FnvOffsetBasis;
        for (int index = 0; index < subLanes.Length; index++)
        {
            Entity entity = subLanes[index].m_SubLane;
            if (!entityManager.TryGetComponent(entity, out LaneSignal signal))
            {
                continue;
            }

            hash = AddEntity(hash, entity);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, signal.m_GroupMask);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, (byte)signal.m_Flags);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, (byte)signal.m_Signal);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, unchecked((byte)signal.m_Default));
            hash = AddEntity(hash, signal.m_Petitioner);
            hash = AddEntity(hash, signal.m_Blocker);
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, unchecked((byte)signal.m_Priority));

            ExtraLaneSignal extra = entityManager.TryGetComponent(
                entity,
                out ExtraLaneSignal existing)
                ? existing
                : default;
            hash = TrafficGroupLockstepDiagnostics.AddHash(hash, extra.m_YieldGroupMask);
            hash = TrafficGroupLockstepDiagnostics.AddHash(
                hash,
                extra.m_IgnorePriorityGroupMask);
        }

        return hash;
    }

    public static ulong HashRenderedLights(
        DynamicBuffer<SubObject> subObjects,
        EntityManager entityManager)
    {
        ulong hash = TrafficGroupLockstepDiagnostics.FnvOffsetBasis;
        for (int index = 0; index < subObjects.Length; index++)
        {
            Entity entity = subObjects[index].m_SubObject;
            if (!entityManager.TryGetComponent(
                    entity,
                    out RenderedTrafficLight rendered))
            {
                continue;
            }

            hash = AddEntity(hash, entity);
            hash = TrafficGroupLockstepDiagnostics.AddHash(
                hash,
                rendered.m_GroupMask0);
            hash = TrafficGroupLockstepDiagnostics.AddHash(
                hash,
                rendered.m_GroupMask1);
            hash = TrafficGroupLockstepDiagnostics.AddHash(
                hash,
                (ushort)rendered.m_State);
        }

        return hash;
    }

    private static ulong AddEntity(ulong hash, Entity entity)
    {
        hash = TrafficGroupLockstepDiagnostics.AddHash(
            hash,
            unchecked((uint)entity.Index));
        return TrafficGroupLockstepDiagnostics.AddHash(
            hash,
            unchecked((uint)entity.Version));
    }
}
