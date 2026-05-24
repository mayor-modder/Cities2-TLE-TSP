using TrafficLightsEnhancement.Logic.Tsp;
using Xunit;

namespace TrafficLightsEnhancement.Tests.Tsp;

public class BusPrioritySuppressionPolicyTests
{
    [Theory]
    [InlineData(0.49f, false)]
    [InlineData(0.5f, false)]
    [InlineData(0.51f, true)]
    public void Is_moving_bus_uses_policy_threshold(float speed, bool expected)
    {
        Assert.Equal(expected, BusPrioritySuppressionPolicy.IsMovingBus(speed));
    }

    [Theory]
    [InlineData(BusStopRelation.None)]
    [InlineData(BusStopRelation.NearSideBeforeSignal)]
    [InlineData(BusStopRelation.FarSideAfterSignal)]
    [InlineData(BusStopRelation.Unknown)]
    public void Boarding_bus_is_suppressed_for_any_stop_relation(BusStopRelation stopRelation)
    {
        BusPrioritySuppressionDecision decision = BusPrioritySuppressionPolicy.EvaluateStopSuppression(
            TransitApproachSuppressionFlags.Boarding,
            stopRelation);

        Assert.True(decision.IsSuppressed);
        Assert.Equal(BusPrioritySuppressionReason.Boarding, decision.Reason);
    }

    [Theory]
    [InlineData(TransitApproachSuppressionFlags.Arriving)]
    [InlineData(TransitApproachSuppressionFlags.RequireStop)]
    [InlineData(TransitApproachSuppressionFlags.Arriving | TransitApproachSuppressionFlags.RequireStop)]
    public void Near_side_stop_arrival_suppresses_bus_priority(TransitApproachSuppressionFlags flags)
    {
        BusPrioritySuppressionDecision decision = BusPrioritySuppressionPolicy.EvaluateStopSuppression(
            flags,
            BusStopRelation.NearSideBeforeSignal);

        Assert.True(decision.IsSuppressed);
        Assert.Equal(BusPrioritySuppressionReason.NearSideStop, decision.Reason);
    }

    [Theory]
    [InlineData(TransitApproachSuppressionFlags.Arriving)]
    [InlineData(TransitApproachSuppressionFlags.RequireStop)]
    [InlineData(TransitApproachSuppressionFlags.Arriving | TransitApproachSuppressionFlags.RequireStop)]
    public void Far_side_stop_arrival_does_not_suppress_bus_priority(TransitApproachSuppressionFlags flags)
    {
        BusPrioritySuppressionDecision decision = BusPrioritySuppressionPolicy.EvaluateStopSuppression(
            flags,
            BusStopRelation.FarSideAfterSignal);

        Assert.False(decision.IsSuppressed);
        Assert.Equal(BusPrioritySuppressionReason.None, decision.Reason);
    }

    [Fact]
    public void Unknown_stop_relation_suppresses_arriving_bus_conservatively()
    {
        BusPrioritySuppressionDecision decision = BusPrioritySuppressionPolicy.EvaluateStopSuppression(
            TransitApproachSuppressionFlags.Arriving,
            BusStopRelation.Unknown,
            isDedicatedBusApproach: false,
            isVehicleMoving: true);

        Assert.True(decision.IsSuppressed);
        Assert.Equal(BusPrioritySuppressionReason.UnknownStopRelation, decision.Reason);
    }

    [Fact]
    public void Unknown_stop_relation_allows_moving_require_stop_bus()
    {
        BusPrioritySuppressionDecision decision = BusPrioritySuppressionPolicy.EvaluateStopSuppression(
            TransitApproachSuppressionFlags.RequireStop,
            BusStopRelation.Unknown,
            isDedicatedBusApproach: false,
            isVehicleMoving: true);

        Assert.False(decision.IsSuppressed);
        Assert.Equal(BusPrioritySuppressionReason.None, decision.Reason);
    }

    [Fact]
    public void Unknown_stop_relation_allows_stopped_require_stop_bus()
    {
        BusPrioritySuppressionDecision decision = BusPrioritySuppressionPolicy.EvaluateStopSuppression(
            TransitApproachSuppressionFlags.RequireStop,
            BusStopRelation.Unknown,
            isDedicatedBusApproach: true,
            isVehicleMoving: false);

        Assert.False(decision.IsSuppressed);
        Assert.Equal(BusPrioritySuppressionReason.None, decision.Reason);
    }

    [Fact]
    public void Unknown_stop_relation_suppresses_stopped_mixed_lane_require_stop_bus()
    {
        BusPrioritySuppressionDecision decision = BusPrioritySuppressionPolicy.EvaluateStopSuppression(
            TransitApproachSuppressionFlags.RequireStop,
            BusStopRelation.Unknown,
            isDedicatedBusApproach: false,
            isVehicleMoving: false);

        Assert.True(decision.IsSuppressed);
        Assert.Equal(BusPrioritySuppressionReason.UnknownStopRelation, decision.Reason);
    }

    [Fact]
    public void Unknown_stop_relation_still_suppresses_arriving_bus_only_sample()
    {
        BusPrioritySuppressionDecision decision = BusPrioritySuppressionPolicy.EvaluateStopSuppression(
            TransitApproachSuppressionFlags.Arriving,
            BusStopRelation.Unknown,
            isDedicatedBusApproach: true,
            isVehicleMoving: true);

        Assert.True(decision.IsSuppressed);
        Assert.Equal(BusPrioritySuppressionReason.UnknownStopRelation, decision.Reason);
    }

    [Fact]
    public void Queued_bus_without_stop_flags_is_not_stop_suppressed()
    {
        BusPrioritySuppressionDecision decision = BusPrioritySuppressionPolicy.EvaluateStopSuppression(
            TransitApproachSuppressionFlags.None,
            BusStopRelation.NearSideBeforeSignal);

        Assert.False(decision.IsSuppressed);
        Assert.Equal(BusPrioritySuppressionReason.None, decision.Reason);
    }
}
