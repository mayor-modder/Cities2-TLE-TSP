using TrafficLightsEnhancement.Logic.Tsp;
using Xunit;

namespace TrafficLightsEnhancement.Tests.Tsp;

public class GroupedTspPropagationTests
{
    [Fact]
    public void Grouped_intersection_is_runtime_eligible_when_tsp_is_enabled()
    {
        var availability = TspPolicy.GetAvailability(
            settings: new TransitSignalPrioritySettings { m_Enabled = true },
            isGroupedIntersection: true);

        Assert.True(availability.IsRuntimeEligible);
        Assert.Equal(TspAvailabilityReason.None, availability.Reason);
    }

    [Fact]
    public void Build_assignments_propagate_ahead_only_within_the_cumulative_distance_window()
    {
        var members = new[]
        {
            new GroupedTspMember(memberIndex: 0, distanceFromPrevious: 0f),
            new GroupedTspMember(memberIndex: 1, distanceFromPrevious: 40f),
            new GroupedTspMember(memberIndex: 2, distanceFromPrevious: 30f),
            new GroupedTspMember(memberIndex: 3, distanceFromPrevious: 50f),
        };

        var candidates = new[]
        {
            new GroupedTspCandidate(
                originMemberIndex: 0,
                targetSignalGroup: 7,
                source: TspSource.Track,
                strength: 1f,
                expiryTimer: 12,
                extendCurrentPhase: true),
        };

        var assignments = GroupedTspPropagation.BuildAssignments(members, candidates, maxPropagationDistance: 70f);

        Assert.Collection(assignments,
            assignment =>
            {
                Assert.Equal(1, assignment.MemberIndex);
                Assert.Equal(0, assignment.OriginMemberIndex);
                Assert.Equal(40f, assignment.DistanceFromOrigin);
            },
            assignment =>
            {
                Assert.Equal(2, assignment.MemberIndex);
                Assert.Equal(0, assignment.OriginMemberIndex);
                Assert.Equal(70f, assignment.DistanceFromOrigin);
            });
    }

    [Fact]
    public void Build_assignments_never_reach_upstream_members()
    {
        var members = new[]
        {
            new GroupedTspMember(memberIndex: 0, distanceFromPrevious: 0f),
            new GroupedTspMember(memberIndex: 1, distanceFromPrevious: 20f),
            new GroupedTspMember(memberIndex: 2, distanceFromPrevious: 25f),
            new GroupedTspMember(memberIndex: 3, distanceFromPrevious: 25f),
        };

        var candidates = new[]
        {
            new GroupedTspCandidate(
                originMemberIndex: 2,
                targetSignalGroup: 3,
                source: TspSource.PublicCar,
                strength: 1f,
                expiryTimer: 8,
                extendCurrentPhase: false),
        };

        var assignments = GroupedTspPropagation.BuildAssignments(members, candidates, maxPropagationDistance: 100f);

        Assert.DoesNotContain(assignments, assignment => assignment.MemberIndex < 2);
        Assert.Contains(assignments, assignment => assignment.MemberIndex == 3);
    }

    [Fact]
    public void Build_assignments_choose_the_strongest_request_for_an_overlapping_target_member()
    {
        var members = new[]
        {
            new GroupedTspMember(memberIndex: 0, distanceFromPrevious: 0f),
            new GroupedTspMember(memberIndex: 1, distanceFromPrevious: 50f),
            new GroupedTspMember(memberIndex: 2, distanceFromPrevious: 50f),
        };

        var candidates = new[]
        {
            new GroupedTspCandidate(
                originMemberIndex: 0,
                targetSignalGroup: 3,
                source: TspSource.Track,
                strength: 0.5f,
                expiryTimer: 10,
                extendCurrentPhase: false),
            new GroupedTspCandidate(
                originMemberIndex: 1,
                targetSignalGroup: 3,
                source: TspSource.PublicCar,
                strength: 1f,
                expiryTimer: 10,
                extendCurrentPhase: true),
        };

        var assignments = GroupedTspPropagation.BuildAssignments(members, candidates, maxPropagationDistance: 100f);

        var targetAssignment = Assert.Single(assignments, assignment => assignment.MemberIndex == 2);

        Assert.Equal(1, targetAssignment.OriginMemberIndex);
        Assert.Equal(TspSource.PublicCar, targetAssignment.Source);
        Assert.Equal(1f, targetAssignment.Strength);
        Assert.True(targetAssignment.ExtendCurrentPhase);
    }

    [Fact]
    public void Build_assignments_use_distance_before_origin_index_when_strengths_are_equal()
    {
        var members = new[]
        {
            new GroupedTspMember(memberIndex: 0, distanceFromPrevious: 0f),
            new GroupedTspMember(memberIndex: 1, distanceFromPrevious: 30f),
            new GroupedTspMember(memberIndex: 2, distanceFromPrevious: 30f),
        };

        var candidates = new[]
        {
            new GroupedTspCandidate(
                originMemberIndex: 0,
                targetSignalGroup: 4,
                source: TspSource.Track,
                strength: 1f,
                expiryTimer: 10,
                extendCurrentPhase: false),
            new GroupedTspCandidate(
                originMemberIndex: 1,
                targetSignalGroup: 4,
                source: TspSource.Track,
                strength: 1f,
                expiryTimer: 10,
                extendCurrentPhase: false),
        };

        var assignments = GroupedTspPropagation.BuildAssignments(members, candidates, maxPropagationDistance: 100f);

        var targetAssignment = Assert.Single(assignments, assignment => assignment.MemberIndex == 2);

        Assert.Equal(1, targetAssignment.OriginMemberIndex);
        Assert.Equal(30f, targetAssignment.DistanceFromOrigin);
    }

    [Fact]
    public void Build_assignments_use_lower_origin_index_when_strength_and_distance_are_equal()
    {
        var members = new[]
        {
            new GroupedTspMember(memberIndex: 0, distanceFromPrevious: 0f),
            new GroupedTspMember(memberIndex: 1, distanceFromPrevious: 0f),
            new GroupedTspMember(memberIndex: 2, distanceFromPrevious: 0f),
        };

        var candidates = new[]
        {
            new GroupedTspCandidate(
                originMemberIndex: 0,
                targetSignalGroup: 4,
                source: TspSource.Track,
                strength: 1f,
                expiryTimer: 10,
                extendCurrentPhase: false),
            new GroupedTspCandidate(
                originMemberIndex: 1,
                targetSignalGroup: 4,
                source: TspSource.PublicCar,
                strength: 1f,
                expiryTimer: 10,
                extendCurrentPhase: true),
        };

        var assignments = GroupedTspPropagation.BuildAssignments(members, candidates, maxPropagationDistance: 100f);

        var targetAssignment = Assert.Single(assignments, assignment => assignment.MemberIndex == 2);

        Assert.Equal(0, targetAssignment.OriginMemberIndex);
    }
}
