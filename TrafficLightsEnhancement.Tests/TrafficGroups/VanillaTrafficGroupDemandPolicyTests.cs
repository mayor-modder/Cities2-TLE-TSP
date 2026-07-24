using TrafficLightsEnhancement.Logic.TrafficGroups;
using Xunit;

namespace TrafficLightsEnhancement.Tests.TrafficGroups;

public sealed class VanillaTrafficGroupDemandPolicyTests
{
    [Fact]
    public void Higher_priority_member_preserves_lower_priority_member_winner()
    {
        var leader = new VanillaTrafficGroupDemand(1, 0b0001, 0b0001, 0);
        var follower = new VanillaTrafficGroupDemand(5, 0b0100, 0, 0);

        VanillaTrafficGroupDemand merged = VanillaTrafficGroupDemandPolicy.Merge(leader, follower);

        Assert.Equal(5, merged.HighestPriority);
        Assert.Equal(0b0101, merged.RequestedPhaseMask);
        Assert.Equal(0b0001, merged.ExtendablePhaseMask);
    }

    [Fact]
    public void Equal_priority_members_combine_requested_and_extendable_masks()
    {
        var leader = new VanillaTrafficGroupDemand(3, 0b0001, 0b0001, 0);
        var follower = new VanillaTrafficGroupDemand(3, 0b0100, 0b0100, 0b0010);

        VanillaTrafficGroupDemand merged = VanillaTrafficGroupDemandPolicy.Merge(leader, follower);

        Assert.Equal(3, merged.HighestPriority);
        Assert.Equal(0b0101, merged.RequestedPhaseMask);
        Assert.Equal(0b0101, merged.ExtendablePhaseMask);
        Assert.Equal(0b0010, merged.SuppressedPhaseMask);
    }

    [Fact]
    public void Remap_uses_inverse_physical_movement_map()
    {
        TrafficGroupPhaseMap phaseMap = BuildSwappedTwoPhaseMap();
        var member = new VanillaTrafficGroupDemand(2, 0b0001, 0b0010, 0b0001);

        bool valid = VanillaTrafficGroupDemandPolicy.TryRemapMemberToLeader(
            member,
            phaseMap,
            out var remapped);

        Assert.True(valid);
        Assert.Equal(0b0010, remapped.RequestedPhaseMask);
        Assert.Equal(0b0001, remapped.ExtendablePhaseMask);
        Assert.Equal(0b0010, remapped.SuppressedPhaseMask);
    }

    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(4, 3, 1)]
    public void Optional_phase_preserves_zero_and_wraps_nonzero(int phase, int phaseCount, int expected)
    {
        Assert.Equal(expected, VanillaTrafficGroupDemandPolicy.MapOptionalOneBasedPhase(phase, phaseCount));
    }

    [Fact]
    public void Incomplete_phase_map_rejects_aggregation()
    {
        bool valid = VanillaTrafficGroupDemandPolicy.TryRemapMemberToLeader(
            new VanillaTrafficGroupDemand(1, 1, 1, 0),
            default,
            out _);

        Assert.False(valid);
    }

    [Fact]
    public void No_positive_priority_honors_suppressed_masks()
    {
        var demand = new VanillaTrafficGroupDemand(0, 0b0011, 0, 0b0010);

        int next = VanillaTrafficGroupDemandPolicy.SelectNextPhase(
            demand,
            currentPhase: 1,
            phaseCount: 2,
            preferChange: true,
            out bool canExtend);

        Assert.Equal(1, next);
        Assert.False(canExtend);
    }

    [Theory]
    [InlineData(1, 4, 1)]
    [InlineData(5, 4, 1)]
    [InlineData(31, 31, 31)]
    public void Required_phase_uses_one_based_wrapping(int phase, int phaseCount, int expected)
    {
        Assert.Equal(expected, VanillaTrafficGroupDemandPolicy.MapRequiredOneBasedPhase(phase, phaseCount));
    }

    [Fact]
    public void Selection_wraps_to_an_earlier_requested_phase()
    {
        var demand = new VanillaTrafficGroupDemand(2, 0b0010, 0, 0);

        int next = VanillaTrafficGroupDemandPolicy.SelectNextPhase(
            demand,
            currentPhase: 3,
            phaseCount: 4,
            preferChange: true,
            out bool canExtend);

        Assert.Equal(2, next);
        Assert.False(canExtend);
    }

    [Theory]
    [InlineData(0b0100, true)]
    [InlineData(0b0010, false)]
    public void Current_phase_extends_only_when_winning_mask_allows_it(int extendableMask, bool expected)
    {
        var demand = new VanillaTrafficGroupDemand(2, 0b0100, extendableMask, 0);

        int next = VanillaTrafficGroupDemandPolicy.SelectNextPhase(
            demand,
            currentPhase: 3,
            phaseCount: 4,
            preferChange: true,
            out bool canExtend);

        Assert.Equal(3, next);
        Assert.Equal(expected, canExtend);
    }

    private static TrafficGroupPhaseMap BuildSwappedTwoPhaseMap()
    {
        var leader = new[]
        {
            new TrafficGroupPhaseSignature(1, 0b0001, 0),
            new TrafficGroupPhaseSignature(2, 0b0010, 0),
        };
        var member = new[]
        {
            new TrafficGroupPhaseSignature(1, 0b0010, 0),
            new TrafficGroupPhaseSignature(2, 0b0001, 0),
        };

        Assert.True(TrafficGroupMovementMappingPolicy.TryBuild(leader, member, out var phaseMap));
        return phaseMap;
    }
}
