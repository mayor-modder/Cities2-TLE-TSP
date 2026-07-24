using TrafficLightsEnhancement.Logic.TrafficGroups;
using Xunit;

namespace TrafficLightsEnhancement.Tests.TrafficGroups;

public sealed class TrafficGroupPhaseMapTests
{
    [Fact]
    public void Aligned_two_phase_signatures_keep_identity_mapping()
    {
        var leader = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0010),
        };
        var member = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0010),
        };

        bool mapped = TrafficGroupMovementMappingPolicy.TryBuild(leader, member, out var phaseMap);

        Assert.True(mapped);
        Assert.True(phaseMap.IsComplete);
        Assert.Equal(1, MapLeader(phaseMap, 1));
        Assert.Equal(2, MapLeader(phaseMap, 2));
    }

    [Fact]
    public void Swapped_two_phase_signatures_map_equivalent_movements()
    {
        var leader = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0010),
        };
        var member = new[]
        {
            Phase(1, roadAxes: 0b0010),
            Phase(2, roadAxes: 0b0001),
        };

        bool mapped = TrafficGroupMovementMappingPolicy.TryBuild(leader, member, out var phaseMap);

        Assert.True(mapped);
        Assert.Equal(2, MapLeader(phaseMap, 1));
        Assert.Equal(1, MapLeader(phaseMap, 2));
    }

    [Fact]
    public void Inverse_lookup_returns_leader_phase_for_member_phase()
    {
        var leader = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0010),
        };
        var member = new[]
        {
            Phase(1, roadAxes: 0b0010),
            Phase(2, roadAxes: 0b0001),
        };
        Assert.True(TrafficGroupMovementMappingPolicy.TryBuild(leader, member, out var phaseMap));

        Assert.True(phaseMap.TryMapMemberToLeader(1, out int leaderPhase));
        Assert.Equal(2, leaderPhase);
        Assert.True(phaseMap.TryMapMemberToLeader(2, out leaderPhase));
        Assert.Equal(1, leaderPhase);
    }

    [Fact]
    public void Opposite_vectors_share_an_undirected_axis_bin()
    {
        int eastbound = TrafficGroupMovementMappingPolicy.QuantizeUndirectedAxis(1, 0);
        int westbound = TrafficGroupMovementMappingPolicy.QuantizeUndirectedAxis(-1, 0);
        int diagonal = TrafficGroupMovementMappingPolicy.QuantizeUndirectedAxis(1, 1);
        int oppositeDiagonal = TrafficGroupMovementMappingPolicy.QuantizeUndirectedAxis(-1, -1);

        Assert.Equal(eastbound, westbound);
        Assert.Equal(diagonal, oppositeDiagonal);
    }

    [Fact]
    public void Road_and_track_axes_are_matched_separately()
    {
        var leader = new[]
        {
            Phase(1, roadAxes: 0b0001, trackAxes: 0b0100),
            Phase(2, roadAxes: 0b0010, trackAxes: 0b1000),
        };
        var member = new[]
        {
            Phase(1, roadAxes: 0b0010, trackAxes: 0b1000),
            Phase(2, roadAxes: 0b0001, trackAxes: 0b0100),
        };

        Assert.True(TrafficGroupMovementMappingPolicy.TryBuild(leader, member, out var phaseMap));
        Assert.Equal(2, MapLeader(phaseMap, 1));
        Assert.Equal(1, MapLeader(phaseMap, 2));
    }

    [Fact]
    public void Ambiguous_candidates_reject_the_entire_map()
    {
        var leader = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0010),
        };
        var member = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0001),
        };

        Assert.False(TrafficGroupMovementMappingPolicy.TryBuild(leader, member, out var phaseMap));
        Assert.False(phaseMap.IsComplete);
    }

    [Fact]
    public void Empty_or_incomplete_signatures_reject_the_entire_map()
    {
        var leader = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0),
        };
        var member = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0010),
        };

        Assert.False(TrafficGroupMovementMappingPolicy.TryBuild(leader, member, out var phaseMap));
        Assert.False(phaseMap.IsComplete);
    }

    [Fact]
    public void Reusing_one_member_phase_rejects_the_entire_map()
    {
        var leader = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0001 | 0b0010),
        };
        var member = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0100),
        };

        Assert.False(TrafficGroupMovementMappingPolicy.TryBuild(leader, member, out _));
    }

    private static TrafficGroupPhaseSignature Phase(
        int signalGroup,
        ulong roadAxes,
        ulong trackAxes = 0)
    {
        return new TrafficGroupPhaseSignature(signalGroup, roadAxes, trackAxes);
    }

    private static int MapLeader(TrafficGroupPhaseMap phaseMap, int leaderPhase)
    {
        Assert.True(phaseMap.TryMapLeaderToMember(leaderPhase, out int memberPhase));
        return memberPhase;
    }
}
