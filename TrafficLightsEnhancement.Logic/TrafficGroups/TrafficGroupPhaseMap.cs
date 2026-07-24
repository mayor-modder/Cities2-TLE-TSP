using System;
using System.Collections.Generic;

namespace TrafficLightsEnhancement.Logic.TrafficGroups;

public readonly struct TrafficGroupPhaseSignature
{
    public TrafficGroupPhaseSignature(
        int signalGroup,
        ulong roadApproachAxisMask,
        ulong trackApproachAxisMask)
    {
        SignalGroup = signalGroup;
        RoadApproachAxisMask = roadApproachAxisMask;
        TrackApproachAxisMask = trackApproachAxisMask;
    }

    public int SignalGroup { get; }

    public ulong RoadApproachAxisMask { get; }

    public ulong TrackApproachAxisMask { get; }

    public bool HasApproach =>
        RoadApproachAxisMask != 0
        || TrackApproachAxisMask != 0;
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

    public static bool TryBuild(
        IReadOnlyList<TrafficGroupPhaseSignature> leader,
        IReadOnlyList<TrafficGroupPhaseSignature> member,
        out TrafficGroupPhaseMap map)
    {
        if (!HasValidSignatures(leader)
            || !HasValidSignatures(member)
            || member.Count < leader.Count)
        {
            map = default;
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
                if (leaderSignature.RoadApproachAxisMask == memberSignature.RoadApproachAxisMask
                    && leaderSignature.TrackApproachAxisMask == memberSignature.TrackApproachAxisMask)
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
                return false;
            }
            else if (!TryFindUniqueBestOverlap(
                         leaderSignature,
                         member,
                         usedMemberMask,
                         out selectedMemberPhase))
            {
                map = default;
                return false;
            }

            memberPhaseByLeaderPhase[leaderIndex] = selectedMemberPhase;
            usedMemberMask |= 1 << (selectedMemberPhase - 1);
        }

        return TrafficGroupPhaseMap.TryCreate(
            leader.Count,
            member.Count,
            memberPhaseByLeaderPhase,
            out map);
    }

    private static bool HasValidSignatures(IReadOnlyList<TrafficGroupPhaseSignature> signatures)
    {
        if (signatures.Count < 1 || signatures.Count > MaximumMappedPhaseCount)
        {
            return false;
        }

        for (int index = 0; index < signatures.Count; index++)
        {
            TrafficGroupPhaseSignature signature = signatures[index];
            if (signature.SignalGroup != index + 1 || !signature.HasApproach)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryFindUniqueBestOverlap(
        TrafficGroupPhaseSignature leader,
        IReadOnlyList<TrafficGroupPhaseSignature> members,
        int usedMemberMask,
        out int memberPhase)
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
            int roadOverlap = PopCount(
                leader.RoadApproachAxisMask & member.RoadApproachAxisMask);
            int trackOverlap = PopCount(
                leader.TrackApproachAxisMask & member.TrackApproachAxisMask);
            if (roadOverlap == 0 && trackOverlap == 0)
            {
                continue;
            }

            int score =
                (roadOverlap * RoadOverlapWeight)
                + (trackOverlap * TrackOverlapWeight)
                - (PopCount(leader.RoadApproachAxisMask ^ member.RoadApproachAxisMask)
                    * RoadDifferencePenalty)
                - (PopCount(leader.TrackApproachAxisMask ^ member.TrackApproachAxisMask)
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
        return bestMemberPhase != 0 && !tied;
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
