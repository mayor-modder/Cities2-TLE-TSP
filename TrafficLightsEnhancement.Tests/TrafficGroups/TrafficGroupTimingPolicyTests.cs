using TrafficLightsEnhancement.Logic.TrafficGroups;
using Xunit;

namespace TrafficLightsEnhancement.Tests.TrafficGroups;

public class TrafficGroupTimingPolicyTests
{
    [Theory]
    [InlineData(1, 4, 1)]
    [InlineData(4, 4, 4)]
    [InlineData(5, 4, 1)]
    [InlineData(8, 4, 4)]
    [InlineData(0, 4, 1)]
    [InlineData(2, 0, 1)]
    public void One_based_phase_wraps_into_valid_group_range(int phase, int phaseCount, int expected)
    {
        Assert.Equal(expected, TrafficGroupTimingPolicy.WrapOneBasedPhase(phase, phaseCount));
    }

    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(3, 4, 3)]
    [InlineData(4, 4, 0)]
    [InlineData(-1, 4, 3)]
    [InlineData(-2, 4, 2)]
    [InlineData(2, 0, 0)]
    public void Zero_based_phase_wraps_negative_offsets_into_valid_index_range(int phase, int phaseCount, int expected)
    {
        Assert.Equal(expected, TrafficGroupTimingPolicy.WrapZeroBasedPhase(phase, phaseCount));
    }

    [Theory]
    [InlineData(10f, 12f, 30f, 28f)]
    [InlineData(65f, 5f, 60f, 0f)]
    [InlineData(5f, 0f, 0f, 0f)]
    public void Cycle_position_wraps_negative_and_overflowing_offsets(float timer, float offset, float length, float expected)
    {
        Assert.Equal(expected, TrafficGroupTimingPolicy.WrapCyclePosition(timer, offset, length));
    }

    [Theory]
    [InlineData(0f, 60f, 4, 0)]
    [InlineData(15f, 60f, 4, 1)]
    [InlineData(59.9f, 60f, 4, 3)]
    [InlineData(75f, 60f, 4, 1)]
    [InlineData(-30f, 60f, 4, -2)]
    [InlineData(10f, 0f, 4, 0)]
    public void Phase_offset_is_zero_based_and_preserves_csharp_remainder_semantics(float arrivalTime, float cycleLength, int phaseCount, int expected)
    {
        Assert.Equal(expected, TrafficGroupTimingPolicy.CalculateZeroBasedPhaseOffset(arrivalTime, cycleLength, phaseCount));
    }

    [Theory]
    [InlineData(0f, 60f, 4, 1)]
    [InlineData(14.9f, 60f, 4, 1)]
    [InlineData(15f, 60f, 4, 2)]
    [InlineData(45f, 60f, 4, 4)]
    [InlineData(60f, 60f, 4, 4)]
    [InlineData(20f, 60f, 0, 1)]
    public void Even_cycle_phase_selection_returns_one_based_group(float cyclePosition, float cycleLength, int groupCount, int expected)
    {
        Assert.Equal(expected, TrafficGroupTimingPolicy.DetermineOneBasedPhaseFromEvenCycle(cyclePosition, cycleLength, groupCount));
    }

}
