using C2VM.TrafficLightsEnhancement.Components;
using C2VM.TrafficLightsEnhancement.Systems.TrafficLightSystems.Initialisation;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests;

public sealed class PredefinedPatternOptionSupportSourceTests
{
    [Theory]
    [InlineData(7, false, true)]
    [InlineData(8, false, false)]
    [InlineData(4, true, false)]
    public void Extra_options_are_supported_only_on_non_train_topologies_with_at_most_seven_edges(
        int edgeCount,
        bool hasTrainTrack,
        bool expected)
    {
        bool actual = PredefinedPatternsProcessor.AreExtraOptionsSupported(edgeCount, hasTrainTrack);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Exclusive_pedestrian_option_is_not_supported_on_highway_topologies(
        bool hasHighwayLane,
        bool expected)
    {
        bool actual = PredefinedPatternsProcessor.IsExclusivePedestrianOptionSupported(hasHighwayLane);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Clear_extra_options_removes_only_option_flags()
    {
        const CustomTrafficLights.Patterns pattern =
            CustomTrafficLights.Patterns.SplitPhasingProtectedLeft |
            CustomTrafficLights.Patterns.ExclusivePedestrian |
            CustomTrafficLights.Patterns.AlwaysGreenKerbsideTurn |
            CustomTrafficLights.Patterns.CentreTurnGiveWay |
            CustomTrafficLights.Patterns.SmartPhaseSelection;

        CustomTrafficLights.Patterns actual = PredefinedPatternsProcessor.ClearExtraOptions(pattern);

        Assert.Equal(
            CustomTrafficLights.Patterns.SplitPhasingProtectedLeft |
            CustomTrafficLights.Patterns.SmartPhaseSelection,
            actual);
    }

    [Theory]
    [InlineData(4, true, true)]
    [InlineData(3, false, false)]
    [InlineData(8, true, false)]
    public void Protected_turn_topology_requires_four_straight_ways(
        int edgeCount,
        bool includeStraightTrafficOnEveryEdge,
        bool expected)
    {
        int straightWays = includeStraightTrafficOnEveryEdge ? edgeCount : 0;

        bool actual = PredefinedPatternsProcessor.IsProtectedCentreTurnTopology(
            edgeCount,
            straightWays,
            hasTurningTrackLane: false);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Protected_turn_topology_rejects_turning_track_lanes()
    {
        bool actual = PredefinedPatternsProcessor.IsProtectedCentreTurnTopology(
            edgeCount: 4,
            straightWays: 4,
            hasTurningTrackLane: true);

        Assert.False(actual);
    }

    [Theory]
    [InlineData(true, true, true)]    // gate open and road vehicle lanes present -> visible
    [InlineData(true, false, false)]  // tram-only junction: gate open but no car lanes -> hidden
    [InlineData(false, true, false)]  // gate closed -> hidden regardless of car lanes
    [InlineData(false, false, false)]
    public void Vehicle_turn_options_require_road_vehicle_lanes(
        bool extraOptionsVisible,
        bool hasCarLane,
        bool expected)
    {
        bool actual = PredefinedPatternsProcessor.IsVehicleTurnOptionVisible(extraOptionsVisible, hasCarLane);

        Assert.Equal(expected, actual);
    }
}
