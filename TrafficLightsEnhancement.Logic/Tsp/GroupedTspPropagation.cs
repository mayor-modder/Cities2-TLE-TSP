using System.Collections.Generic;
using System.Linq;

namespace TrafficLightsEnhancement.Logic.Tsp;

public readonly struct GroupedTspMember
{
    public GroupedTspMember(int memberIndex, float distanceFromPrevious)
    {
        MemberIndex = memberIndex;
        DistanceFromPrevious = distanceFromPrevious;
    }

    public int MemberIndex { get; }

    public float DistanceFromPrevious { get; }
}

public readonly struct GroupedTspCandidate
{
    public GroupedTspCandidate(
        int originMemberIndex,
        int targetSignalGroup,
        TspSource source,
        float strength,
        uint expiryTimer,
        bool extendCurrentPhase)
    {
        OriginMemberIndex = originMemberIndex;
        TargetSignalGroup = targetSignalGroup;
        Source = source;
        Strength = strength;
        ExpiryTimer = expiryTimer;
        ExtendCurrentPhase = extendCurrentPhase;
    }

    public int OriginMemberIndex { get; }

    public int TargetSignalGroup { get; }

    public TspSource Source { get; }

    public float Strength { get; }

    public uint ExpiryTimer { get; }

    public bool ExtendCurrentPhase { get; }
}

public readonly struct GroupedTspAssignment
{
    public GroupedTspAssignment(
        int memberIndex,
        int originMemberIndex,
        int targetSignalGroup,
        TspSource source,
        float strength,
        uint expiryTimer,
        bool extendCurrentPhase,
        float distanceFromOrigin)
    {
        MemberIndex = memberIndex;
        OriginMemberIndex = originMemberIndex;
        TargetSignalGroup = targetSignalGroup;
        Source = source;
        Strength = strength;
        ExpiryTimer = expiryTimer;
        ExtendCurrentPhase = extendCurrentPhase;
        DistanceFromOrigin = distanceFromOrigin;
    }

    public int MemberIndex { get; }

    public int OriginMemberIndex { get; }

    public int TargetSignalGroup { get; }

    public TspSource Source { get; }

    public float Strength { get; }

    public uint ExpiryTimer { get; }

    public bool ExtendCurrentPhase { get; }

    public float DistanceFromOrigin { get; }
}

public static class GroupedTspPropagation
{
    public static IReadOnlyList<GroupedTspAssignment> BuildAssignments(
        IReadOnlyList<GroupedTspMember> members,
        IReadOnlyList<GroupedTspCandidate> candidates,
        float maxPropagationDistance)
    {
        if (members.Count == 0 || candidates.Count == 0)
        {
            return new List<GroupedTspAssignment>();
        }

        var orderedMembers = members.OrderBy(member => member.MemberIndex).ToArray();
        var assignmentsByMember = new Dictionary<int, GroupedTspAssignment>();

        foreach (var candidate in candidates)
        {
            int originPosition = -1;
            for (int i = 0; i < orderedMembers.Length; i++)
            {
                if (orderedMembers[i].MemberIndex == candidate.OriginMemberIndex)
                {
                    originPosition = i;
                    break;
                }
            }

            if (originPosition < 0)
            {
                continue;
            }

            float cumulativeDistance = 0f;
            for (int i = originPosition + 1; i < orderedMembers.Length; i++)
            {
                cumulativeDistance += orderedMembers[i].DistanceFromPrevious;
                if (cumulativeDistance > maxPropagationDistance)
                {
                    break;
                }

                var assignment = new GroupedTspAssignment(
                    memberIndex: orderedMembers[i].MemberIndex,
                    originMemberIndex: candidate.OriginMemberIndex,
                    targetSignalGroup: candidate.TargetSignalGroup,
                    source: candidate.Source,
                    strength: candidate.Strength,
                    expiryTimer: candidate.ExpiryTimer,
                    extendCurrentPhase: candidate.ExtendCurrentPhase,
                    distanceFromOrigin: cumulativeDistance);

                if (assignmentsByMember.TryGetValue(assignment.MemberIndex, out var existing))
                {
                    if (CompareRequests(assignment, existing) < 0)
                    {
                        continue;
                    }
                }

                assignmentsByMember[assignment.MemberIndex] = assignment;
            }
        }

        return assignmentsByMember.Values.OrderBy(assignment => assignment.MemberIndex).ToArray();
    }

    private static int CompareRequests(GroupedTspAssignment left, GroupedTspAssignment right)
    {
        int strengthComparison = left.Strength.CompareTo(right.Strength);
        if (strengthComparison != 0)
        {
            return strengthComparison;
        }

        int distanceComparison = right.DistanceFromOrigin.CompareTo(left.DistanceFromOrigin);
        if (distanceComparison != 0)
        {
            return distanceComparison;
        }

        return right.OriginMemberIndex.CompareTo(left.OriginMemberIndex);
    }
}
