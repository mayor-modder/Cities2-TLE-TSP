using C2VM.TrafficLightsEnhancement.Components;
using C2VM.TrafficLightsEnhancement.Systems.TrafficLightSystems.Simulation;
using Game.Vehicles;
using TrafficLightsEnhancement.Logic.Tsp;
using Xunit;
using EcsTspRuntime = C2VM.TrafficLightsEnhancement.Systems.TrafficLightSystems.Simulation.TransitSignalPriorityRuntime;
using LogicSettings = TrafficLightsEnhancement.Logic.Tsp.TransitSignalPrioritySettings;

namespace TrafficLightsEnhancement.Ecs.Tests;

public class TransitSignalPriorityRuntimeBusRequestTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bus_sample_builds_public_car_request(bool isBusOnlyLane)
    {
        bool hasRequest = EcsTspRuntime.TryBuildBusApproachRequestFromSample(
            BusSettings(enabled: true),
            Sample(curvePosition: 0.5f, isBusOnlyLane: isBusOnlyLane),
            out TspRequest request,
            out TransitSignalPriorityBusDecision decision,
            out BusPrioritySuppressionReason suppressionReason);

        Assert.True(hasRequest);
        Assert.Equal(TspSource.PublicCar, request.Source);
        Assert.Equal(TransitSignalPriorityBusDecision.RequestEmitted, decision);
        Assert.Equal(BusPrioritySuppressionReason.None, suppressionReason);
    }

    [Fact]
    public void Boarding_bus_sample_is_suppressed()
    {
        bool hasRequest = EcsTspRuntime.TryBuildBusApproachRequestFromSample(
            BusSettings(enabled: true),
            Sample(curvePosition: 0.5f, state: PublicTransportFlags.Boarding),
            out _,
            out TransitSignalPriorityBusDecision decision,
            out BusPrioritySuppressionReason suppressionReason);

        Assert.False(hasRequest);
        Assert.Equal(TransitSignalPriorityBusDecision.SuppressedBoarding, decision);
        Assert.Equal(BusPrioritySuppressionReason.Boarding, suppressionReason);
    }

    [Fact]
    public void Dummy_traffic_bus_sample_is_not_eligible_for_priority()
    {
        bool hasRequest = EcsTspRuntime.TryBuildBusApproachRequestFromSample(
            BusSettings(enabled: true),
            Sample(curvePosition: 0.8f, state: PublicTransportFlags.DummyTraffic),
            out _,
            out TransitSignalPriorityBusDecision decision,
            out BusPrioritySuppressionReason suppressionReason);

        Assert.False(hasRequest);
        Assert.Equal(TransitSignalPriorityBusDecision.NoEligibleSample, decision);
        Assert.Equal(BusPrioritySuppressionReason.None, suppressionReason);
    }

    [Fact]
    public void Route_source_bus_sample_can_emit_priority_request()
    {
        bool hasRequest = EcsTspRuntime.TryBuildBusApproachRequestFromSample(
            BusSettings(enabled: true),
            Sample(curvePosition: 0.8f, state: PublicTransportFlags.RouteSource),
            out TspRequest request,
            out TransitSignalPriorityBusDecision decision,
            out BusPrioritySuppressionReason suppressionReason);

        Assert.True(hasRequest);
        Assert.Equal(TspSource.PublicCar, request.Source);
        Assert.Equal(TransitSignalPriorityBusDecision.RequestEmitted, decision);
        Assert.Equal(BusPrioritySuppressionReason.None, suppressionReason);
    }

    [Fact]
    public void Bus_priority_toggle_off_short_circuits_sample()
    {
        bool hasRequest = EcsTspRuntime.TryBuildBusApproachRequestFromSample(
            BusSettings(enabled: true, allowPublicCarRequests: false),
            Sample(curvePosition: 0.5f),
            out _,
            out TransitSignalPriorityBusDecision decision,
            out BusPrioritySuppressionReason suppressionReason);

        Assert.False(hasRequest);
        Assert.Equal(TransitSignalPriorityBusDecision.PriorityDisabled, decision);
        Assert.Equal(BusPrioritySuppressionReason.None, suppressionReason);
    }

    [Fact]
    public void Low_curve_connected_approach_bus_sample_is_not_close_enough()
    {
        bool hasRequest = EcsTspRuntime.TryBuildBusApproachRequestFromSample(
            BusSettings(enabled: true),
            Sample(curvePosition: 0.1f, state: PublicTransportFlags.RequireStop),
            TransitSignalPriorityBusProbeResult.MatchOnConnectedApproachLane,
            out _,
            out TransitSignalPriorityBusDecision decision,
            out BusPrioritySuppressionReason suppressionReason);

        Assert.False(hasRequest);
        Assert.Equal(TransitSignalPriorityBusDecision.NoEligibleSample, decision);
        Assert.Equal(BusPrioritySuppressionReason.None, suppressionReason);
    }

    [Fact]
    public void Low_curve_signaled_lane_require_stop_bus_sample_builds_request()
    {
        bool hasRequest = EcsTspRuntime.TryBuildBusApproachRequestFromSample(
            BusSettings(enabled: true),
            Sample(curvePosition: 0.1f, state: PublicTransportFlags.RequireStop),
            TransitSignalPriorityBusProbeResult.MatchOnSignaledLane,
            out TspRequest request,
            out TransitSignalPriorityBusDecision decision,
            out BusPrioritySuppressionReason suppressionReason);

        Assert.True(hasRequest);
        Assert.Equal(TspSource.PublicCar, request.Source);
        Assert.Equal(TransitSignalPriorityBusDecision.RequestEmitted, decision);
        Assert.Equal(BusPrioritySuppressionReason.None, suppressionReason);
    }

    [Fact]
    public void Bus_request_loses_ranking_to_track_candidate()
    {
        bool hasRequest = EcsTspRuntime.TryBuildBusApproachRequestFromSample(
            BusSettings(enabled: true),
            Sample(curvePosition: 0.5f),
            out TspRequest busRequest,
            out _,
            out _);

        Assert.True(hasRequest);

        var trackRequest = new TspRequest(TspSource.Track, strength: 0.5f, extensionEligible: false);
        TspRequest? selected = EcsTspRuntime.SelectPreferredRequestAndRole(
            trackRequest,
            TransitSignalPriorityApproachLaneRole.ApproachLane,
            busRequest,
            TransitSignalPriorityApproachLaneRole.UpstreamLane,
            out TransitSignalPriorityApproachLaneRole selectedRole);

        Assert.True(selected.HasValue);
        Assert.Equal(TspSource.Track, selected.Value.Source);
        Assert.Equal(TransitSignalPriorityApproachLaneRole.ApproachLane, selectedRole);
    }

    private static LogicSettings BusSettings(
        bool enabled,
        bool allowPublicCarRequests = true)
    {
        return new LogicSettings(
            enabled: enabled,
            allowTrackRequests: false,
            allowPublicCarRequests: allowPublicCarRequests);
    }

    private static BusApproachSample Sample(
        float curvePosition,
        bool isBusOnlyLane = false,
        PublicTransportFlags state = 0)
    {
        return new BusApproachSample
        {
            CurvePosition = curvePosition,
            Speed = 1f,
            IsBusOnlyLane = isBusOnlyLane ? (byte)1 : (byte)0,
            PublicTransportState = state,
        };
    }
}
