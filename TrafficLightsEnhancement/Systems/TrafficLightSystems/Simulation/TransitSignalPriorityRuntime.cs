using C2VM.TrafficLightsEnhancement.Components;
using Game.Net;
using TrafficLightsEnhancement.Logic.Tsp;
using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Systems.TrafficLightSystems.Simulation;

public static class TransitSignalPriorityRuntime
{
    public static bool TryResolveActiveLocalRequest(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        Entity junctionEntity,
        DynamicBuffer<SubLane> subLanes,
        TrafficLights trafficLights,
        out TransitSignalPriorityRequest request,
        out Components.TransitSignalPrioritySettings settings)
    {
        request = default;
        settings = default;

        if (!job.m_ExtraTypeHandle.m_TransitSignalPrioritySettingsLookup.TryGetComponent(junctionEntity, out settings) || !settings.m_Enabled)
        {
            return false;
        }

        var availability = TspPolicy.GetAvailability(
            new global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings
            {
                m_Enabled = settings.m_Enabled,
                m_AllowTrackRequests = settings.m_AllowTrackRequests,
                m_AllowPublicCarRequests = settings.m_AllowPublicCarRequests,
                m_AllowGroupPropagation = settings.m_AllowGroupPropagation,
                m_RequestHorizonTicks = settings.m_RequestHorizonTicks,
                m_MaxGreenExtensionTicks = settings.m_MaxGreenExtensionTicks,
            },
            isGroupedIntersection: false);

        if (!availability.IsRuntimeEligible)
        {
            return false;
        }

        TransitSignalPriorityRequest freshRequest = default;
        bool hasFreshRequest = TryBuildFreshRequest(job, subLanes, trafficLights, settings, out freshRequest);

        TransitSignalPriorityRequest priorRequest = default;
        bool hasExistingRequest = job.m_ExtraTypeHandle.m_TransitSignalPriorityRequest.TryGetComponent(junctionEntity, out priorRequest)
            && priorRequest.m_TargetSignalGroup > 0
            && priorRequest.m_Strength > 0f;

        TspSignalRequest? freshSignalRequest = hasFreshRequest ? ToSignalRequest(freshRequest) : null;
        TspSignalRequest? existingRequest = hasExistingRequest ? ToSignalRequest(priorRequest) : null;

        if (!TspPreemptionPolicy.TryRefreshOrLatchRequest(
                freshSignalRequest,
                existingRequest,
                settings.m_RequestHorizonTicks,
                trafficLights.m_CurrentSignalGroup,
                out var activeRequest))
        {
            return false;
        }

        request = FromSignalRequest(activeRequest);
        return true;
    }

    public static bool ShouldHoldCurrentGroup(
        TrafficLights trafficLights,
        TransitSignalPriorityRequest request,
        ushort maxGreenExtensionTicks)
    {
        return TspPreemptionPolicy.ShouldHoldCurrentGroup(
            trafficLights.m_CurrentSignalGroup,
            ToSignalRequest(request),
            trafficLights.m_Timer,
            maxGreenExtensionTicks);
    }

    public static int GetMinimumGreenDurationTicks(
        int defaultMinimumGreenTicks,
        TrafficLights trafficLights,
        TransitSignalPriorityRequest request)
    {
        return TspPreemptionPolicy.GetMinimumGreenDurationTicks(
            defaultMinimumGreenTicks,
            trafficLights.m_CurrentSignalGroup,
            ToSignalRequest(request));
    }

    private static bool TryBuildFreshRequest(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        DynamicBuffer<SubLane> subLanes,
        TrafficLights trafficLights,
        Components.TransitSignalPrioritySettings settings,
        out TransitSignalPriorityRequest request)
    {
        request = default;

        var logicSettings = new global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings
        {
            m_Enabled = settings.m_Enabled,
            m_AllowTrackRequests = settings.m_AllowTrackRequests,
            m_AllowPublicCarRequests = settings.m_AllowPublicCarRequests,
            m_AllowGroupPropagation = settings.m_AllowGroupPropagation,
            m_RequestHorizonTicks = settings.m_RequestHorizonTicks,
            m_MaxGreenExtensionTicks = settings.m_MaxGreenExtensionTicks,
        };

        foreach (var subLane in subLanes)
        {
            Entity subLaneEntity = subLane.m_SubLane;
            if (!job.m_LaneSignalData.TryGetComponent(subLaneEntity, out var laneSignal))
            {
                continue;
            }

            if (laneSignal.m_Petitioner == Entity.Null)
            {
                continue;
            }

            bool isTrackLane = job.m_ExtraTypeHandle.m_TrackLane.HasComponent(subLaneEntity);
            bool isPublicCarLane = IsPublicOnlyCarLane(job, subLaneEntity);

            if (!global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPriorityRuntime.TryBuildRequestForLane(
                    logicSettings,
                    isTrackLane,
                    isPublicCarLane,
                    out var logicRequest))
            {
                continue;
            }

            byte currentSignalGroup = trafficLights.m_CurrentSignalGroup;
            byte targetSignalGroup = GetTargetSignalGroup(laneSignal.m_GroupMask, currentSignalGroup);
            if (targetSignalGroup == 0)
            {
                continue;
            }

            request = new TransitSignalPriorityRequest
            {
                m_TargetSignalGroup = targetSignalGroup,
                m_SourceType = (byte)logicRequest.Source,
                m_Strength = logicRequest.Strength,
                m_ExpiryTimer = settings.m_RequestHorizonTicks,
                m_ExtendCurrentPhase = logicRequest.ExtensionEligible
                    && currentSignalGroup == targetSignalGroup
                    && (laneSignal.m_Flags & LaneSignalFlags.CanExtend) != 0,
            };
            return true;
        }

        return false;
    }

    private static TspSignalRequest ToSignalRequest(TransitSignalPriorityRequest request)
    {
        return new TspSignalRequest(
            request.m_TargetSignalGroup,
            (TspSource)request.m_SourceType,
            request.m_Strength,
            request.m_ExpiryTimer,
            request.m_ExtendCurrentPhase);
    }

    private static TransitSignalPriorityRequest FromSignalRequest(TspSignalRequest request)
    {
        return new TransitSignalPriorityRequest
        {
            m_TargetSignalGroup = (byte)request.TargetSignalGroup,
            m_SourceType = (byte)request.Source,
            m_Strength = request.Strength,
            m_ExpiryTimer = request.ExpiryTimer,
            m_ExtendCurrentPhase = request.ExtendCurrentPhase,
        };
    }

    public static bool TryGetGroupedPropagatedRequest(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        Entity junctionEntity,
        TrafficLights trafficLights,
        out TransitSignalPriorityRequest request)
    {
        request = default;

        if (!job.m_ExtraTypeHandle.m_GroupedTransitSignalPriorityRequest.TryGetComponent(junctionEntity, out var groupedRequest))
        {
            return false;
        }

        if (groupedRequest.m_TargetSignalGroup == 0 || groupedRequest.m_Strength <= 0f)
        {
            return false;
        }

        request = new TransitSignalPriorityRequest
        {
            m_TargetSignalGroup = groupedRequest.m_TargetSignalGroup,
            m_SourceType = groupedRequest.m_SourceType,
            m_Strength = groupedRequest.m_Strength,
            m_ExpiryTimer = groupedRequest.m_ExpiryTimer,
            m_ExtendCurrentPhase = groupedRequest.m_ExtendCurrentPhase
                && trafficLights.m_CurrentSignalGroup > 0
                && trafficLights.m_CurrentSignalGroup == groupedRequest.m_TargetSignalGroup,
        };
        return true;
    }

    public static TransitSignalPriorityRequest SelectPreferredRequest(
        TransitSignalPriorityRequest activeRequest,
        TransitSignalPriorityRequest candidateRequest,
        bool preferActiveOnTie)
    {
        if (candidateRequest.m_Strength > activeRequest.m_Strength)
        {
            return candidateRequest;
        }

        return preferActiveOnTie ? activeRequest : candidateRequest;
    }

    private static bool IsPublicOnlyCarLane(PatchedTrafficLightSystem.UpdateTrafficLightsJob job, Entity subLaneEntity)
    {
        if (!job.m_ExtraTypeHandle.m_CarLane.TryGetComponent(subLaneEntity, out var carLane))
        {
            return false;
        }

        if ((carLane.m_Flags & CarLaneFlags.PublicOnly) != 0)
        {
            return true;
        }

        if (!job.m_ExtraTypeHandle.m_ExtraLaneSignal.TryGetComponent(subLaneEntity, out var extraLaneSignal))
        {
            return false;
        }

        return extraLaneSignal.m_SourceSubLane != Entity.Null
            && job.m_ExtraTypeHandle.m_CarLane.TryGetComponent(extraLaneSignal.m_SourceSubLane, out var sourceCarLane)
            && (sourceCarLane.m_Flags & CarLaneFlags.PublicOnly) != 0;
    }

    private static byte GetTargetSignalGroup(ushort groupMask, byte currentSignalGroup)
    {
        if (currentSignalGroup > 0 && (groupMask & (1 << (currentSignalGroup - 1))) != 0)
        {
            return currentSignalGroup;
        }

        for (byte i = 1; i <= 16; i++)
        {
            if ((groupMask & (1 << (i - 1))) != 0)
            {
                return i;
            }
        }

        return 0;
    }
}
