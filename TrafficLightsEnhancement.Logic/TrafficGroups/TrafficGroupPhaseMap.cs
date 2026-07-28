using System;
using System.Collections.Generic;

namespace TrafficLightsEnhancement.Logic.TrafficGroups;

public readonly struct TrafficGroupMovementMask : IEquatable<TrafficGroupMovementMask>
{
    private const int AxisBinCount = TrafficGroupMovementMappingPolicy.AxisBinCount;
    private readonly ulong _first;
    private readonly ulong _second;
    private readonly ulong _third;
    private readonly ulong _fourth;

    private TrafficGroupMovementMask(
        ulong first,
        ulong second,
        ulong third,
        ulong fourth)
    {
        _first = first;
        _second = second;
        _third = third;
        _fourth = fourth;
    }

    public bool IsEmpty =>
        _first == 0
        && _second == 0
        && _third == 0
        && _fourth == 0;

    public static TrafficGroupMovementMask FromAxisBins(
        int sourceAxisBin,
        int destinationAxisBin)
    {
        if (sourceAxisBin < 0
            || sourceAxisBin >= AxisBinCount
            || destinationAxisBin < 0
            || destinationAxisBin >= AxisBinCount)
        {
            return default;
        }

        int bitIndex = (sourceAxisBin * AxisBinCount) + destinationAxisBin;
        int wordIndex = bitIndex / 64;
        ulong bit = 1UL << (bitIndex % 64);
        return wordIndex switch
        {
            0 => new TrafficGroupMovementMask(bit, 0, 0, 0),
            1 => new TrafficGroupMovementMask(0, bit, 0, 0),
            2 => new TrafficGroupMovementMask(0, 0, bit, 0),
            _ => new TrafficGroupMovementMask(0, 0, 0, bit),
        };
    }

    public int IntersectionCount(TrafficGroupMovementMask other)
    {
        return PopCount(_first & other._first)
            + PopCount(_second & other._second)
            + PopCount(_third & other._third)
            + PopCount(_fourth & other._fourth);
    }

    public int DifferenceCount(TrafficGroupMovementMask other)
    {
        return PopCount(_first ^ other._first)
            + PopCount(_second ^ other._second)
            + PopCount(_third ^ other._third)
            + PopCount(_fourth ^ other._fourth);
    }

    public string ToDiagnosticString()
    {
        return $"{_first:X16}:{_second:X16}:{_third:X16}:{_fourth:X16}";
    }

    public bool Equals(TrafficGroupMovementMask other)
    {
        return _first == other._first
            && _second == other._second
            && _third == other._third
            && _fourth == other._fourth;
    }

    public override bool Equals(object obj)
    {
        return obj is TrafficGroupMovementMask other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = _first.GetHashCode();
            hash = (hash * 397) ^ _second.GetHashCode();
            hash = (hash * 397) ^ _third.GetHashCode();
            hash = (hash * 397) ^ _fourth.GetHashCode();
            return hash;
        }
    }

    public static TrafficGroupMovementMask operator |(
        TrafficGroupMovementMask left,
        TrafficGroupMovementMask right)
    {
        return new TrafficGroupMovementMask(
            left._first | right._first,
            left._second | right._second,
            left._third | right._third,
            left._fourth | right._fourth);
    }

    public static bool operator ==(
        TrafficGroupMovementMask left,
        TrafficGroupMovementMask right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(
        TrafficGroupMovementMask left,
        TrafficGroupMovementMask right)
    {
        return !left.Equals(right);
    }

    private static int PopCount(ulong value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }
}

public readonly struct TrafficGroupPhaseSignature
{
    public TrafficGroupPhaseSignature(
        int signalGroup,
        ulong roadApproachAxisMask,
        ulong trackApproachAxisMask)
        : this(
            signalGroup,
            roadApproachAxisMask,
            trackApproachAxisMask,
            default,
            default,
            default,
            default)
    {
    }

    public TrafficGroupPhaseSignature(
        int signalGroup,
        ulong roadApproachAxisMask,
        ulong trackApproachAxisMask,
        TrafficGroupMovementMask roadMovements,
        TrafficGroupMovementMask trackMovements,
        TrafficGroupMovementMask roadYieldMovements,
        TrafficGroupMovementMask trackYieldMovements)
    {
        SignalGroup = signalGroup;
        RoadApproachAxisMask = roadApproachAxisMask;
        TrackApproachAxisMask = trackApproachAxisMask;
        RoadMovements = roadMovements;
        TrackMovements = trackMovements;
        RoadYieldMovements = roadYieldMovements;
        TrackYieldMovements = trackYieldMovements;
    }

    public int SignalGroup { get; }

    public ulong RoadApproachAxisMask { get; }

    public ulong TrackApproachAxisMask { get; }

    public TrafficGroupMovementMask RoadMovements { get; }

    public TrafficGroupMovementMask TrackMovements { get; }

    public TrafficGroupMovementMask RoadYieldMovements { get; }

    public TrafficGroupMovementMask TrackYieldMovements { get; }

    public bool HasMovements =>
        !RoadMovements.IsEmpty
        || !TrackMovements.IsEmpty;

    public bool HasApproach =>
        RoadApproachAxisMask != 0
        || TrackApproachAxisMask != 0;

    public string ToDiagnosticString()
    {
        return $"group={SignalGroup},"
            + $"roadAxes={RoadApproachAxisMask:X16},"
            + $"trackAxes={TrackApproachAxisMask:X16},"
            + $"roadMovements={RoadMovements.ToDiagnosticString()},"
            + $"trackMovements={TrackMovements.ToDiagnosticString()},"
            + $"roadYield={RoadYieldMovements.ToDiagnosticString()},"
            + $"trackYield={TrackYieldMovements.ToDiagnosticString()}";
    }
}

public enum TrafficGroupMovementMappingFailureReason
{
    None,
    LeaderPhaseCountOutOfRange,
    MemberPhaseCountOutOfRange,
    LeaderSignalGroupOutOfSequence,
    MemberSignalGroupOutOfSequence,
    LeaderPhaseHasNoApproach,
    MemberPhaseHasNoApproach,
    MemberHasFewerPhases,
    AmbiguousExactMatch,
    NoOverlappingPhase,
    TiedBestOverlap,
    InvalidFinalMap,
}

public readonly struct TrafficGroupMovementMappingFailure
{
    public TrafficGroupMovementMappingFailure(
        TrafficGroupMovementMappingFailureReason reason,
        int leaderPhase = 0,
        int memberPhase = 0)
    {
        Reason = reason;
        LeaderPhase = leaderPhase;
        MemberPhase = memberPhase;
    }

    public TrafficGroupMovementMappingFailureReason Reason { get; }

    public int LeaderPhase { get; }

    public int MemberPhase { get; }
}

public readonly struct TrafficGroupPhaseMap
{
    private const int BitsPerPhase = 5;
    private const int PhasesPerPackedValue = 8;
    private const ulong PackedPhaseMask = (1UL << BitsPerPhase) - 1;

    private readonly ulong _firstEightMappings;
    private readonly ulong _lastEightMappings;
    private readonly byte _leaderPhaseCount;
    private readonly byte _memberPhaseCount;
    private readonly bool _isComplete;

    private TrafficGroupPhaseMap(
        ulong firstEightMappings,
        ulong lastEightMappings,
        int leaderPhaseCount,
        int memberPhaseCount)
    {
        _firstEightMappings = firstEightMappings;
        _lastEightMappings = lastEightMappings;
        _leaderPhaseCount = (byte)leaderPhaseCount;
        _memberPhaseCount = (byte)memberPhaseCount;
        _isComplete = true;
    }

    public int LeaderPhaseCount => _leaderPhaseCount;

    public int MemberPhaseCount => _memberPhaseCount;

    public bool IsComplete => _isComplete;

    public bool TryMapLeaderToMember(int leaderPhase, out int memberPhase)
    {
        if (!_isComplete
            || leaderPhase < 1
            || leaderPhase > _leaderPhaseCount)
        {
            memberPhase = 0;
            return false;
        }

        int zeroBasedPhase = leaderPhase - 1;
        ulong packedMappings = zeroBasedPhase < PhasesPerPackedValue
            ? _firstEightMappings
            : _lastEightMappings;
        int packedIndex = zeroBasedPhase % PhasesPerPackedValue;
        memberPhase = (int)((packedMappings >> (packedIndex * BitsPerPhase)) & PackedPhaseMask);
        return memberPhase >= 1 && memberPhase <= _memberPhaseCount;
    }

    public bool TryMapMemberToLeader(int memberPhase, out int leaderPhase)
    {
        if (!_isComplete
            || memberPhase < 1
            || memberPhase > _memberPhaseCount)
        {
            leaderPhase = 0;
            return false;
        }

        for (int candidate = 1; candidate <= _leaderPhaseCount; candidate++)
        {
            if (TryMapLeaderToMember(candidate, out int mappedMemberPhase)
                && mappedMemberPhase == memberPhase)
            {
                leaderPhase = candidate;
                return true;
            }
        }

        leaderPhase = 0;
        return false;
    }

    internal static bool TryCreate(
        int leaderPhaseCount,
        int memberPhaseCount,
        IReadOnlyList<int> memberPhaseByLeaderPhase,
        out TrafficGroupPhaseMap map)
    {
        if (leaderPhaseCount < 1
            || leaderPhaseCount > TrafficGroupMovementMappingPolicy.MaximumMappedPhaseCount
            || memberPhaseCount < leaderPhaseCount
            || memberPhaseCount > TrafficGroupMovementMappingPolicy.MaximumMappedPhaseCount
            || memberPhaseByLeaderPhase.Count != leaderPhaseCount)
        {
            map = default;
            return false;
        }

        ulong firstEightMappings = 0;
        ulong lastEightMappings = 0;
        int usedMemberMask = 0;
        for (int index = 0; index < leaderPhaseCount; index++)
        {
            int memberPhase = memberPhaseByLeaderPhase[index];
            if (memberPhase < 1 || memberPhase > memberPhaseCount)
            {
                map = default;
                return false;
            }

            int memberBit = 1 << (memberPhase - 1);
            if ((usedMemberMask & memberBit) != 0)
            {
                map = default;
                return false;
            }

            usedMemberMask |= memberBit;
            if (index < PhasesPerPackedValue)
            {
                firstEightMappings |= (ulong)memberPhase << (index * BitsPerPhase);
            }
            else
            {
                lastEightMappings |= (ulong)memberPhase
                    << ((index - PhasesPerPackedValue) * BitsPerPhase);
            }
        }

        map = new TrafficGroupPhaseMap(
            firstEightMappings,
            lastEightMappings,
            leaderPhaseCount,
            memberPhaseCount);
        return true;
    }
}

public static class TrafficGroupMovementMappingPolicy
{
    public const int AxisBinCount = 16;
    public const int MaximumMappedPhaseCount = 16;

    private const int RoadOverlapWeight = 16;
    private const int TrackOverlapWeight = 24;
    private const int RoadDifferencePenalty = 2;
    private const int TrackDifferencePenalty = 3;

    public static int QuantizeUndirectedAxis(double x, double z)
    {
        if (Math.Abs(x) < 0.000001 && Math.Abs(z) < 0.000001)
        {
            return -1;
        }

        double angle = Math.Atan2(z, x) % Math.PI;
        if (angle < 0)
        {
            angle += Math.PI;
        }

        int bin = (int)Math.Floor(angle * AxisBinCount / Math.PI);
        return Math.Min(AxisBinCount - 1, Math.Max(0, bin));
    }

    public static bool TryBuildIdentity(
        IReadOnlyList<TrafficGroupPhaseSignature> leader,
        IReadOnlyList<TrafficGroupPhaseSignature> member,
        out TrafficGroupPhaseMap map,
        out TrafficGroupMovementMappingFailure failure)
    {
        if (!HasValidSignatures(
                leader,
                TrafficGroupMovementMappingFailureReason.LeaderPhaseCountOutOfRange,
                TrafficGroupMovementMappingFailureReason.LeaderSignalGroupOutOfSequence,
                TrafficGroupMovementMappingFailureReason.LeaderPhaseHasNoApproach,
                true,
                out failure))
        {
            map = default;
            return false;
        }

        if (!HasValidSignatures(
                member,
                TrafficGroupMovementMappingFailureReason.MemberPhaseCountOutOfRange,
                TrafficGroupMovementMappingFailureReason.MemberSignalGroupOutOfSequence,
                TrafficGroupMovementMappingFailureReason.MemberPhaseHasNoApproach,
                false,
                out failure))
        {
            map = default;
            return false;
        }

        if (member.Count < leader.Count)
        {
            map = default;
            failure = new TrafficGroupMovementMappingFailure(
                TrafficGroupMovementMappingFailureReason.MemberHasFewerPhases);
            return false;
        }

        var memberPhaseByLeaderPhase = new int[leader.Count];
        for (int phaseIndex = 0; phaseIndex < leader.Count; phaseIndex++)
        {
            memberPhaseByLeaderPhase[phaseIndex] = phaseIndex + 1;
        }

        if (!TrafficGroupPhaseMap.TryCreate(
                leader.Count,
                member.Count,
                memberPhaseByLeaderPhase,
                out map))
        {
            failure = new TrafficGroupMovementMappingFailure(
                TrafficGroupMovementMappingFailureReason.InvalidFinalMap);
            return false;
        }

        failure = default;
        return true;
    }

    public static bool TryBuild(
        IReadOnlyList<TrafficGroupPhaseSignature> leader,
        IReadOnlyList<TrafficGroupPhaseSignature> member,
        out TrafficGroupPhaseMap map)
    {
        return TryBuild(leader, member, out map, out _);
    }

    public static bool TryBuild(
        IReadOnlyList<TrafficGroupPhaseSignature> leader,
        IReadOnlyList<TrafficGroupPhaseSignature> member,
        out TrafficGroupPhaseMap map,
        out TrafficGroupMovementMappingFailure failure)
    {
        if (!HasValidSignatures(
                leader,
                TrafficGroupMovementMappingFailureReason.LeaderPhaseCountOutOfRange,
                TrafficGroupMovementMappingFailureReason.LeaderSignalGroupOutOfSequence,
                TrafficGroupMovementMappingFailureReason.LeaderPhaseHasNoApproach,
                true,
                out failure))
        {
            map = default;
            return false;
        }

        if (!HasValidSignatures(
                member,
                TrafficGroupMovementMappingFailureReason.MemberPhaseCountOutOfRange,
                TrafficGroupMovementMappingFailureReason.MemberSignalGroupOutOfSequence,
                TrafficGroupMovementMappingFailureReason.MemberPhaseHasNoApproach,
                false,
                out failure))
        {
            map = default;
            return false;
        }

        if (member.Count < leader.Count)
        {
            map = default;
            failure = new TrafficGroupMovementMappingFailure(
                TrafficGroupMovementMappingFailureReason.MemberHasFewerPhases);
            return false;
        }

        var memberPhaseByLeaderPhase = new int[leader.Count];
        int usedMemberMask = 0;
        for (int leaderIndex = 0; leaderIndex < leader.Count; leaderIndex++)
        {
            TrafficGroupPhaseSignature leaderSignature = leader[leaderIndex];
            int exactMatch = 0;
            int exactMatchCount = 0;

            for (int memberIndex = 0; memberIndex < member.Count; memberIndex++)
            {
                int memberBit = 1 << memberIndex;
                if ((usedMemberMask & memberBit) != 0)
                {
                    continue;
                }

                TrafficGroupPhaseSignature memberSignature = member[memberIndex];
                if (AreExactMatch(leaderSignature, memberSignature))
                {
                    exactMatch = memberIndex + 1;
                    exactMatchCount++;
                }
            }

            int selectedMemberPhase;
            if (exactMatchCount == 1)
            {
                selectedMemberPhase = exactMatch;
            }
            else if (exactMatchCount > 1)
            {
                map = default;
                failure = new TrafficGroupMovementMappingFailure(
                    TrafficGroupMovementMappingFailureReason.AmbiguousExactMatch,
                    leaderIndex + 1);
                return false;
            }
            else if (!TryFindUniqueBestOverlap(
                         leaderSignature,
                         member,
                         usedMemberMask,
                         out selectedMemberPhase,
                         out TrafficGroupMovementMappingFailureReason overlapFailure))
            {
                map = default;
                failure = new TrafficGroupMovementMappingFailure(
                    overlapFailure,
                    leaderIndex + 1);
                return false;
            }

            memberPhaseByLeaderPhase[leaderIndex] = selectedMemberPhase;
            usedMemberMask |= 1 << (selectedMemberPhase - 1);
        }

        if (!TrafficGroupPhaseMap.TryCreate(
            leader.Count,
            member.Count,
            memberPhaseByLeaderPhase,
            out map))
        {
            failure = new TrafficGroupMovementMappingFailure(
                TrafficGroupMovementMappingFailureReason.InvalidFinalMap);
            return false;
        }

        failure = default;
        return true;
    }

    private static bool HasValidSignatures(
        IReadOnlyList<TrafficGroupPhaseSignature> signatures,
        TrafficGroupMovementMappingFailureReason countFailure,
        TrafficGroupMovementMappingFailureReason sequenceFailure,
        TrafficGroupMovementMappingFailureReason approachFailure,
        bool isLeader,
        out TrafficGroupMovementMappingFailure failure)
    {
        if (signatures.Count < 1 || signatures.Count > MaximumMappedPhaseCount)
        {
            failure = new TrafficGroupMovementMappingFailure(countFailure);
            return false;
        }

        for (int index = 0; index < signatures.Count; index++)
        {
            TrafficGroupPhaseSignature signature = signatures[index];
            if (signature.SignalGroup != index + 1)
            {
                failure = new TrafficGroupMovementMappingFailure(
                    sequenceFailure,
                    isLeader ? index + 1 : 0,
                    isLeader ? 0 : index + 1);
                return false;
            }

            if (!signature.HasApproach)
            {
                failure = new TrafficGroupMovementMappingFailure(
                    approachFailure,
                    isLeader ? index + 1 : 0,
                    isLeader ? 0 : index + 1);
                return false;
            }
        }

        failure = default;
        return true;
    }

    private static bool TryFindUniqueBestOverlap(
        TrafficGroupPhaseSignature leader,
        IReadOnlyList<TrafficGroupPhaseSignature> members,
        int usedMemberMask,
        out int memberPhase,
        out TrafficGroupMovementMappingFailureReason failure)
    {
        int bestScore = int.MinValue;
        int bestMemberPhase = 0;
        bool tied = false;

        for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
        {
            int memberBit = 1 << memberIndex;
            if ((usedMemberMask & memberBit) != 0)
            {
                continue;
            }

            TrafficGroupPhaseSignature member = members[memberIndex];
            bool compareMovements = leader.HasMovements && member.HasMovements;
            int roadOverlap = compareMovements
                ? leader.RoadMovements.IntersectionCount(member.RoadMovements)
                : PopCount(leader.RoadApproachAxisMask & member.RoadApproachAxisMask);
            int trackOverlap = compareMovements
                ? leader.TrackMovements.IntersectionCount(member.TrackMovements)
                : PopCount(leader.TrackApproachAxisMask & member.TrackApproachAxisMask);
            if (roadOverlap == 0 && trackOverlap == 0)
            {
                continue;
            }

            int score =
                (roadOverlap * RoadOverlapWeight)
                + (trackOverlap * TrackOverlapWeight)
                + (leader.RoadYieldMovements.IntersectionCount(member.RoadYieldMovements)
                    * RoadOverlapWeight)
                + (leader.TrackYieldMovements.IntersectionCount(member.TrackYieldMovements)
                    * TrackOverlapWeight)
                - ((compareMovements
                        ? leader.RoadMovements.DifferenceCount(member.RoadMovements)
                        : PopCount(leader.RoadApproachAxisMask ^ member.RoadApproachAxisMask))
                    * RoadDifferencePenalty)
                - ((compareMovements
                        ? leader.TrackMovements.DifferenceCount(member.TrackMovements)
                        : PopCount(leader.TrackApproachAxisMask ^ member.TrackApproachAxisMask))
                    * TrackDifferencePenalty)
                - (leader.RoadYieldMovements.DifferenceCount(member.RoadYieldMovements)
                    * RoadDifferencePenalty)
                - (leader.TrackYieldMovements.DifferenceCount(member.TrackYieldMovements)
                    * TrackDifferencePenalty);

            if (score > bestScore)
            {
                bestScore = score;
                bestMemberPhase = memberIndex + 1;
                tied = false;
            }
            else if (score == bestScore)
            {
                tied = true;
            }
        }

        memberPhase = bestMemberPhase;
        if (bestMemberPhase == 0)
        {
            failure = TrafficGroupMovementMappingFailureReason.NoOverlappingPhase;
            return false;
        }

        if (tied)
        {
            failure = TrafficGroupMovementMappingFailureReason.TiedBestOverlap;
            return false;
        }

        failure = TrafficGroupMovementMappingFailureReason.None;
        return true;
    }

    private static bool AreExactMatch(
        TrafficGroupPhaseSignature leader,
        TrafficGroupPhaseSignature member)
    {
        if (leader.HasMovements && member.HasMovements)
        {
            return leader.RoadMovements == member.RoadMovements
                && leader.TrackMovements == member.TrackMovements
                && leader.RoadYieldMovements == member.RoadYieldMovements
                && leader.TrackYieldMovements == member.TrackYieldMovements;
        }

        return leader.RoadApproachAxisMask == member.RoadApproachAxisMask
            && leader.TrackApproachAxisMask == member.TrackApproachAxisMask;
    }

    private static int PopCount(ulong value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }
}
