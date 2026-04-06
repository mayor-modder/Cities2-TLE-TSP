using C2VM.TrafficLightsEnhancement.Components;
using Colossal.Logging;
using Game;
using Game.Net;
using TrafficLightsEnhancement.Logic.Tsp;
using Unity.Collections;
using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Systems;

public partial class TransitSignalPriorityDiagnosticsSystem : GameSystemBase
{
    private static readonly ILog m_Log = Mod.m_Log;

    private EntityQuery m_JunctionQuery;
    private EntityQuery m_DecisionTraceQuery;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_JunctionQuery = GetEntityQuery(ComponentType.ReadOnly<TrafficLights>());
        m_DecisionTraceQuery = GetEntityQuery(ComponentType.ReadOnly<TransitSignalPriorityDecisionTrace>());
    }

    protected override void OnUpdate()
    {
        LogJunctionRequestChanges();
        LogGroupedPropagationChanges();
        LogDecisionTraces();
    }

    private void LogJunctionRequestChanges()
    {
        var entities = m_JunctionQuery.ToEntityArray(Allocator.Temp);

        foreach (Entity entity in entities)
        {
            bool hasDebugState = EntityManager.HasComponent<TransitSignalPriorityDebugState>(entity);
            bool hasRequest = EntityManager.HasComponent<TransitSignalPriorityRequest>(entity);
            if (!hasDebugState && !hasRequest)
            {
                continue;
            }

            TransitSignalPriorityDebugState previousState = hasDebugState
                ? EntityManager.GetComponentData<TransitSignalPriorityDebugState>(entity)
                : default;

            if (!hasRequest)
            {
                if (previousState.m_HadRequest)
                {
                    m_Log.Info($"[TSP] Junction {FormatEntity(entity)} request cleared");
                }

                if (hasDebugState)
                {
                    EntityManager.RemoveComponent<TransitSignalPriorityDebugState>(entity);
                }

                continue;
            }

            TransitSignalPriorityRequest request = EntityManager.GetComponentData<TransitSignalPriorityRequest>(entity);
            TransitSignalPriorityRuntimeDebugInfo runtimeDebugInfo = EntityManager.HasComponent<TransitSignalPriorityRuntimeDebugInfo>(entity)
                ? EntityManager.GetComponentData<TransitSignalPriorityRuntimeDebugInfo>(entity)
                : default;
            bool requestChanged =
                !previousState.m_HadRequest ||
                previousState.m_TargetSignalGroup != request.m_TargetSignalGroup ||
                previousState.m_SourceType != request.m_SourceType ||
                previousState.m_Strength != request.m_Strength ||
                previousState.m_ExtendCurrentPhase != request.m_ExtendCurrentPhase ||
                previousState.m_RequestKind != runtimeDebugInfo.m_RequestKind ||
                previousState.m_ApproachLaneRole != runtimeDebugInfo.m_ApproachLaneRole ||
                previousState.m_HasEarlyCandidate != runtimeDebugInfo.m_HasEarlyCandidate ||
                previousState.m_HasPetitionerCandidate != runtimeDebugInfo.m_HasPetitionerCandidate ||
                previousState.m_HadExistingRequest != runtimeDebugInfo.m_HadExistingRequest ||
                previousState.m_TrackSignaledLaneProbe != runtimeDebugInfo.m_TrackSignaledLaneProbe ||
                previousState.m_TrackApproachLaneProbe != runtimeDebugInfo.m_TrackApproachLaneProbe ||
                previousState.m_TrackUpstreamLaneProbe != runtimeDebugInfo.m_TrackUpstreamLaneProbe;

            if (requestChanged)
            {
                string action = previousState.m_HadRequest ? "updated" : "started";
                m_Log.Info(
                    $"[TSP] Junction {FormatEntity(entity)} request {action}: source={FormatSource(request.m_SourceType)} target={request.m_TargetSignalGroup} extend={request.m_ExtendCurrentPhase} strength={request.m_Strength:0.##} origin={FormatRequestKind(runtimeDebugInfo.m_RequestKind)} laneRole={FormatApproachLaneRole(runtimeDebugInfo.m_ApproachLaneRole)} earlyCandidate={runtimeDebugInfo.m_HasEarlyCandidate} petitionerCandidate={runtimeDebugInfo.m_HasPetitionerCandidate} hadExisting={runtimeDebugInfo.m_HadExistingRequest} trackSignalProbe={FormatTrackProbe(runtimeDebugInfo.m_TrackSignaledLaneProbe)} trackApproachProbe={FormatTrackProbe(runtimeDebugInfo.m_TrackApproachLaneProbe)} trackUpstreamProbe={FormatTrackProbe(runtimeDebugInfo.m_TrackUpstreamLaneProbe)} expiry={runtimeDebugInfo.m_ExpiryTimer}");
            }

            var nextState = new TransitSignalPriorityDebugState
            {
                m_HadRequest = true,
                m_TargetSignalGroup = request.m_TargetSignalGroup,
                m_SourceType = request.m_SourceType,
                m_Strength = request.m_Strength,
                m_ExtendCurrentPhase = request.m_ExtendCurrentPhase,
                m_RequestKind = runtimeDebugInfo.m_RequestKind,
                m_ApproachLaneRole = runtimeDebugInfo.m_ApproachLaneRole,
                m_HasEarlyCandidate = runtimeDebugInfo.m_HasEarlyCandidate,
                m_HasPetitionerCandidate = runtimeDebugInfo.m_HasPetitionerCandidate,
                m_HadExistingRequest = runtimeDebugInfo.m_HadExistingRequest,
                m_TrackSignaledLaneProbe = runtimeDebugInfo.m_TrackSignaledLaneProbe,
                m_TrackApproachLaneProbe = runtimeDebugInfo.m_TrackApproachLaneProbe,
                m_TrackUpstreamLaneProbe = runtimeDebugInfo.m_TrackUpstreamLaneProbe,
            };

            if (hasDebugState)
            {
                EntityManager.SetComponentData(entity, nextState);
            }
            else
            {
                EntityManager.AddComponentData(entity, nextState);
            }
        }

        entities.Dispose();
    }

    private void LogGroupedPropagationChanges()
    {
        var entities = m_JunctionQuery.ToEntityArray(Allocator.Temp);

        foreach (Entity entity in entities)
        {
            bool hasDebugState = EntityManager.HasComponent<TrafficGroupTspDebugState>(entity);
            bool hasPropagationRequest = EntityManager.HasComponent<GroupedTransitSignalPriorityRequest>(entity);
            if (!hasDebugState && !hasPropagationRequest)
            {
                continue;
            }

            TrafficGroupTspDebugState previousState = hasDebugState
                ? EntityManager.GetComponentData<TrafficGroupTspDebugState>(entity)
                : default;

            if (!hasPropagationRequest)
            {
                if (previousState.m_HadRequest)
                {
                    m_Log.Info($"[TSP] Junction {FormatEntity(entity)} grouped propagation cleared");
                }

                if (hasDebugState)
                {
                    EntityManager.RemoveComponent<TrafficGroupTspDebugState>(entity);
                }

                continue;
            }

            GroupedTransitSignalPriorityRequest groupedRequest = EntityManager.GetComponentData<GroupedTransitSignalPriorityRequest>(entity);
            bool stateChanged =
                !previousState.m_HadRequest ||
                previousState.m_TargetSignalGroup != groupedRequest.m_TargetSignalGroup ||
                previousState.m_SourceType != groupedRequest.m_SourceType ||
                previousState.m_Strength != groupedRequest.m_Strength ||
                previousState.m_ExtendCurrentPhase != groupedRequest.m_ExtendCurrentPhase;

            if (stateChanged)
            {
                string action = previousState.m_HadRequest ? "updated" : "started";
                m_Log.Info(
                    $"[TSP] Junction {FormatEntity(entity)} grouped propagation {action}: source={FormatSource(groupedRequest.m_SourceType)} target={groupedRequest.m_TargetSignalGroup} extend={groupedRequest.m_ExtendCurrentPhase} strength={groupedRequest.m_Strength:0.##} origin={FormatEntity(groupedRequest.m_OriginEntity)} group={FormatEntity(groupedRequest.m_GroupEntity)}");
            }

            var nextState = new TrafficGroupTspDebugState
            {
                m_HadRequest = true,
                m_TargetSignalGroup = groupedRequest.m_TargetSignalGroup,
                m_SourceType = groupedRequest.m_SourceType,
                m_Strength = groupedRequest.m_Strength,
                m_ExtendCurrentPhase = groupedRequest.m_ExtendCurrentPhase,
            };

            if (hasDebugState)
            {
                EntityManager.SetComponentData(entity, nextState);
            }
            else
            {
                EntityManager.AddComponentData(entity, nextState);
            }
        }

        entities.Dispose();
    }

    private void LogDecisionTraces()
    {
        var entities = m_DecisionTraceQuery.ToEntityArray(Allocator.Temp);

        foreach (Entity entity in entities)
        {
            TransitSignalPriorityDecisionTrace trace = EntityManager.GetComponentData<TransitSignalPriorityDecisionTrace>(entity);
            string requestScope = FormatRequestOrigin(trace.m_RequestOrigin);
            m_Log.Info(
                $"[TSP] Junction {FormatEntity(entity)} applied {FormatReason(trace.m_Reason)} from {requestScope} request: source={FormatSource(trace.m_SourceType)} target={trace.m_RequestTargetSignalGroup} base={trace.m_BaseSignalGroup} selected={trace.m_SelectedSignalGroup}");
            EntityManager.RemoveComponent<TransitSignalPriorityDecisionTrace>(entity);
        }

        entities.Dispose();
    }

    private static string FormatEntity(Entity entity)
    {
        return $"{entity.Index}:{entity.Version}";
    }

    private static string FormatReason(byte reason)
    {
        return ((TspSelectionReason)reason) switch
        {
            TspSelectionReason.ExtendedCurrentPhase => "current-phase extension",
            TspSelectionReason.SelectedTargetPhase => "target-phase selection",
            _ => "no override",
        };
    }

    private static string FormatSource(byte sourceType)
    {
        return ((TspSource)sourceType) switch
        {
            TspSource.Track => "track",
            TspSource.PublicCar => "public-car",
            _ => "none",
        };
    }

    private static string FormatRequestOrigin(byte requestOrigin)
    {
        return (TransitSignalPriorityRequestOrigin)requestOrigin switch
        {
            TransitSignalPriorityRequestOrigin.GroupedPropagation => "grouped-propagation",
            _ => "local",
        };
    }

    private static string FormatRequestKind(byte requestKind)
    {
        return (TransitSignalPriorityRequestKind)requestKind switch
        {
            TransitSignalPriorityRequestKind.FreshEarly => "fresh-early",
            TransitSignalPriorityRequestKind.FreshPetitioner => "fresh-petitioner",
            TransitSignalPriorityRequestKind.LatchedExisting => "latched-existing",
            TransitSignalPriorityRequestKind.GroupedPropagation => "grouped-propagation",
            _ => "unknown",
        };
    }

    private static string FormatApproachLaneRole(byte laneRole)
    {
        return (TransitSignalPriorityApproachLaneRole)laneRole switch
        {
            TransitSignalPriorityApproachLaneRole.ApproachLane => "approach",
            TransitSignalPriorityApproachLaneRole.UpstreamLane => "upstream",
            _ => "none",
        };
    }

    private static string FormatTrackProbe(byte probe)
    {
        return (TransitSignalPriorityTrackProbeResult)probe switch
        {
            TransitSignalPriorityTrackProbeResult.NoTramSamples => "no-tram-samples",
            TransitSignalPriorityTrackProbeResult.BelowThreshold => "below-threshold",
            TransitSignalPriorityTrackProbeResult.MatchOnApproachLane => "match-approach",
            TransitSignalPriorityTrackProbeResult.MatchOnUpstreamLane => "match-upstream",
            _ => "none",
        };
    }

}
