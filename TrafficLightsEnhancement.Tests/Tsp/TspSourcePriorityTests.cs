using TrafficLightsEnhancement.Logic.Tsp;
using Xunit;

namespace TrafficLightsEnhancement.Tests.Tsp;

public class TspSourcePriorityTests
{
    [Theory]
    [InlineData(TspSource.Track, 2)]
    [InlineData(TspSource.PublicCar, 1)]
    [InlineData(TspSource.None, 0)]
    public void Get_priority_orders_transit_sources(TspSource source, int expected)
    {
        Assert.Equal(expected, TspSourcePriority.GetPriority(source));
    }

    [Fact]
    public void Track_request_outranks_stronger_public_car_request()
    {
        var track = new TspRequest(TspSource.Track, strength: 0.5f, extensionEligible: false);
        var bus = new TspRequest(TspSource.PublicCar, strength: 1f, extensionEligible: false);

        Assert.True(TspSourcePriority.IsPreferredRequest(track, bus));
        Assert.False(TspSourcePriority.IsPreferredRequest(bus, track));
    }

    [Fact]
    public void Track_tie_outranks_public_car_tie()
    {
        var track = new TspRequest(TspSource.Track, strength: 1f, extensionEligible: false);
        var bus = new TspRequest(TspSource.PublicCar, strength: 1f, extensionEligible: false);

        Assert.True(TspSourcePriority.IsPreferredRequest(track, bus));
        Assert.False(TspSourcePriority.IsPreferredRequest(bus, track));
    }

    [Fact]
    public void Dedicated_lane_bus_outranks_tied_mixed_lane_bus()
    {
        var dedicated = new TspRequest(TspSource.PublicCar, strength: 1f, extensionEligible: true, onDedicatedLane: true);
        var mixed = new TspRequest(TspSource.PublicCar, strength: 1f, extensionEligible: true, onDedicatedLane: false);

        Assert.True(TspSourcePriority.IsPreferredRequest(dedicated, mixed));
        Assert.False(TspSourcePriority.IsPreferredRequest(mixed, dedicated));
    }

    [Fact]
    public void Dedicated_lane_tiebreak_does_not_disturb_equal_bus_requests()
    {
        var mixedA = new TspRequest(TspSource.PublicCar, strength: 1f, extensionEligible: true, onDedicatedLane: false);
        var mixedB = new TspRequest(TspSource.PublicCar, strength: 1f, extensionEligible: true, onDedicatedLane: false);
        Assert.False(TspSourcePriority.IsPreferredRequest(mixedA, mixedB));

        var dedicatedA = new TspRequest(TspSource.PublicCar, strength: 1f, extensionEligible: true, onDedicatedLane: true);
        var dedicatedB = new TspRequest(TspSource.PublicCar, strength: 1f, extensionEligible: true, onDedicatedLane: true);
        Assert.False(TspSourcePriority.IsPreferredRequest(dedicatedA, dedicatedB));
    }

    [Fact]
    public void Track_still_outranks_dedicated_lane_bus()
    {
        var track = new TspRequest(TspSource.Track, strength: 1f, extensionEligible: true);
        var dedicatedBus = new TspRequest(TspSource.PublicCar, strength: 1f, extensionEligible: true, onDedicatedLane: true);

        Assert.True(TspSourcePriority.IsPreferredRequest(track, dedicatedBus));
        Assert.False(TspSourcePriority.IsPreferredRequest(dedicatedBus, track));
    }

    [Fact]
    public void Stronger_mixed_lane_bus_still_outranks_weaker_dedicated_bus()
    {
        var strongerMixed = new TspRequest(TspSource.PublicCar, strength: 1f, extensionEligible: true, onDedicatedLane: false);
        var weakerDedicated = new TspRequest(TspSource.PublicCar, strength: 0.5f, extensionEligible: true, onDedicatedLane: true);

        Assert.True(TspSourcePriority.IsPreferredRequest(strongerMixed, weakerDedicated));
        Assert.False(TspSourcePriority.IsPreferredRequest(weakerDedicated, strongerMixed));
    }
}
