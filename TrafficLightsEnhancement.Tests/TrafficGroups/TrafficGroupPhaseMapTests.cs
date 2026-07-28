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
    public void Movement_destinations_disambiguate_phases_with_the_same_approaches()
    {
        var leader = new[]
        {
            Phase(
                1,
                roadAxes: 0b0011,
                roadMovements: Movements((0, 0), (0, 1))),
            Phase(
                2,
                roadAxes: 0b0011,
                roadMovements: Movements((1, 1), (1, 0))),
        };
        var member = new[]
        {
            Phase(
                1,
                roadAxes: 0b0011,
                roadMovements: Movements((1, 1), (1, 0))),
            Phase(
                2,
                roadAxes: 0b0011,
                roadMovements: Movements((0, 0), (0, 1))),
        };

        Assert.True(TrafficGroupMovementMappingPolicy.TryBuild(leader, member, out var phaseMap));
        Assert.Equal(2, MapLeader(phaseMap, 1));
        Assert.Equal(1, MapLeader(phaseMap, 2));
    }

    [Fact]
    public void Yield_assignments_disambiguate_phases_with_the_same_active_movements()
    {
        TrafficGroupMovementMask activeMovements = Movements((0, 0), (0, 1));
        TrafficGroupMovementMask yieldingTurn = Movements((0, 1));
        var leader = new[]
        {
            Phase(
                1,
                roadAxes: 0b0001,
                roadMovements: activeMovements,
                roadYieldMovements: yieldingTurn),
            Phase(
                2,
                roadAxes: 0b0001,
                roadMovements: activeMovements),
        };
        var member = new[]
        {
            Phase(
                1,
                roadAxes: 0b0001,
                roadMovements: activeMovements),
            Phase(
                2,
                roadAxes: 0b0001,
                roadMovements: activeMovements,
                roadYieldMovements: yieldingTurn),
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
    public void Detailed_failure_identifies_ambiguous_exact_match()
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

        Assert.False(
            TrafficGroupMovementMappingPolicy.TryBuild(
                leader,
                member,
                out _,
                out TrafficGroupMovementMappingFailure failure));
        Assert.Equal(
            TrafficGroupMovementMappingFailureReason.AmbiguousExactMatch,
            failure.Reason);
        Assert.Equal(1, failure.LeaderPhase);
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
    public void Detailed_failure_identifies_leader_phase_without_an_approach()
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

        Assert.False(
            TrafficGroupMovementMappingPolicy.TryBuild(
                leader,
                member,
                out _,
                out TrafficGroupMovementMappingFailure failure));
        Assert.Equal(
            TrafficGroupMovementMappingFailureReason.LeaderPhaseHasNoApproach,
            failure.Reason);
        Assert.Equal(2, failure.LeaderPhase);
    }

    [Fact]
    public void Detailed_failure_identifies_member_phase_without_an_approach()
    {
        var leader = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0010),
        };
        var member = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0),
        };

        Assert.False(
            TrafficGroupMovementMappingPolicy.TryBuild(
                leader,
                member,
                out _,
                out TrafficGroupMovementMappingFailure failure));
        Assert.Equal(
            TrafficGroupMovementMappingFailureReason.MemberPhaseHasNoApproach,
            failure.Reason);
        Assert.Equal(2, failure.MemberPhase);
    }

    [Fact]
    public void Identity_mapping_preserves_custom_phase_numbers_without_physical_overlap()
    {
        var leader = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0010),
        };
        var member = new[]
        {
            Phase(1, roadAxes: 0b0100),
            Phase(2, roadAxes: 0b1000),
        };

        Assert.True(
            TrafficGroupMovementMappingPolicy.TryBuildIdentity(
                leader,
                member,
                out TrafficGroupPhaseMap phaseMap,
                out _));
        Assert.Equal(1, MapLeader(phaseMap, 1));
        Assert.Equal(2, MapLeader(phaseMap, 2));
    }

    [Fact]
    public void Identity_mapping_accepts_duplicate_physical_signatures()
    {
        var duplicatePhases = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0001),
        };

        Assert.True(
            TrafficGroupMovementMappingPolicy.TryBuildIdentity(
                duplicatePhases,
                duplicatePhases,
                out TrafficGroupPhaseMap phaseMap,
                out _));
        Assert.Equal(1, MapLeader(phaseMap, 1));
        Assert.Equal(2, MapLeader(phaseMap, 2));
    }

    [Fact]
    public void Identity_mapping_rejects_an_empty_local_phase()
    {
        var leader = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0010),
        };
        var member = new[]
        {
            Phase(1, roadAxes: 0b0100),
            Phase(2, roadAxes: 0),
        };

        Assert.False(
            TrafficGroupMovementMappingPolicy.TryBuildIdentity(
                leader,
                member,
                out _,
                out TrafficGroupMovementMappingFailure failure));
        Assert.Equal(
            TrafficGroupMovementMappingFailureReason.MemberPhaseHasNoApproach,
            failure.Reason);
        Assert.Equal(2, failure.MemberPhase);
    }

    [Fact]
    public void Identity_mapping_rejects_a_member_with_fewer_phases()
    {
        var leader = new[]
        {
            Phase(1, roadAxes: 0b0001),
            Phase(2, roadAxes: 0b0010),
        };
        var member = new[]
        {
            Phase(1, roadAxes: 0b0100),
        };

        Assert.False(
            TrafficGroupMovementMappingPolicy.TryBuildIdentity(
                leader,
                member,
                out _,
                out TrafficGroupMovementMappingFailure failure));
        Assert.Equal(
            TrafficGroupMovementMappingFailureReason.MemberHasFewerPhases,
            failure.Reason);
    }

    [Fact]
    public void Phase_signature_diagnostic_includes_all_mapping_inputs()
    {
        var signature = Phase(
            2,
            roadAxes: 0x12,
            trackAxes: 0x34,
            roadMovements: Movements((0, 1)),
            trackMovements: Movements((2, 3)),
            roadYieldMovements: Movements((4, 5)),
            trackYieldMovements: Movements((6, 7)));

        string diagnostic = signature.ToDiagnosticString();

        Assert.Contains("group=2", diagnostic);
        Assert.Contains("roadAxes=0000000000000012", diagnostic);
        Assert.Contains("trackAxes=0000000000000034", diagnostic);
        Assert.Contains("roadMovements=", diagnostic);
        Assert.Contains("trackMovements=", diagnostic);
        Assert.Contains("roadYield=", diagnostic);
        Assert.Contains("trackYield=", diagnostic);
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
        ulong trackAxes = 0,
        TrafficGroupMovementMask roadMovements = default,
        TrafficGroupMovementMask trackMovements = default,
        TrafficGroupMovementMask roadYieldMovements = default,
        TrafficGroupMovementMask trackYieldMovements = default)
    {
        return new TrafficGroupPhaseSignature(
            signalGroup,
            roadAxes,
            trackAxes,
            roadMovements,
            trackMovements,
            roadYieldMovements,
            trackYieldMovements);
    }

    private static TrafficGroupMovementMask Movements(
        params (int SourceAxis, int DestinationAxis)[] movements)
    {
        TrafficGroupMovementMask mask = default;
        foreach ((int sourceAxis, int destinationAxis) in movements)
        {
            mask |= TrafficGroupMovementMask.FromAxisBins(sourceAxis, destinationAxis);
        }

        return mask;
    }

    private static int MapLeader(TrafficGroupPhaseMap phaseMap, int leaderPhase)
    {
        Assert.True(phaseMap.TryMapLeaderToMember(leaderPhase, out int memberPhase));
        return memberPhase;
    }
}
