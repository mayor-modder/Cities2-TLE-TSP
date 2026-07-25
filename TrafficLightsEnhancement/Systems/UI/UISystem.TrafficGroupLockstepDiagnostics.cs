using System;
using System.Collections;
using System.Collections.Generic;
using C2VM.TrafficLightsEnhancement.Components;
using C2VM.TrafficLightsEnhancement.Systems.TrafficLightSystems.Simulation;
using Colossal.Entities;
using Game.Net;
using Unity.Collections;
using Unity.Entities;
using TrafficLightsEnhancement.Logic.TrafficGroups;
using RenderedTrafficLight = Game.Objects.TrafficLight;
using SubObject = Game.Objects.SubObject;

namespace C2VM.TrafficLightsEnhancement.Systems.UI;

public partial class UISystem
{
    private readonly Dictionary<Entity, string> m_LockstepVerdictWarnings = new();

    private object GetTrafficGroupLockstepTrace(Entity entity)
    {
        if (!EntityManager.TryGetComponent(
                entity,
                out TrafficGroupMember selectedMember)
            || selectedMember.m_GroupEntity == Entity.Null
            || !EntityManager.TryGetComponent(
                selectedMember.m_GroupEntity,
                out TrafficGroup group))
        {
            return null;
        }

        var trafficGroupSystem =
            World.GetOrCreateSystemManaged<TrafficGroupSystem>();
        NativeList<Entity> groupMembers =
            trafficGroupSystem.GetGroupMembers(selectedMember.m_GroupEntity);
        var memberTraces = new ArrayList(groupMembers.Length);
        int completeMappingCount = 0;
        int missingMappingCount = 0;
        try
        {
            foreach (Entity memberEntity in groupMembers)
            {
                if (!EntityManager.Exists(memberEntity))
                {
                    memberTraces.Add(new
                    {
                        entity = FormatEntity(memberEntity),
                        available = false,
                        reason = "Member entity no longer exists.",
                    });
                    continue;
                }

                bool hasCompleteMapping =
                    EntityManager.TryGetComponent(
                        memberEntity,
                        out TrafficGroupPhaseMapping mapping)
                    && mapping.m_Map.IsComplete;
                if (hasCompleteMapping)
                {
                    completeMappingCount++;
                }
                else
                {
                    missingMappingCount++;
                }

                try
                {
                    memberTraces.Add(
                        GetTrafficGroupLockstepMemberTrace(
                            memberEntity,
                            selectedMember.m_GroupEntity));
                }
                catch (Exception ex)
                {
                    memberTraces.Add(new
                    {
                        entity = FormatEntity(memberEntity),
                        available = false,
                        reason = $"Member trace failed: {ex.GetType().Name}: {ex.Message}",
                    });
                }
            }
        }
        finally
        {
            groupMembers.Dispose();
        }

        return new
        {
            groupEntity = FormatEntity(selectedMember.m_GroupEntity),
            selectedEntity = FormatEntity(entity),
            mode = FormatTrafficGroupMode(hasGroup: true, group),
            isCoordinated = group.m_IsCoordinated,
            greenWaveEnabled = group.m_GreenWaveEnabled,
            memberCount = memberTraces.Count,
            movementMapping = new
            {
                complete = completeMappingCount,
                unavailable = missingMappingCount,
            },
            storedMaster = new
            {
                state = group.m_MasterState.ToString(),
                currentGroup = group.m_MasterPhase,
                nextGroup = group.m_MasterNextPhase,
                timer = group.m_MasterTimer,
                customTimer = group.m_MasterCustomTimer,
                signalGroupCount = group.m_MasterSignalGroupCount,
            },
            members = memberTraces,
        };
    }

    private object GetTrafficGroupLockstepMemberTrace(
        Entity memberEntity,
        Entity groupEntity)
    {
        bool hasMember = EntityManager.TryGetComponent(
            memberEntity,
            out TrafficGroupMember member);
        bool hasGroup = EntityManager.TryGetComponent(
            groupEntity,
            out TrafficGroup group);
        bool hasTrafficLights = EntityManager.TryGetComponent(
            memberEntity,
            out TrafficLights trafficLights);
        CustomTrafficLights customTrafficLights =
            EntityManager.TryGetComponent(
                memberEntity,
                out CustomTrafficLights existingCustomTrafficLights)
                ? existingCustomTrafficLights
                : default;
        bool hasDebugState = EntityManager.TryGetComponent(
            memberEntity,
            out TrafficGroupLockstepDebugState debugState);
        bool hasMapping = EntityManager.TryGetComponent(
            memberEntity,
            out TrafficGroupPhaseMapping phaseMapping);

        int mappedCurrentGroup = 0;
        int mappedNextGroup = 0;
        bool currentMapped = hasGroup
            && hasMapping
            && phaseMapping.m_Map.IsComplete
            && phaseMapping.m_Map.TryMapLeaderToMember(
                group.m_MasterPhase,
                out mappedCurrentGroup);
        bool nextMapped = hasGroup
            && hasMapping
            && phaseMapping.m_Map.IsComplete
            && (group.m_MasterNextPhase == 0
                || phaseMapping.m_Map.TryMapLeaderToMember(
                    group.m_MasterNextPhase,
                    out mappedNextGroup));

        var laneSignals = GetTspLaneSignalTrace(
            memberEntity,
            hasTrafficLights,
            trafficLights,
            hasRuntimeDebug: false,
            runtimeDebug: default);
        ArrayList renderedTrafficLights =
            GetRenderedTrafficLightTrace(memberEntity);

        ulong liveLaneHash = 0;
        ushort liveOutputGroupMask = 0;
        int liveLaneCount = 0;
        if (EntityManager.TryGetBuffer(
                memberEntity,
                isReadOnly: true,
                out DynamicBuffer<SubLane> subLanes))
        {
            liveLaneHash =
                TrafficGroupLockstepRuntimeDiagnostics.HashLaneSignals(
                    subLanes,
                    EntityManager);
            foreach (SubLane subLane in subLanes)
            {
                if (!EntityManager.TryGetComponent(
                        subLane.m_SubLane,
                        out LaneSignal laneSignal))
                {
                    continue;
                }

                liveLaneCount++;
                if (laneSignal.m_Signal != LaneSignalType.None
                    && laneSignal.m_Signal != LaneSignalType.Stop)
                {
                    liveOutputGroupMask |= laneSignal.m_GroupMask;
                }
            }
        }

        ulong liveRenderedHash = 0;
        if (EntityManager.TryGetBuffer(
                memberEntity,
                isReadOnly: true,
                out DynamicBuffer<SubObject> subObjects))
        {
            liveRenderedHash =
                TrafficGroupLockstepRuntimeDiagnostics.HashRenderedLights(
                    subObjects,
                    EntityManager);
        }

        TrafficGroupLockstepControllerSnapshot liveController =
            hasTrafficLights
                ? TrafficGroupLockstepRuntimeDiagnostics.Snapshot(
                    trafficLights,
                    customTrafficLights)
                : default;
        var evidence = new TrafficGroupLockstepEvidence(
            hasDebugState,
            isCoordinated: hasGroup && group.m_IsCoordinated,
            isGreenWave: hasGroup && group.m_GreenWaveEnabled,
            hasDebugState ? debugState.PassFlags : TrafficGroupLockstepPassFlags.None,
            hasDebugState
                ? debugState.SyncDisposition
                : TrafficGroupLockstepSyncDisposition.None,
            hasDebugState ? debugState.Before : default,
            hasDebugState ? debugState.Master : default,
            hasDebugState ? debugState.After : default,
            liveController,
            hasDebugState ? debugState.LaneHashAfter : 0,
            liveLaneHash,
            hasDebugState ? debugState.RenderedHashAfter : 0,
            liveRenderedHash,
            ToGroupBit(hasDebugState
                ? debugState.MappedCurrentGroup
                : (byte)mappedCurrentGroup),
            ToGroupBit(hasDebugState
                ? debugState.MappedNextGroup
                : (byte)mappedNextGroup),
            liveOutputGroupMask);
        TrafficGroupLockstepClassification classification =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);
        if (hasMember && member.m_IsGroupLeader)
        {
            classification = new TrafficGroupLockstepClassification(
                TrafficGroupLockstepVerdict.InsufficientEvidence,
                "The group leader is the synchronization source.");
        }

        if (hasMember && !member.m_IsGroupLeader)
        {
            WarnLockstepVerdictIfChanged(
                memberEntity,
                classification,
                hasDebugState ? debugState : default,
                liveController,
                liveLaneHash,
                liveRenderedHash);
        }

        Entity leaderEntity = hasMember
            ? (member.m_IsGroupLeader ? memberEntity : member.m_LeaderEntity)
            : Entity.Null;
        return new
        {
            entity = FormatEntity(memberEntity),
            available = hasMember && hasTrafficLights,
            role = hasMember
                ? (member.m_IsGroupLeader ? "Leader" : "Follower")
                : "Stale member",
            groupEntity = FormatEntity(groupEntity),
            leaderEntity = FormatEntity(leaderEntity),
            updateFrameIndex = GetUpdateFrameIndex(memberEntity),
            leaderUpdateFrameIndex = GetUpdateFrameIndex(leaderEntity),
            liveController = ControllerTrace(liveController),
            liveLeader = GetTspTrafficGroupLeaderTrace(memberEntity),
            storedMaster = hasGroup
                ? new
                {
                    state = group.m_MasterState.ToString(),
                    currentGroup = group.m_MasterPhase,
                    nextGroup = group.m_MasterNextPhase,
                    timer = group.m_MasterTimer,
                    customTimer = group.m_MasterCustomTimer,
                    signalGroupCount = group.m_MasterSignalGroupCount,
                }
                : null,
            runtimeDebug = hasDebugState
                ? (object)new
                {
                    available = true,
                    simulationFrame = debugState.SimulationFrame,
                    memberUpdateFrame = debugState.MemberUpdateFrame,
                    leaderUpdateFrame = debugState.LeaderUpdateFrame,
                    passFlags = debugState.PassFlags.ToString(),
                    syncDisposition = debugState.SyncDisposition.ToString(),
                    coordinated = debugState.IsCoordinated,
                    greenWave = debugState.IsGreenWave,
                    completeMapping = debugState.HasCompleteMapping,
                    mappedCurrentGroup = debugState.MappedCurrentGroup,
                    mappedNextGroup = debugState.MappedNextGroup,
                    before = ControllerTrace(debugState.Before),
                    mappedMaster = ControllerTrace(debugState.Master),
                    after = ControllerTrace(debugState.After),
                    laneHashBefore = FormatHash(debugState.LaneHashBefore),
                    laneHashAfter = FormatHash(debugState.LaneHashAfter),
                    renderedHashBefore = FormatHash(debugState.RenderedHashBefore),
                    renderedHashAfter = FormatHash(debugState.RenderedHashAfter),
                    laneCount = debugState.LaneCount,
                    renderedCount = debugState.RenderedCount,
                }
                : new { available = false },
            movementMapping = new
            {
                componentPresent = hasMapping,
                complete = hasMapping && phaseMapping.m_Map.IsComplete,
                currentMapped,
                mappedCurrentGroup,
                nextMapped,
                mappedNextGroup,
            },
            liveOutputs = new
            {
                laneCount = liveLaneCount,
                laneHash = FormatHash(liveLaneHash),
                renderedCount = renderedTrafficLights.Count,
                renderedHash = FormatHash(liveRenderedHash),
                activeGroupMask = liveOutputGroupMask,
            },
            verdict = classification.Verdict.ToString(),
            reason = classification.Reason,
            laneSignals,
            renderedTrafficLights,
        };
    }

    private ArrayList GetRenderedTrafficLightTrace(Entity entity)
    {
        var renderedLights = new ArrayList();
        if (!EntityManager.TryGetBuffer(
                entity,
                isReadOnly: true,
                out DynamicBuffer<SubObject> subObjects))
        {
            return renderedLights;
        }

        foreach (SubObject subObject in subObjects)
        {
            if (!EntityManager.TryGetComponent(
                    subObject.m_SubObject,
                    out RenderedTrafficLight rendered))
            {
                continue;
            }

            renderedLights.Add(new
            {
                entity = FormatEntity(subObject.m_SubObject),
                groupMask0 = rendered.m_GroupMask0,
                groupMask1 = rendered.m_GroupMask1,
                state = rendered.m_State.ToString(),
            });
        }

        return renderedLights;
    }

    private void WarnLockstepVerdictIfChanged(
        Entity memberEntity,
        TrafficGroupLockstepClassification classification,
        TrafficGroupLockstepDebugState debugState,
        TrafficGroupLockstepControllerSnapshot live,
        ulong liveLaneHash,
        ulong liveRenderedHash)
    {
        if (classification.Verdict == TrafficGroupLockstepVerdict.InSync
            || classification.Verdict == TrafficGroupLockstepVerdict.GreenWaveExcluded
            || classification.Verdict == TrafficGroupLockstepVerdict.InsufficientEvidence)
        {
            m_LockstepVerdictWarnings.Remove(memberEntity);
            return;
        }

        string signature =
            $"{classification.Verdict}|{classification.Reason}"
            + $"|{debugState.SimulationFrame}|{debugState.PassFlags}"
            + $"|{FormatController(debugState.Before)}"
            + $"|{FormatController(debugState.After)}"
            + $"|{FormatController(live)}"
            + $"|{debugState.LaneHashAfter:X16}|{liveLaneHash:X16}"
            + $"|{debugState.RenderedHashAfter:X16}|{liveRenderedHash:X16}";
        if (m_LockstepVerdictWarnings.TryGetValue(
                memberEntity,
                out string previousSignature)
            && previousSignature == signature)
        {
            return;
        }

        m_LockstepVerdictWarnings[memberEntity] = signature;
        Mod.log.Warn(
            $"[TLE][TrafficGroupLockstep] member={FormatEntity(memberEntity)} "
            + $"verdict={classification.Verdict} "
            + $"reason={classification.Reason} "
            + $"simulationFrame={debugState.SimulationFrame} "
            + $"passes={debugState.PassFlags} "
            + $"sync={debugState.SyncDisposition}");
    }

    private static object ControllerTrace(
        TrafficGroupLockstepControllerSnapshot snapshot)
    {
        return new
        {
            state = snapshot.State,
            currentGroup = snapshot.CurrentGroup,
            nextGroup = snapshot.NextGroup,
            timer = snapshot.Timer,
            customTimer = snapshot.CustomTimer,
            signalGroupCount = snapshot.SignalGroupCount,
        };
    }

    private static string FormatController(
        TrafficGroupLockstepControllerSnapshot snapshot)
    {
        return $"{snapshot.State}:{snapshot.CurrentGroup}:{snapshot.NextGroup}:"
            + $"{snapshot.Timer}:{snapshot.CustomTimer}:{snapshot.SignalGroupCount}";
    }

    private static string FormatHash(ulong value)
    {
        return value.ToString("X16");
    }
}
