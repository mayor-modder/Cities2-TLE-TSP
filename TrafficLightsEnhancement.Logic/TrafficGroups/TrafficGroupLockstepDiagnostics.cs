using System;

namespace TrafficLightsEnhancement.Logic.TrafficGroups;

public readonly struct TrafficGroupLockstepControllerSnapshot : IEquatable<TrafficGroupLockstepControllerSnapshot>
{
    public TrafficGroupLockstepControllerSnapshot(
        byte state,
        byte currentGroup,
        byte nextGroup,
        ushort timer,
        uint customTimer,
        byte signalGroupCount)
    {
        State = state;
        CurrentGroup = currentGroup;
        NextGroup = nextGroup;
        Timer = timer;
        CustomTimer = customTimer;
        SignalGroupCount = signalGroupCount;
    }

    public byte State { get; }

    public byte CurrentGroup { get; }

    public byte NextGroup { get; }

    public ushort Timer { get; }

    public uint CustomTimer { get; }

    public byte SignalGroupCount { get; }

    public bool Equals(TrafficGroupLockstepControllerSnapshot other)
    {
        return State == other.State
            && CurrentGroup == other.CurrentGroup
            && NextGroup == other.NextGroup
            && Timer == other.Timer
            && CustomTimer == other.CustomTimer
            && SignalGroupCount == other.SignalGroupCount;
    }

    public override bool Equals(object obj)
    {
        return obj is TrafficGroupLockstepControllerSnapshot other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = State;
            hash = (hash * 397) ^ CurrentGroup;
            hash = (hash * 397) ^ NextGroup;
            hash = (hash * 397) ^ Timer;
            hash = (hash * 397) ^ (int)CustomTimer;
            hash = (hash * 397) ^ SignalGroupCount;
            return hash;
        }
    }

    public static bool operator ==(
        TrafficGroupLockstepControllerSnapshot left,
        TrafficGroupLockstepControllerSnapshot right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(
        TrafficGroupLockstepControllerSnapshot left,
        TrafficGroupLockstepControllerSnapshot right)
    {
        return !left.Equals(right);
    }
}

[Flags]
public enum TrafficGroupLockstepPassFlags : ushort
{
    None = 0,
    CollectionVisited = 1 << 0,
    IndependentVisited = 1 << 1,
    IndependentDeferred = 1 << 2,
    IndependentHeld = 1 << 3,
    IndependentAdvanced = 1 << 4,
    SynchronizationVisited = 1 << 5,
    SynchronizationApplied = 1 << 6,
}

public enum TrafficGroupLockstepSyncDisposition : byte
{
    None,
    Applied,
    NotLockstep,
    MissingMaster,
    InvalidMaster,
    MissingMapping,
    IncompleteMapping,
    UnmappedCurrentPhase,
    UnmappedNextPhase,
    InactiveGroup,
    MissingLocalDemand,
}

public enum TrafficGroupLockstepVerdict : byte
{
    InSync,
    GreenWaveExcluded,
    InsufficientEvidence,
    IndependentStateMachineAdvanced,
    SynchronizationDidNotRun,
    SynchronizationRefused,
    ControllerChangedAfterSynchronization,
    LaneOutputsChangedAfterSynchronization,
    RenderedOutputsChangedAfterSynchronization,
    OutputMasksDoNotRepresentMappedPhase,
}

public readonly struct TrafficGroupLockstepEvidence
{
    public TrafficGroupLockstepEvidence(
        bool hasDebugState,
        bool isCoordinated,
        bool isGreenWave,
        TrafficGroupLockstepPassFlags passFlags,
        TrafficGroupLockstepSyncDisposition syncDisposition,
        TrafficGroupLockstepControllerSnapshot before,
        TrafficGroupLockstepControllerSnapshot master,
        TrafficGroupLockstepControllerSnapshot after,
        TrafficGroupLockstepControllerSnapshot live,
        ulong laneHashAfter,
        ulong liveLaneHash,
        ulong renderedHashAfter,
        ulong liveRenderedHash,
        ushort mappedCurrentGroupBit,
        ushort mappedNextGroupBit,
        ushort liveOutputGroupMask)
    {
        HasDebugState = hasDebugState;
        IsCoordinated = isCoordinated;
        IsGreenWave = isGreenWave;
        PassFlags = passFlags;
        SyncDisposition = syncDisposition;
        Before = before;
        Master = master;
        After = after;
        Live = live;
        LaneHashAfter = laneHashAfter;
        LiveLaneHash = liveLaneHash;
        RenderedHashAfter = renderedHashAfter;
        LiveRenderedHash = liveRenderedHash;
        MappedCurrentGroupBit = mappedCurrentGroupBit;
        MappedNextGroupBit = mappedNextGroupBit;
        LiveOutputGroupMask = liveOutputGroupMask;
    }

    public bool HasDebugState { get; }

    public bool IsCoordinated { get; }

    public bool IsGreenWave { get; }

    public TrafficGroupLockstepPassFlags PassFlags { get; }

    public TrafficGroupLockstepSyncDisposition SyncDisposition { get; }

    public TrafficGroupLockstepControllerSnapshot Before { get; }

    public TrafficGroupLockstepControllerSnapshot Master { get; }

    public TrafficGroupLockstepControllerSnapshot After { get; }

    public TrafficGroupLockstepControllerSnapshot Live { get; }

    public ulong LaneHashAfter { get; }

    public ulong LiveLaneHash { get; }

    public ulong RenderedHashAfter { get; }

    public ulong LiveRenderedHash { get; }

    public ushort MappedCurrentGroupBit { get; }

    public ushort MappedNextGroupBit { get; }

    public ushort LiveOutputGroupMask { get; }
}

public readonly struct TrafficGroupLockstepClassification
{
    public TrafficGroupLockstepClassification(
        TrafficGroupLockstepVerdict verdict,
        string reason)
    {
        Verdict = verdict;
        Reason = reason;
    }

    public TrafficGroupLockstepVerdict Verdict { get; }

    public string Reason { get; }
}

public static class TrafficGroupLockstepDiagnostics
{
    public const ulong FnvOffsetBasis = 14695981039346656037UL;

    private const ulong FnvPrime = 1099511628211UL;

    public static TrafficGroupLockstepClassification Classify(
        in TrafficGroupLockstepEvidence evidence)
    {
        if (!evidence.HasDebugState)
        {
            return Result(
                TrafficGroupLockstepVerdict.InsufficientEvidence,
                "The runtime diagnostic component was not present.");
        }

        if (evidence.IsGreenWave)
        {
            return Result(
                TrafficGroupLockstepVerdict.GreenWaveExcluded,
                "Green-wave members use offset timing and are excluded from lockstep.");
        }

        if (!evidence.IsCoordinated)
        {
            return Result(
                TrafficGroupLockstepVerdict.InsufficientEvidence,
                "The traffic group is not in lockstep mode.");
        }

        if ((evidence.PassFlags & TrafficGroupLockstepPassFlags.IndependentAdvanced) != 0)
        {
            return Result(
                TrafficGroupLockstepVerdict.IndependentStateMachineAdvanced,
                "The follower controller advanced during the independent pass.");
        }

        if ((evidence.PassFlags & TrafficGroupLockstepPassFlags.SynchronizationVisited) == 0
            || evidence.SyncDisposition == TrafficGroupLockstepSyncDisposition.MissingMaster)
        {
            return Result(
                TrafficGroupLockstepVerdict.SynchronizationDidNotRun,
                evidence.SyncDisposition == TrafficGroupLockstepSyncDisposition.MissingMaster
                    ? "Synchronization visited the follower but its master was unavailable."
                    : "The follower was not visited by the synchronization pass.");
        }

        if (evidence.SyncDisposition != TrafficGroupLockstepSyncDisposition.Applied
            || (evidence.PassFlags & TrafficGroupLockstepPassFlags.SynchronizationApplied) == 0)
        {
            return Result(
                TrafficGroupLockstepVerdict.SynchronizationRefused,
                $"Synchronization was refused: {evidence.SyncDisposition}.");
        }

        if (evidence.After != evidence.Master)
        {
            return Result(
                TrafficGroupLockstepVerdict.SynchronizationRefused,
                "The follower controller did not match the mapped master snapshot after synchronization.");
        }

        if (evidence.Live != evidence.After)
        {
            return Result(
                TrafficGroupLockstepVerdict.ControllerChangedAfterSynchronization,
                "The follower controller changed after the synchronization pass.");
        }

        if (evidence.LiveLaneHash != evidence.LaneHashAfter)
        {
            return Result(
                TrafficGroupLockstepVerdict.LaneOutputsChangedAfterSynchronization,
                "Lane-signal outputs changed after the synchronization pass.");
        }

        if (evidence.LiveRenderedHash != evidence.RenderedHashAfter)
        {
            return Result(
                TrafficGroupLockstepVerdict.RenderedOutputsChangedAfterSynchronization,
                "Rendered traffic-light outputs changed after the synchronization pass.");
        }

        ushort expectedMask;
        switch (evidence.Live.State)
        {
            case 1: // Beginning exposes the next phase.
                expectedMask = evidence.MappedNextGroupBit;
                break;
            case 2: // Ongoing
            case 3: // Ending
            case 5: // Extending
            case 6: // Extended
                expectedMask = evidence.MappedCurrentGroupBit;
                break;
            default:
                expectedMask = 0;
                break;
        }
        if (expectedMask != 0
            && (evidence.LiveOutputGroupMask & expectedMask) != expectedMask)
        {
            return Result(
                TrafficGroupLockstepVerdict.OutputMasksDoNotRepresentMappedPhase,
                "Live lane outputs do not contain every mapped current/next phase bit.");
        }

        return Result(
            TrafficGroupLockstepVerdict.InSync,
            "All captured lockstep boundaries match.");
    }

    public static ulong AddHash(ulong hash, ulong value)
    {
        unchecked
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= FnvPrime;
            }

            return hash;
        }
    }

    private static TrafficGroupLockstepClassification Result(
        TrafficGroupLockstepVerdict verdict,
        string reason)
    {
        return new TrafficGroupLockstepClassification(verdict, reason);
    }
}
