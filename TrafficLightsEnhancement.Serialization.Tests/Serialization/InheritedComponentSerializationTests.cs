using C2VM.TrafficLightsEnhancement.Components;
using C2VM.TrafficLightsEnhancement.Systems.Serialization;
using Unity.Entities;
using Unity.Mathematics;
using Xunit;

namespace TrafficLightsEnhancement.Serialization.Tests.Serialization;

public class InheritedComponentSerializationTests
{
    [Fact]
    public void Signal_delay_data_round_trips_current_payload()
    {
        var source = new SignalDelayData(EntityAt(701, 3), openDelay: 11, closeDelay: 17, isEnabled: true);

        var result = RoundTrip<SignalDelayData>(source);

        Assert.Equal(source.m_Edge, result.m_Edge);
        Assert.Equal(source.m_OpenDelay, result.m_OpenDelay);
        Assert.Equal(source.m_CloseDelay, result.m_CloseDelay);
        Assert.Equal(source.m_IsEnabled, result.m_IsEnabled);
    }

    [Fact]
    public void Custom_traffic_lights_round_trips_current_payload()
    {
        var source = new CustomTrafficLights(CustomTrafficLights.Patterns.CustomPhase, CustomTrafficLights.TrafficMode.FixedTimed);
        source.SetOptions(CustomTrafficLights.TrafficOptions.None);
        source.SetPedestrianPhaseDurationMultiplier(2.5f);
        source.SetPedestrianPhaseGroupMask(0b1010);
        source.m_Timer = 42;
        source.m_ManualSignalGroup = 3;

        var result = RoundTrip<CustomTrafficLights>(source);

        Assert.Equal(source.GetPattern(), result.GetPattern());
        Assert.Equal(source.GetMode(), result.GetMode());
        Assert.Equal(source.GetOptions(), result.GetOptions());
        Assert.Equal(source.m_PedestrianPhaseDurationMultiplier, result.m_PedestrianPhaseDurationMultiplier);
        Assert.Equal(source.m_PedestrianPhaseGroupMask, result.m_PedestrianPhaseGroupMask);
        Assert.Equal(source.m_Timer, result.m_Timer);
        Assert.Equal(source.m_ManualSignalGroup, result.m_ManualSignalGroup);
    }

    [Fact]
    public void Custom_traffic_lights_deserializes_v4_payload_with_default_mode_and_options()
    {
        var result = Deserialize<CustomTrafficLights>(writer =>
        {
            writer.Write(TLEDataVersion.V4);
            writer.Write((uint)CustomTrafficLights.Patterns.ExclusivePedestrian);
            writer.Write(3.5f);
            writer.Write(0b101);
            writer.Write(77u);
            writer.Write((byte)4);
        });

        Assert.Equal(CustomTrafficLights.Patterns.ExclusivePedestrian, result.GetPattern());
        Assert.Equal(3.5f, result.m_PedestrianPhaseDurationMultiplier);
        Assert.Equal(0b101, result.m_PedestrianPhaseGroupMask);
        Assert.Equal(77u, result.m_Timer);
        Assert.Equal((byte)4, result.m_ManualSignalGroup);
        Assert.Equal(CustomTrafficLights.TrafficMode.Dynamic, result.GetMode());
        Assert.Equal(CustomTrafficLights.TrafficOptions.SmartPhaseSelection, result.GetOptions());
    }

    [Fact]
    public void Custom_traffic_lights_deserializes_v1_payload_as_vanilla()
    {
        var result = Deserialize<CustomTrafficLights>(writer =>
        {
            writer.Write(TLEDataVersion.V1);
            for (int i = 1; i < 16; i++)
            {
                writer.Write((uint)CustomTrafficLights.Patterns.CustomPhase);
            }
        });

        Assert.Equal(CustomTrafficLights.Patterns.Vanilla, result.GetPattern());
        Assert.Equal(1f, result.m_PedestrianPhaseDurationMultiplier);
        Assert.Equal(0, result.m_PedestrianPhaseGroupMask);
        Assert.Equal(0u, result.m_Timer);
        Assert.Equal((byte)0, result.m_ManualSignalGroup);
        Assert.Equal(CustomTrafficLights.TrafficMode.Dynamic, result.GetMode());
        Assert.Equal(CustomTrafficLights.TrafficOptions.SmartPhaseSelection, result.GetOptions());
    }

    [Fact]
    public void Custom_traffic_lights_deserializes_v2_payload_with_default_later_fields()
    {
        var result = Deserialize<CustomTrafficLights>(writer =>
        {
            writer.Write(TLEDataVersion.V2);
            writer.Write((uint)CustomTrafficLights.Patterns.SplitPhasing);
        });

        Assert.Equal(CustomTrafficLights.Patterns.SplitPhasing, result.GetPattern());
        Assert.Equal(1f, result.m_PedestrianPhaseDurationMultiplier);
        Assert.Equal(0, result.m_PedestrianPhaseGroupMask);
        Assert.Equal(0u, result.m_Timer);
        Assert.Equal((byte)0, result.m_ManualSignalGroup);
        Assert.Equal(CustomTrafficLights.TrafficMode.Dynamic, result.GetMode());
        Assert.Equal(CustomTrafficLights.TrafficOptions.SmartPhaseSelection, result.GetOptions());
    }

    [Fact]
    public void Transit_signal_priority_settings_round_trips_current_payload()
    {
        var source = TransitSignalPrioritySettings.CreateDefault();
        source.m_Enabled = true;
        source.m_AllowTrackRequests = true;
        source.m_AllowPublicCarRequests = true;
        source.m_RequestHorizonTicks = 17;
        source.m_MaxGreenExtensionTicks = 88;

        var result = RoundTrip<TransitSignalPrioritySettings>(source);

        Assert.True(result.m_Enabled);
        Assert.True(result.m_AllowTrackRequests);
        Assert.True(result.m_AllowPublicCarRequests);
        Assert.Equal((ushort)17, result.m_RequestHorizonTicks);
        Assert.Equal((ushort)88, result.m_MaxGreenExtensionTicks);
    }

    [Fact]
    public void Transit_signal_priority_settings_deserializes_v1_payload_and_discards_group_propagation()
    {
        var result = Deserialize<TransitSignalPrioritySettings>(writer =>
        {
            writer.Write(1);
            writer.Write(true);
            writer.Write(false);
            writer.Write(true);
            writer.Write(true);
            writer.Write(global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings.LegacyDefaultRequestHorizonTicks);
            writer.Write((ushort)0);
        });

        Assert.True(result.m_Enabled);
        Assert.False(result.m_AllowTrackRequests);
        Assert.True(result.m_AllowPublicCarRequests);
        Assert.Equal(
            global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings.DefaultRequestHorizonTicks,
            result.m_RequestHorizonTicks);
        Assert.Equal(
            global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings.DefaultMaxGreenExtensionTicks,
            result.m_MaxGreenExtensionTicks);
    }

    [Fact]
    public void Transit_signal_priority_settings_deserializes_v2_payload_and_normalizes_upper_limits()
    {
        var result = Deserialize<TransitSignalPrioritySettings>(writer =>
        {
            writer.Write(2);
            writer.Write(true);
            writer.Write(false);
            writer.Write(true);
            writer.Write(ushort.MaxValue);
            writer.Write(ushort.MaxValue);
        });

        Assert.True(result.m_Enabled);
        Assert.False(result.m_AllowTrackRequests);
        Assert.True(result.m_AllowPublicCarRequests);
        Assert.Equal(
            global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings.MaxRequestHorizonTicksUpperBound,
            result.m_RequestHorizonTicks);
        Assert.Equal(
            global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings.MaxGreenExtensionTicksUpperBound,
            result.m_MaxGreenExtensionTicks);
    }

    [Fact]
    public void Transit_signal_priority_settings_deserializes_pre_v1_payload_as_defaults()
    {
        var result = Deserialize<TransitSignalPrioritySettings>(writer =>
        {
            writer.Write(0);
        });

        Assert.False(result.m_Enabled);
        Assert.True(result.m_AllowTrackRequests);
        Assert.False(result.m_AllowPublicCarRequests);
        Assert.Equal(
            global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings.DefaultRequestHorizonTicks,
            result.m_RequestHorizonTicks);
        Assert.Equal(
            global::TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings.DefaultMaxGreenExtensionTicks,
            result.m_MaxGreenExtensionTicks);
    }

    [Fact]
    public void Traffic_group_round_trips_current_payload_and_resets_runtime_fields()
    {
        var source = new TrafficGroup(isCoordinated: true, greenWaveEnabled: true, greenWaveSpeed: 70f, greenWaveOffset: 1.5f, maxCoordinationDistance: 800f, cycleLength: 64f)
        {
            m_CreationTime = 1234,
            m_LastSyncTime = 9f,
            m_CycleTimer = 12f,
            m_MasterPhase = 4,
            m_MasterNextPhase = 5,
            m_MasterTimer = 6,
            m_MasterCustomTimer = 7,
            m_MasterSignalGroupCount = 8
        };

        var result = RoundTrip<TrafficGroup>(source);

        Assert.Equal(source.m_IsCoordinated, result.m_IsCoordinated);
        Assert.Equal(source.m_GreenWaveEnabled, result.m_GreenWaveEnabled);
        Assert.Equal(source.m_GreenWaveSpeed, result.m_GreenWaveSpeed);
        Assert.Equal(source.m_GreenWaveOffset, result.m_GreenWaveOffset);
        Assert.Equal(source.m_MaxCoordinationDistance, result.m_MaxCoordinationDistance);
        Assert.Equal(source.m_CreationTime, result.m_CreationTime);
        Assert.Equal(source.m_CycleLength, result.m_CycleLength);
        Assert.Equal(0f, result.m_LastSyncTime);
        Assert.Equal(0f, result.m_CycleTimer);
        Assert.Equal(0, result.m_MasterPhase);
        Assert.Equal(0, result.m_MasterNextPhase);
        Assert.Equal(0, result.m_MasterTimer);
        Assert.Equal(0u, result.m_MasterCustomTimer);
        Assert.Equal(0, result.m_MasterSignalGroupCount);
    }

    [Fact]
    public void Traffic_group_deserializes_v2_payload_and_discards_legacy_group_propagation()
    {
        var result = Deserialize<TrafficGroup>(writer =>
        {
            writer.Write(TLEDataVersion.V2);
            writer.Write(true);
            writer.Write(false);
            writer.Write(true);
            writer.Write(44f);
            writer.Write(5.5f);
            writer.Write(123f);
            writer.Write(987u);
            writer.Write(30f);
        });

        Assert.True(result.m_IsCoordinated);
        Assert.False(result.m_GreenWaveEnabled);
        Assert.Equal(44f, result.m_GreenWaveSpeed);
        Assert.Equal(5.5f, result.m_GreenWaveOffset);
        Assert.Equal(123f, result.m_MaxCoordinationDistance);
        Assert.Equal(987u, result.m_CreationTime);
        Assert.Equal(30f, result.m_CycleLength);
    }

    [Fact]
    public void Traffic_group_member_round_trips_current_payload()
    {
        var source = new TrafficGroupMember(
            EntityAt(11, 1),
            EntityAt(12, 2),
            groupIndex: 5,
            distanceToCenter: 50.5f,
            distanceToLeader: 75.25f,
            phaseOffset: 2,
            signalDelay: 3,
            memberCycleTimer: 4.5f,
            isGroupLeader: true);

        var result = RoundTrip<TrafficGroupMember>(source);

        Assert.Equal(source.m_GroupEntity, result.m_GroupEntity);
        Assert.Equal(source.m_LeaderEntity, result.m_LeaderEntity);
        Assert.Equal(source.m_GroupIndex, result.m_GroupIndex);
        Assert.Equal(source.m_DistanceToGroupCenter, result.m_DistanceToGroupCenter);
        Assert.Equal(source.m_DistanceToLeader, result.m_DistanceToLeader);
        Assert.Equal(source.m_PhaseOffset, result.m_PhaseOffset);
        Assert.Equal(source.m_SignalDelay, result.m_SignalDelay);
        Assert.Equal(source.m_MemberCycleTimer, result.m_MemberCycleTimer);
        Assert.Equal(source.m_IsGroupLeader, result.m_IsGroupLeader);
    }

    [Fact]
    public void Traffic_group_member_deserializes_v1_payload_with_default_member_cycle_timer()
    {
        var group = EntityAt(41, 4);
        var leader = EntityAt(42, 5);

        var result = Deserialize<TrafficGroupMember>(writer =>
        {
            writer.Write(TLEDataVersion.V1);
            writer.Write(group);
            writer.Write(leader);
            writer.Write(6);
            writer.Write(10.5f);
            writer.Write(11.5f);
            writer.Write(7);
            writer.Write(8);
            writer.Write(true);
        });

        Assert.Equal(group, result.m_GroupEntity);
        Assert.Equal(leader, result.m_LeaderEntity);
        Assert.Equal(6, result.m_GroupIndex);
        Assert.Equal(10.5f, result.m_DistanceToGroupCenter);
        Assert.Equal(11.5f, result.m_DistanceToLeader);
        Assert.Equal(7, result.m_PhaseOffset);
        Assert.Equal(8, result.m_SignalDelay);
        Assert.Equal(0f, result.m_MemberCycleTimer);
        Assert.True(result.m_IsGroupLeader);
    }

    [Fact]
    public void Edge_group_mask_round_trips_current_payload()
    {
        var source = new EdgeGroupMask(EntityAt(21, 2), new float3(1f, 2f, 3f))
        {
            m_Options = EdgeGroupMask.Options.PerLaneSignal,
            m_Car = Turn(1),
            m_PublicCar = Turn(11),
            m_Track = Turn(21),
            m_PedestrianStopLine = Signal(31),
            m_PedestrianNonStopLine = Signal(41),
            m_Pedestrian = Signal(51),
            m_Bicycle = Signal(61),
            m_OpenDelay = 7,
            m_CloseDelay = 8
        };

        var result = RoundTrip<EdgeGroupMask>(source);

        Assert.Equal(source.m_Edge, result.m_Edge);
        Assert.Equal((float3)source.m_Position, (float3)result.m_Position);
        Assert.Equal(source.m_Options, result.m_Options);
        AssertTurn(source.m_Car, result.m_Car);
        AssertTurn(source.m_PublicCar, result.m_PublicCar);
        AssertTurn(source.m_Track, result.m_Track);
        AssertSignal(source.m_PedestrianStopLine, result.m_PedestrianStopLine);
        AssertSignal(source.m_PedestrianNonStopLine, result.m_PedestrianNonStopLine);
        AssertSignal(source.m_Pedestrian, result.m_Pedestrian);
        AssertSignal(source.m_Bicycle, result.m_Bicycle);
        Assert.Equal(source.m_OpenDelay, result.m_OpenDelay);
        Assert.Equal(source.m_CloseDelay, result.m_CloseDelay);
    }

    [Fact]
    public void Edge_group_mask_deserializes_v1_payload_with_merged_pedestrian_mask()
    {
        var edge = EntityAt(51, 5);
        var position = new float3(7f, 8f, 9f);
        var stopLine = Signal(71);
        var nonStopLine = Signal(81);

        var result = Deserialize<EdgeGroupMask>(writer =>
        {
            writer.Write((ushort)TLEDataVersion.V1);
            writer.Write(edge);
            writer.Write(position);
            writer.Write((uint)EdgeGroupMask.Options.PerLaneSignal);
            writer.Write(Turn(1));
            writer.Write(Turn(11));
            writer.Write(Turn(21));
            writer.Write(stopLine);
            writer.Write(nonStopLine);
        });

        Assert.Equal(edge, result.m_Edge);
        Assert.Equal(position, (float3)result.m_Position);
        Assert.Equal(EdgeGroupMask.Options.PerLaneSignal, result.m_Options);
        Assert.Equal((ushort)(stopLine.m_GoGroupMask | nonStopLine.m_GoGroupMask), result.m_Pedestrian.m_GoGroupMask);
        Assert.Equal((ushort)(stopLine.m_YieldGroupMask | nonStopLine.m_YieldGroupMask), result.m_Pedestrian.m_YieldGroupMask);
        AssertSignal(new GroupMask.Signal(), result.m_Bicycle);
        Assert.Equal(0, result.m_OpenDelay);
        Assert.Equal(0, result.m_CloseDelay);
    }

    [Fact]
    public void Sub_lane_group_mask_round_trips_current_payload()
    {
        var source = new SubLaneGroupMask(EntityAt(31, 3), new float3(4f, 5f, 6f))
        {
            m_Car = Turn(2),
            m_Track = Turn(12),
            m_Pedestrian = Signal(22)
        };

        var result = RoundTrip<SubLaneGroupMask>(source);

        Assert.Equal(source.m_SubLane, result.m_SubLane);
        Assert.Equal((float3)source.m_Position, (float3)result.m_Position);
        Assert.Equal(source.m_Options, result.m_Options);
        AssertTurn(source.m_Car, result.m_Car);
        AssertTurn(source.m_Track, result.m_Track);
        AssertSignal(source.m_Pedestrian, result.m_Pedestrian);
    }

    [Fact]
    public void Custom_phase_data_round_trips_current_payload()
    {
        var source = new CustomPhaseData
        {
            m_TurnsSinceLastRun = 1,
            m_LowFlowTimer = 2,
            m_LowPriorityTimer = 3,
            m_CarFlow = new float3(4f, 5f, 6f),
            m_CarLaneOccupied = 7,
            m_PublicCarLaneOccupied = 8,
            m_TrackLaneOccupied = 9,
            m_PedestrianLaneOccupied = 10,
            m_WeightedWaiting = 11.5f,
            m_TargetDuration = 12.5f,
            m_Priority = 13,
            m_Options = CustomPhaseData.Options.PrioritiseTrack | CustomPhaseData.Options.LinkedWithNextPhase | CustomPhaseData.Options.PrioritiseBicycle,
            m_MinimumDuration = 14,
            m_MaximumDuration = 15,
            m_TargetDurationMultiplier = 1.6f,
            m_LaneOccupiedMultiplier = 1.7f,
            m_IntervalExponent = 1.8f,
            m_BicycleLaneOccupied = 16,
            m_ChangeMetric = CustomPhaseData.StepChangeMetric.FirstWait,
            m_WaitFlowBalance = 1.9f,
            m_CarOpenDelay = 20,
            m_CarCloseDelay = 21,
            m_PublicCarOpenDelay = 22,
            m_PublicCarCloseDelay = 23,
            m_TrackOpenDelay = 24,
            m_TrackCloseDelay = 25,
            m_PedestrianOpenDelay = 26,
            m_PedestrianCloseDelay = 27,
            m_BicycleOpenDelay = 28,
            m_BicycleCloseDelay = 29,
            m_CarWeight = 2.1f,
            m_PublicCarWeight = 2.2f,
            m_TrackWeight = 2.3f,
            m_PedestrianWeight = 2.4f,
            m_BicycleWeight = 2.5f,
            m_FlowRatio = 2.6f,
            m_WaitRatio = 2.7f,
            m_SmoothingFactor = 0.8f,
            m_NextStepRefIndex = 30,
            m_CurrentFlow = 2.9f,
            m_CurrentWait = 3.1f
        };

        var result = RoundTrip<CustomPhaseData>(source);

        Assert.Equal(source.m_TurnsSinceLastRun, result.m_TurnsSinceLastRun);
        Assert.Equal(source.m_LowFlowTimer, result.m_LowFlowTimer);
        Assert.Equal(source.m_LowPriorityTimer, result.m_LowPriorityTimer);
        Assert.Equal(source.m_CarFlow, result.m_CarFlow);
        Assert.Equal(source.m_CarLaneOccupied, result.m_CarLaneOccupied);
        Assert.Equal(source.m_PublicCarLaneOccupied, result.m_PublicCarLaneOccupied);
        Assert.Equal(source.m_TrackLaneOccupied, result.m_TrackLaneOccupied);
        Assert.Equal(source.m_PedestrianLaneOccupied, result.m_PedestrianLaneOccupied);
        Assert.Equal(source.m_WeightedWaiting, result.m_WeightedWaiting);
        Assert.Equal(source.m_TargetDuration, result.m_TargetDuration);
        Assert.Equal(source.m_Priority, result.m_Priority);
        Assert.Equal(source.m_Options, result.m_Options);
        Assert.Equal(source.m_MinimumDuration, result.m_MinimumDuration);
        Assert.Equal(source.m_MaximumDuration, result.m_MaximumDuration);
        Assert.Equal(source.m_TargetDurationMultiplier, result.m_TargetDurationMultiplier);
        Assert.Equal(source.m_LaneOccupiedMultiplier, result.m_LaneOccupiedMultiplier);
        Assert.Equal(source.m_IntervalExponent, result.m_IntervalExponent);
        Assert.Equal(source.m_BicycleLaneOccupied, result.m_BicycleLaneOccupied);
        Assert.Equal(source.m_ChangeMetric, result.m_ChangeMetric);
        Assert.Equal(source.m_WaitFlowBalance, result.m_WaitFlowBalance);
        Assert.Equal(source.m_CarOpenDelay, result.m_CarOpenDelay);
        Assert.Equal(source.m_CarCloseDelay, result.m_CarCloseDelay);
        Assert.Equal(source.m_PublicCarOpenDelay, result.m_PublicCarOpenDelay);
        Assert.Equal(source.m_PublicCarCloseDelay, result.m_PublicCarCloseDelay);
        Assert.Equal(source.m_TrackOpenDelay, result.m_TrackOpenDelay);
        Assert.Equal(source.m_TrackCloseDelay, result.m_TrackCloseDelay);
        Assert.Equal(source.m_PedestrianOpenDelay, result.m_PedestrianOpenDelay);
        Assert.Equal(source.m_PedestrianCloseDelay, result.m_PedestrianCloseDelay);
        Assert.Equal(source.m_BicycleOpenDelay, result.m_BicycleOpenDelay);
        Assert.Equal(source.m_BicycleCloseDelay, result.m_BicycleCloseDelay);
        Assert.Equal(source.m_CarWeight, result.m_CarWeight);
        Assert.Equal(source.m_PublicCarWeight, result.m_PublicCarWeight);
        Assert.Equal(source.m_TrackWeight, result.m_TrackWeight);
        Assert.Equal(source.m_PedestrianWeight, result.m_PedestrianWeight);
        Assert.Equal(source.m_BicycleWeight, result.m_BicycleWeight);
        Assert.Equal(source.m_FlowRatio, result.m_FlowRatio);
        Assert.Equal(source.m_WaitRatio, result.m_WaitRatio);
        Assert.Equal(source.m_SmoothingFactor, result.m_SmoothingFactor);
        Assert.Equal(source.m_NextStepRefIndex, result.m_NextStepRefIndex);
        Assert.Equal(source.m_CurrentFlow, result.m_CurrentFlow);
        Assert.Equal(source.m_CurrentWait, result.m_CurrentWait);
    }

    [Fact]
    public void Custom_phase_data_deserializes_v1_payload_with_v2_defaults()
    {
        var result = Deserialize<CustomPhaseData>(writer =>
        {
            writer.Write((ushort)TLEDataVersion.V1);
            writer.Write((ushort)1);
            writer.Write((ushort)2);
            writer.Write((ushort)3);
            writer.Write(new float3(4f, 5f, 6f));
            writer.Write((ushort)7);
            writer.Write((ushort)8);
            writer.Write((ushort)9);
            writer.Write((ushort)10);
            writer.Write(11.5f);
            writer.Write(12.5f);
            writer.Write(13);
            writer.Write((uint)CustomPhaseData.Options.PrioritisePedestrian);
            writer.Write((ushort)14);
            writer.Write((ushort)15);
            writer.Write(1.6f);
            writer.Write(1.7f);
            writer.Write(1.8f);
        });

        Assert.Equal((ushort)1, result.m_TurnsSinceLastRun);
        Assert.Equal((ushort)2, result.m_LowFlowTimer);
        Assert.Equal((ushort)3, result.m_LowPriorityTimer);
        Assert.Equal(new float3(4f, 5f, 6f), result.m_CarFlow);
        Assert.Equal((ushort)7, result.m_CarLaneOccupied);
        Assert.Equal((ushort)8, result.m_PublicCarLaneOccupied);
        Assert.Equal((ushort)9, result.m_TrackLaneOccupied);
        Assert.Equal((ushort)10, result.m_PedestrianLaneOccupied);
        Assert.Equal(11.5f, result.m_WeightedWaiting);
        Assert.Equal(12.5f, result.m_TargetDuration);
        Assert.Equal(13, result.m_Priority);
        Assert.Equal(CustomPhaseData.Options.PrioritisePedestrian, result.m_Options);
        Assert.Equal((ushort)14, result.m_MinimumDuration);
        Assert.Equal((ushort)15, result.m_MaximumDuration);
        Assert.Equal(1.6f, result.m_TargetDurationMultiplier);
        Assert.Equal(1.7f, result.m_LaneOccupiedMultiplier);
        Assert.Equal(1.8f, result.m_IntervalExponent);
        Assert.Equal((ushort)0, result.m_BicycleLaneOccupied);
        Assert.Equal(CustomPhaseData.StepChangeMetric.Default, result.m_ChangeMetric);
        Assert.Equal(1f, result.m_WaitFlowBalance);
        Assert.Equal(0, result.m_CarOpenDelay);
        Assert.Equal(1.0f, result.m_CarWeight);
        Assert.Equal(2.0f, result.m_PublicCarWeight);
        Assert.Equal(3.0f, result.m_TrackWeight);
        Assert.Equal(0.5f, result.m_SmoothingFactor);
        Assert.Equal(-1, result.m_NextStepRefIndex);
    }

    [Fact]
    public void Group_mask_signal_deserializes_v1_payload_with_zero_delays()
    {
        var result = Deserialize<GroupMask.Signal>(writer =>
        {
            writer.Write((ushort)TLEDataVersion.V1);
            writer.Write((ushort)33);
            writer.Write((ushort)44);
        });

        Assert.Equal((ushort)33, result.m_GoGroupMask);
        Assert.Equal((ushort)44, result.m_YieldGroupMask);
        Assert.Equal(0f, result.m_OpenDelay);
        Assert.Equal(0f, result.m_CloseDelay);
    }

    private static T RoundTrip<T>(T source) where T : struct, Colossal.Serialization.Entities.ISerializable
    {
        var writer = new SerializationWriter();
        source.Serialize(writer);
        return Deserialize<T>(writer.Values);
    }

    private static T Deserialize<T>(Action<SerializationWriter> write) where T : struct, Colossal.Serialization.Entities.ISerializable
    {
        var writer = new SerializationWriter();
        write(writer);
        return Deserialize<T>(writer.Values);
    }

    private static T Deserialize<T>(IReadOnlyList<object> values) where T : struct, Colossal.Serialization.Entities.ISerializable
    {
        var reader = new SerializationReader(values);

        var result = default(T);
        result.Deserialize(reader);
        return result;
    }

    private static Entity EntityAt(int index, int version)
    {
        return new Entity { Index = index, Version = version };
    }

    private static GroupMask.Signal Signal(ushort seed)
    {
        return new GroupMask.Signal
        {
            m_GoGroupMask = seed,
            m_YieldGroupMask = (ushort)(seed + 1),
            m_OpenDelay = seed + 0.25f,
            m_CloseDelay = seed + 0.75f
        };
    }

    private static GroupMask.Turn Turn(ushort seed)
    {
        return new GroupMask.Turn
        {
            m_Left = Signal(seed),
            m_Straight = Signal((ushort)(seed + 2)),
            m_Right = Signal((ushort)(seed + 4)),
            m_UTurn = Signal((ushort)(seed + 6))
        };
    }

    private static void AssertSignal(GroupMask.Signal expected, GroupMask.Signal actual)
    {
        Assert.Equal(expected.m_GoGroupMask, actual.m_GoGroupMask);
        Assert.Equal(expected.m_YieldGroupMask, actual.m_YieldGroupMask);
        Assert.Equal(expected.m_OpenDelay, actual.m_OpenDelay);
        Assert.Equal(expected.m_CloseDelay, actual.m_CloseDelay);
    }

    private static void AssertTurn(GroupMask.Turn expected, GroupMask.Turn actual)
    {
        AssertSignal(expected.m_Left, actual.m_Left);
        AssertSignal(expected.m_Straight, actual.m_Straight);
        AssertSignal(expected.m_Right, actual.m_Right);
        AssertSignal(expected.m_UTurn, actual.m_UTurn);
    }
}
