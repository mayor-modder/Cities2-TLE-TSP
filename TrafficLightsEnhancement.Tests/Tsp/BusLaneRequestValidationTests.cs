using TrafficLightsEnhancement.Logic.Tsp;
using Xunit;

namespace TrafficLightsEnhancement.Tests.Tsp;

public class BusLaneRequestValidationTests
{
    [Fact]
    public void Disabled_tsp_blocks_validated_bus_requests()
    {
        bool built = TransitSignalPriorityRuntime.TryBuildRequestForLane(
            settings: new TransitSignalPrioritySettings(),
            isTrackLane: false,
            isPublicCarLane: true,
            hasValidatedBusOccupant: true,
            out _);

        Assert.False(built);
    }

    [Fact]
    public void Public_only_lane_without_validated_bus_does_not_build_a_tsp_request()
    {
        bool built = TransitSignalPriorityRuntime.TryBuildRequestForLane(
            settings: new TransitSignalPrioritySettings { m_Enabled = true, m_AllowPublicCarRequests = true },
            isTrackLane: false,
            isPublicCarLane: true,
            hasValidatedBusOccupant: false,
            out var request);

        Assert.False(built);
        Assert.Equal(default, request);
    }

    [Fact]
    public void Public_only_lane_with_validated_bus_builds_a_public_car_request()
    {
        bool built = TransitSignalPriorityRuntime.TryBuildRequestForLane(
            settings: new TransitSignalPrioritySettings { m_Enabled = true, m_AllowPublicCarRequests = true },
            isTrackLane: false,
            isPublicCarLane: true,
            hasValidatedBusOccupant: true,
            out var request);

        Assert.True(built);
        Assert.Equal(TspSource.PublicCar, request.Source);
    }
}
