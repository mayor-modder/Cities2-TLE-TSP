using C2VM.TrafficLightsEnhancement.Components;
using Game.Net;
using Game.Prefabs;
using Game.Vehicles;
using TrafficLightsEnhancement.Logic.Tsp;
using Unity.Collections;
using Unity.Entities;
using NetCarLaneFlags = Game.Net.CarLaneFlags;
using NetSubLane = Game.Net.SubLane;
using VehicleCarLaneFlags = Game.Vehicles.CarLaneFlags;

namespace C2VM.TrafficLightsEnhancement.Systems.TrafficLightSystems.Simulation;

public static class TransitSignalPriorityRuntime
{
    private struct FreshRequestDebugInfo
    {
        public TransitSignalPriorityRequestKind RequestKind;
        public TransitSignalPriorityApproachLaneRole ApproachLaneRole;
        public bool HasEarlyCandidate;
        public bool HasPetitionerCandidate;
        public TransitSignalPriorityTrackProbeResult TrackSignaledLaneProbe;
        public TransitSignalPriorityTrackProbeResult TrackApproachLaneProbe;
        public TransitSignalPriorityTrackProbeResult TrackUpstreamLaneProbe;
    }

    private struct TransitApproachCandidate
    {
        public TspRequest Request;
        public LaneSignal LaneSignal;
        public byte TargetSignalGroup;
        public TransitSignalPriorityApproachLaneRole ApproachLaneRole;
        public TransitSignalPriorityTrackProbeResult TrackSignaledLaneProbe;
        public TransitSignalPriorityTrackProbeResult TrackApproachLaneProbe;
        public TransitSignalPriorityTrackProbeResult TrackUpstreamLaneProbe;
    }

    private readonly struct TrackProbeSnapshot
    {
        public TrackProbeSnapshot(bool hasSample, float curvePosition, TransitSignalPriorityTrackProbeResult result)
        {
            HasSample = hasSample;
            CurvePosition = curvePosition;
            Result = result;
        }

        public bool HasSample { get; }

        public float CurvePosition { get; }

        public TransitSignalPriorityTrackProbeResult Result { get; }
    }

    private const float TramApproachLaneCurveThreshold = 0.2f;
    private const float TramUpstreamLaneCurveThreshold = 0.9f;
    private const float BusApproachCurveThreshold = 0.35f;
    private const VehicleCarLaneFlags BusStoppedLaneFlags =
        VehicleCarLaneFlags.IsBlocked
        | VehicleCarLaneFlags.EndReached;

    public static bool TryResolveActiveLocalRequest(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        Entity junctionEntity,
        DynamicBuffer<NetSubLane> subLanes,
        TrafficLights trafficLights,
        out TransitSignalPriorityRequest request,
        out Components.TransitSignalPrioritySettings settings,
        out TransitSignalPriorityRuntimeDebugInfo debugInfo)
    {
        request = default;
        settings = default;
        debugInfo = default;

        if (!job.m_ExtraTypeHandle.m_TransitSignalPrioritySettingsLookup.TryGetComponent(junctionEntity, out settings) || !settings.m_Enabled)
        {
            return false;
        }

        ushort effectiveRequestHorizonTicks = TspPolicy.GetEffectiveRequestHorizonTicks(settings.m_RequestHorizonTicks);

        var availability = TspPolicy.GetAvailability(
            new global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings
            {
                m_Enabled = settings.m_Enabled,
                m_AllowTrackRequests = settings.m_AllowTrackRequests,
                m_AllowPublicCarRequests = settings.m_AllowPublicCarRequests,
                m_AllowGroupPropagation = settings.m_AllowGroupPropagation,
                m_RequestHorizonTicks = effectiveRequestHorizonTicks,
                m_MaxGreenExtensionTicks = settings.m_MaxGreenExtensionTicks,
            },
            isGroupedIntersection: false);

        if (!availability.IsRuntimeEligible)
        {
            return false;
        }

        TransitSignalPriorityRequest freshRequest = default;
        FreshRequestDebugInfo freshDebugInfo = default;
        bool hasFreshRequest = TryBuildFreshRequest(job, subLanes, trafficLights, settings, effectiveRequestHorizonTicks, out freshRequest, out freshDebugInfo);

        TransitSignalPriorityRequest priorRequest = default;
        bool hasExistingRequest = job.m_ExtraTypeHandle.m_TransitSignalPriorityRequest.TryGetComponent(junctionEntity, out priorRequest)
            && priorRequest.m_TargetSignalGroup > 0
            && priorRequest.m_Strength > 0f;

        TspSignalRequest? freshSignalRequest = hasFreshRequest ? ToSignalRequest(freshRequest) : null;
        TspSignalRequest? existingRequest = hasExistingRequest ? ToSignalRequest(priorRequest) : null;

        if (!TspPreemptionPolicy.TryRefreshOrLatchRequest(
                freshSignalRequest,
                existingRequest,
                effectiveRequestHorizonTicks,
                trafficLights.m_CurrentSignalGroup,
                out var activeRequest))
        {
            return false;
        }

        request = FromSignalRequest(activeRequest);
        debugInfo = new TransitSignalPriorityRuntimeDebugInfo
        {
            m_RequestKind = (byte)(hasFreshRequest ? freshDebugInfo.RequestKind : TransitSignalPriorityRequestKind.LatchedExisting),
            m_ApproachLaneRole = (byte)(hasFreshRequest ? freshDebugInfo.ApproachLaneRole : TransitSignalPriorityApproachLaneRole.None),
            m_SourceType = request.m_SourceType,
            m_TargetSignalGroup = request.m_TargetSignalGroup,
            m_Strength = request.m_Strength,
            m_ExpiryTimer = request.m_ExpiryTimer,
            m_ExtendCurrentPhase = request.m_ExtendCurrentPhase,
            m_HasEarlyCandidate = freshDebugInfo.HasEarlyCandidate,
            m_HasPetitionerCandidate = freshDebugInfo.HasPetitionerCandidate,
            m_HadExistingRequest = hasExistingRequest,
            m_TrackSignaledLaneProbe = (byte)freshDebugInfo.TrackSignaledLaneProbe,
            m_TrackApproachLaneProbe = (byte)freshDebugInfo.TrackApproachLaneProbe,
            m_TrackUpstreamLaneProbe = (byte)freshDebugInfo.TrackUpstreamLaneProbe,
        };
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
        DynamicBuffer<NetSubLane> subLanes,
        TrafficLights trafficLights,
        Components.TransitSignalPrioritySettings settings,
        ushort effectiveRequestHorizonTicks,
        out TransitSignalPriorityRequest request,
        out FreshRequestDebugInfo debugInfo)
    {
        request = default;
        debugInfo = default;

        var logicSettings = new global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings
        {
            m_Enabled = settings.m_Enabled,
            m_AllowTrackRequests = settings.m_AllowTrackRequests,
            m_AllowPublicCarRequests = settings.m_AllowPublicCarRequests,
            m_AllowGroupPropagation = settings.m_AllowGroupPropagation,
            m_RequestHorizonTicks = effectiveRequestHorizonTicks,
            m_MaxGreenExtensionTicks = settings.m_MaxGreenExtensionTicks,
        };

        TransitApproachScanState scanState = default;
        TransitApproachCandidate? earlyCandidate = null;
        TransitApproachCandidate? petitionerCandidate = null;

        foreach (var subLane in subLanes)
        {
            Entity subLaneEntity = subLane.m_SubLane;
            if (!job.m_LaneSignalData.TryGetComponent(subLaneEntity, out var laneSignal))
            {
                continue;
            }

            Entity approachLaneEntity = ResolveApproachLane(job, subLaneEntity);
            bool isTramTrackLane = IsTramTrackLane(job, approachLaneEntity);
            bool isPublicCarLane = IsPublicOnlyCarLane(job, approachLaneEntity);

            if (!global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPriorityRuntime.TryBuildRequestForLane(
                    logicSettings,
                    isTramTrackLane,
                    isPublicCarLane,
                    out var logicRequest))
            {
                continue;
            }

            TspRequest? earlyRequest = null;
            if (TryBuildEarlyApproachRequestForLane(
                    job,
                    subLaneEntity,
                    approachLaneEntity,
                    isTramTrackLane,
                    isPublicCarLane,
                    logicRequest,
                    out var detectedEarlyRequest,
                    out var detectedLaneRole,
                    out var trackSignaledLaneProbe,
                    out var trackApproachLaneProbe,
                    out var trackUpstreamLaneProbe))
            {
                earlyRequest = detectedEarlyRequest;
            }

            TspRequest? petitionerRequest = null;
            if (TryBuildPetitionerRequestForLane(laneSignal, logicRequest, out var detectedPetitionerRequest))
            {
                petitionerRequest = detectedPetitionerRequest;
            }

            if (!earlyRequest.HasValue && !petitionerRequest.HasValue)
            {
                continue;
            }

            byte currentSignalGroup = trafficLights.m_CurrentSignalGroup;
            byte targetSignalGroup = GetTargetSignalGroup(laneSignal.m_GroupMask, currentSignalGroup);
            if (targetSignalGroup == 0)
            {
                continue;
            }

            scanState = EarlyApproachDetection.RecordLaneRequests(scanState, earlyRequest, petitionerRequest);

            if (earlyRequest.HasValue && !earlyCandidate.HasValue)
            {
                earlyCandidate = new TransitApproachCandidate
                {
                    Request = earlyRequest.Value,
                    LaneSignal = laneSignal,
                    TargetSignalGroup = targetSignalGroup,
                    ApproachLaneRole = detectedLaneRole,
                    TrackSignaledLaneProbe = trackSignaledLaneProbe,
                    TrackApproachLaneProbe = trackApproachLaneProbe,
                    TrackUpstreamLaneProbe = trackUpstreamLaneProbe,
                };
            }

            if (petitionerRequest.HasValue && !petitionerCandidate.HasValue)
            {
                petitionerCandidate = new TransitApproachCandidate
                {
                    Request = petitionerRequest.Value,
                    LaneSignal = laneSignal,
                    TargetSignalGroup = targetSignalGroup,
                };
            }
        }

        TspRequest? selectedRequest = EarlyApproachDetection.PreferEarlyRequest(
            scanState.EarlyRequest,
            scanState.PetitionerRequest);

        if (!selectedRequest.HasValue)
        {
            return false;
        }

        TransitApproachCandidate? selectedCandidate = scanState.EarlyRequest.HasValue
            ? earlyCandidate
            : petitionerCandidate;

        if (!selectedCandidate.HasValue)
        {
            return false;
        }

        request = CreateRequest(
            selectedCandidate.Value.Request,
            effectiveRequestHorizonTicks,
            trafficLights.m_CurrentSignalGroup,
            selectedCandidate.Value.TargetSignalGroup,
            selectedCandidate.Value.LaneSignal);

        debugInfo = new FreshRequestDebugInfo
        {
            RequestKind = scanState.EarlyRequest.HasValue
                ? TransitSignalPriorityRequestKind.FreshEarly
                : TransitSignalPriorityRequestKind.FreshPetitioner,
            ApproachLaneRole = scanState.EarlyRequest.HasValue
                ? selectedCandidate.Value.ApproachLaneRole
                : TransitSignalPriorityApproachLaneRole.None,
            HasEarlyCandidate = scanState.EarlyRequest.HasValue,
            HasPetitionerCandidate = scanState.PetitionerRequest.HasValue,
            TrackSignaledLaneProbe = scanState.EarlyRequest.HasValue
                ? selectedCandidate.Value.TrackSignaledLaneProbe
                : TransitSignalPriorityTrackProbeResult.None,
            TrackApproachLaneProbe = scanState.EarlyRequest.HasValue
                ? selectedCandidate.Value.TrackApproachLaneProbe
                : TransitSignalPriorityTrackProbeResult.None,
            TrackUpstreamLaneProbe = scanState.EarlyRequest.HasValue
                ? selectedCandidate.Value.TrackUpstreamLaneProbe
                : TransitSignalPriorityTrackProbeResult.None,
        };
        return true;
    }

    private static Entity ResolveApproachLane(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        Entity subLaneEntity)
    {
        Entity sourceSubLane = Entity.Null;
        if (job.m_ExtraTypeHandle.m_ExtraLaneSignal.TryGetComponent(subLaneEntity, out var extraLaneSignal))
        {
            sourceSubLane = extraLaneSignal.m_SourceSubLane;
        }

        return EarlyApproachDetection.ResolveApproachLane(subLaneEntity, sourceSubLane, Entity.Null);
    }

    private static bool TryBuildEarlyApproachRequestForLane(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        Entity signaledLaneEntity,
        Entity approachLaneEntity,
        bool isTramTrackLane,
        bool isPublicCarLane,
        TspRequest laneRequest,
        out TspRequest request,
        out TransitSignalPriorityApproachLaneRole laneRole,
        out TransitSignalPriorityTrackProbeResult trackSignaledLaneProbe,
        out TransitSignalPriorityTrackProbeResult trackApproachLaneProbe,
        out TransitSignalPriorityTrackProbeResult trackUpstreamLaneProbe)
    {
        request = default;
        laneRole = TransitSignalPriorityApproachLaneRole.None;
        trackSignaledLaneProbe = TransitSignalPriorityTrackProbeResult.None;
        trackApproachLaneProbe = TransitSignalPriorityTrackProbeResult.None;
        trackUpstreamLaneProbe = TransitSignalPriorityTrackProbeResult.None;

        if (isTramTrackLane)
        {
            return TryBuildEarlyApproachRequestForTrackLane(
                job,
                signaledLaneEntity,
                approachLaneEntity,
                laneRequest,
                out request,
                out laneRole,
                out trackSignaledLaneProbe,
                out trackApproachLaneProbe,
                out trackUpstreamLaneProbe);
        }

        if (!job.m_LaneObjects.TryGetBuffer(approachLaneEntity, out var laneObjects) || laneObjects.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < laneObjects.Length; i++)
        {
            if (TryGetMovingTransitApproachRequest(
                    job,
                    approachLaneEntity,
                    laneObjects[i],
                    isPublicCarLane,
                    laneRequest,
                    out request))
            {
                laneRole = TransitSignalPriorityApproachLaneRole.ApproachLane;
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildPetitionerRequestForLane(
        LaneSignal laneSignal,
        TspRequest laneRequest,
        out TspRequest request)
    {
        request = default;

        if (laneSignal.m_Petitioner == Entity.Null)
        {
            return false;
        }

        request = laneRequest;
        return true;
    }

    private static bool TryGetMovingTransitApproachRequest(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        Entity scanLaneEntity,
        LaneObject laneObject,
        bool isPublicCarLane,
        TspRequest laneRequest,
        out TspRequest request)
    {
        request = default;

        Entity vehicleEntity = ResolveTransitRuntimeEntity(job, laneObject.m_LaneObject);
        if (vehicleEntity == Entity.Null
            || !job.m_ExtraTypeHandle.m_PublicTransport.TryGetComponent(vehicleEntity, out var publicTransport))
        {
            return false;
        }

        TransitApproachSuppressionFlags suppressionFlags = GetSuppressionFlags(publicTransport.m_State);
        bool isVehicleMoving = isPublicCarLane
            && IsMovingPublicTransportRoadVehicle(job, vehicleEntity, scanLaneEntity, laneObject, suppressionFlags);

        if (!EarlyApproachDetection.IsMovingEligibleApproachState(
                isEligibleLane: isPublicCarLane,
                isVehicleMoving,
                suppressionFlags))
        {
            return false;
        }

        request = laneRequest;
        return true;
    }

    private static Entity ResolveTransitRuntimeEntity(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        Entity laneObjectEntity)
    {
        Entity ownerEntity = Entity.Null;
        Entity grandOwnerEntity = Entity.Null;

        if (job.m_OwnerData.TryGetComponent(laneObjectEntity, out var owner))
        {
            ownerEntity = owner.m_Owner;
            if (ownerEntity != Entity.Null && job.m_OwnerData.TryGetComponent(ownerEntity, out var grandOwner))
            {
                grandOwnerEntity = grandOwner.m_Owner;
            }
        }

        return TransitApproachEntityResolver.SelectPreferredTransitEntity(
            laneObjectEntity,
            HasTransitRuntime(job, laneObjectEntity),
            ownerEntity,
            HasTransitRuntime(job, ownerEntity),
            grandOwnerEntity,
            HasTransitRuntime(job, grandOwnerEntity),
            Entity.Null);
    }

    private static bool HasTransitRuntime(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        Entity entity)
    {
        return entity != Entity.Null
            && (job.m_ExtraTypeHandle.m_PublicTransport.HasComponent(entity)
                || job.m_ExtraTypeHandle.m_TrainCurrentLane.HasComponent(entity)
                || job.m_ExtraTypeHandle.m_CarCurrentLane.HasComponent(entity));
    }

    private static bool IsTramTrackLane(PatchedTrafficLightSystem.UpdateTrafficLightsJob job, Entity approachLaneEntity)
    {
        if (!job.m_ExtraTypeHandle.m_TrackLane.HasComponent(approachLaneEntity))
        {
            return false;
        }

        if (!job.m_ExtraTypeHandle.m_PrefabRef.TryGetComponent(approachLaneEntity, out var prefabRef))
        {
            return false;
        }

        if (!job.m_ExtraTypeHandle.m_TrackLaneData.TryGetComponent(prefabRef.m_Prefab, out var trackLaneData))
        {
            return false;
        }

        return (trackLaneData.m_TrackTypes & TrackTypes.Tram) != 0;
    }

    private static bool TryBuildEarlyApproachRequestForTrackLane(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        Entity signaledLaneEntity,
        Entity approachLaneEntity,
        TspRequest laneRequest,
        out TspRequest request,
        out TransitSignalPriorityApproachLaneRole laneRole,
        out TransitSignalPriorityTrackProbeResult signaledLaneProbe,
        out TransitSignalPriorityTrackProbeResult approachLaneProbe,
        out TransitSignalPriorityTrackProbeResult upstreamLaneProbe)
    {
        request = default;
        laneRole = TransitSignalPriorityApproachLaneRole.None;
        signaledLaneProbe = TransitSignalPriorityTrackProbeResult.None;
        approachLaneProbe = TransitSignalPriorityTrackProbeResult.None;
        upstreamLaneProbe = TransitSignalPriorityTrackProbeResult.None;
        TrackProbeSnapshot signaledProbe = ProbeIndexedTrackLane(
            job.m_TramApproachIndex,
            signaledLaneEntity,
            TramApproachLaneCurveThreshold,
            isUpstreamLane: false);
        signaledLaneProbe = signaledProbe.Result;

        TrackProbeSnapshot approachProbe = signaledLaneEntity == approachLaneEntity
            ? signaledProbe
            : ProbeIndexedTrackLane(
                job.m_TramApproachIndex,
                approachLaneEntity,
                TramApproachLaneCurveThreshold,
                isUpstreamLane: false);

        approachLaneProbe = approachProbe.Result;

        Entity upstreamLaneEntity = TryResolveImmediateUpstreamTramLane(job, approachLaneEntity);
        TrackProbeSnapshot upstreamProbe = upstreamLaneEntity == Entity.Null
            ? default
            : ProbeIndexedTrackLane(
                job.m_TramApproachIndex,
                upstreamLaneEntity,
                TramUpstreamLaneCurveThreshold,
                isUpstreamLane: true);
        upstreamLaneProbe = upstreamProbe.Result;

        IndexedTrackProbeMatch indexedMatch = EarlyApproachDetection.EvaluateIndexedTrackTramSamples(
            approachProbe.HasSample,
            approachProbe.CurvePosition,
            upstreamProbe.HasSample,
            upstreamProbe.CurvePosition,
            TramApproachLaneCurveThreshold,
            TramUpstreamLaneCurveThreshold);

        switch (indexedMatch)
        {
            case IndexedTrackProbeMatch.MatchOnApproachLane:
                request = laneRequest;
                laneRole = TransitSignalPriorityApproachLaneRole.ApproachLane;
                return true;

            case IndexedTrackProbeMatch.MatchOnUpstreamLane:
                request = laneRequest;
                laneRole = TransitSignalPriorityApproachLaneRole.UpstreamLane;
                return true;

            default:
                return false;
        }
    }

    private static TrackProbeSnapshot ProbeIndexedTrackLane(
        NativeParallelHashMap<Entity, float>.ReadOnly tramApproachIndex,
        Entity laneEntity,
        float threshold,
        bool isUpstreamLane)
    {
        if (laneEntity == Entity.Null || !tramApproachIndex.TryGetValue(laneEntity, out float curvePosition))
        {
            return new TrackProbeSnapshot(false, 0f, TransitSignalPriorityTrackProbeResult.NoTramSamples);
        }

        if (curvePosition < threshold)
        {
            return new TrackProbeSnapshot(true, curvePosition, TransitSignalPriorityTrackProbeResult.BelowThreshold);
        }

        return new TrackProbeSnapshot(
            true,
            curvePosition,
            isUpstreamLane
                ? TransitSignalPriorityTrackProbeResult.MatchOnUpstreamLane
                : TransitSignalPriorityTrackProbeResult.MatchOnApproachLane);
    }

    private static Entity TryResolveImmediateUpstreamTramLane(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        Entity approachLaneEntity)
    {
        if (!job.m_OwnerData.TryGetComponent(approachLaneEntity, out var owner)
            || !job.m_SubLanes.TryGetBuffer(owner.m_Owner, out var subLanes)
            || !job.m_LaneData.TryGetComponent(approachLaneEntity, out var approachLane))
        {
            return Entity.Null;
        }

        for (int i = 0; i < subLanes.Length; i++)
        {
            Entity candidateLaneEntity = subLanes[i].m_SubLane;
            if (candidateLaneEntity == approachLaneEntity
                || !IsTramTrackLane(job, candidateLaneEntity)
                || !job.m_LaneData.TryGetComponent(candidateLaneEntity, out var candidateLane))
            {
                continue;
            }

            if (candidateLane.m_EndNode.Equals(approachLane.m_StartNode))
            {
                return candidateLaneEntity;
            }
        }

        return Entity.Null;
    }

    private static bool IsMovingPublicTransportRoadVehicle(
        PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
        Entity vehicleEntity,
        Entity subLaneEntity,
        LaneObject laneObject,
        TransitApproachSuppressionFlags suppressionFlags)
    {
        if (!job.m_ExtraTypeHandle.m_CarCurrentLane.TryGetComponent(vehicleEntity, out var currentLane)
            || currentLane.m_Lane != subLaneEntity
            || (currentLane.m_LaneFlags & BusStoppedLaneFlags) != 0)
        {
            return false;
        }

        float approachPosition = laneObject.m_CurvePosition.x;
        if (currentLane.m_CurvePosition.x > approachPosition)
        {
            approachPosition = currentLane.m_CurvePosition.x;
        }

        return EarlyApproachDetection.IsEligibleRoadTransitApproachState(
            isEligibleLane: true,
            hasReachedApproachThreshold: approachPosition >= BusApproachCurveThreshold,
            isBlocked: (currentLane.m_LaneFlags & VehicleCarLaneFlags.IsBlocked) != 0,
            hasReachedLaneEnd: (currentLane.m_LaneFlags & VehicleCarLaneFlags.EndReached) != 0,
            suppressionFlags: suppressionFlags);
    }

    private static TransitApproachSuppressionFlags GetSuppressionFlags(PublicTransportFlags state)
    {
        TransitApproachSuppressionFlags flags = TransitApproachSuppressionFlags.None;

        if ((state & PublicTransportFlags.Boarding) != 0)
        {
            flags |= TransitApproachSuppressionFlags.Boarding;
        }

        if ((state & PublicTransportFlags.Arriving) != 0)
        {
            flags |= TransitApproachSuppressionFlags.Arriving;
        }

        if ((state & PublicTransportFlags.RequireStop) != 0)
        {
            flags |= TransitApproachSuppressionFlags.RequireStop;
        }

        return flags;
    }

    private static TransitSignalPriorityRequest CreateRequest(
        TspRequest request,
        ushort expiryTimer,
        byte currentSignalGroup,
        byte targetSignalGroup,
        LaneSignal laneSignal)
    {
        return new TransitSignalPriorityRequest
        {
            m_TargetSignalGroup = targetSignalGroup,
            m_SourceType = (byte)request.Source,
            m_Strength = request.Strength,
            m_ExpiryTimer = expiryTimer,
            m_ExtendCurrentPhase = request.ExtensionEligible
                && currentSignalGroup == targetSignalGroup
                && (laneSignal.m_Flags & LaneSignalFlags.CanExtend) != 0,
        };
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

        if ((carLane.m_Flags & NetCarLaneFlags.PublicOnly) != 0)
        {
            return true;
        }

        if (!job.m_ExtraTypeHandle.m_ExtraLaneSignal.TryGetComponent(subLaneEntity, out var extraLaneSignal))
        {
            return false;
        }

        return extraLaneSignal.m_SourceSubLane != Entity.Null
            && job.m_ExtraTypeHandle.m_CarLane.TryGetComponent(extraLaneSignal.m_SourceSubLane, out var sourceCarLane)
            && (sourceCarLane.m_Flags & NetCarLaneFlags.PublicOnly) != 0;
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
