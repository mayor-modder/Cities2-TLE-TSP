using System;

namespace TrafficLightsEnhancement.Logic.TrafficGroups;

public readonly struct VanillaTrafficGroupDemand
{
    public VanillaTrafficGroupDemand(
        int highestPriority,
        int requestedPhaseMask,
        int extendablePhaseMask,
        int suppressedPhaseMask)
    {
        HighestPriority = highestPriority;
        RequestedPhaseMask = requestedPhaseMask;
        ExtendablePhaseMask = extendablePhaseMask;
        SuppressedPhaseMask = suppressedPhaseMask;
    }

    public int HighestPriority { get; }

    public int RequestedPhaseMask { get; }

    public int ExtendablePhaseMask { get; }

    public int SuppressedPhaseMask { get; }
}

public static class VanillaTrafficGroupDemandPolicy
{
    private const int MaximumPhaseCount = 31;

    public static VanillaTrafficGroupDemand Merge(
        VanillaTrafficGroupDemand current,
        VanillaTrafficGroupDemand candidate)
    {
        int suppressedPhaseMask = current.SuppressedPhaseMask | candidate.SuppressedPhaseMask;
        if (candidate.HighestPriority > current.HighestPriority)
        {
            return new VanillaTrafficGroupDemand(
                candidate.HighestPriority,
                candidate.RequestedPhaseMask,
                candidate.ExtendablePhaseMask,
                suppressedPhaseMask);
        }

        if (candidate.HighestPriority == current.HighestPriority)
        {
            return new VanillaTrafficGroupDemand(
                current.HighestPriority,
                current.RequestedPhaseMask | candidate.RequestedPhaseMask,
                current.ExtendablePhaseMask | candidate.ExtendablePhaseMask,
                suppressedPhaseMask);
        }

        return new VanillaTrafficGroupDemand(
            current.HighestPriority,
            current.RequestedPhaseMask,
            current.ExtendablePhaseMask,
            suppressedPhaseMask);
    }

    public static bool TryRemap(
        VanillaTrafficGroupDemand demand,
        int sourcePhaseCount,
        int targetPhaseCount,
        out VanillaTrafficGroupDemand remapped)
    {
        if (!IsValidPhaseCount(sourcePhaseCount) || !IsValidPhaseCount(targetPhaseCount))
        {
            remapped = default;
            return false;
        }

        remapped = new VanillaTrafficGroupDemand(
            demand.HighestPriority,
            RemapMask(demand.RequestedPhaseMask, sourcePhaseCount, targetPhaseCount),
            RemapMask(demand.ExtendablePhaseMask, sourcePhaseCount, targetPhaseCount),
            RemapMask(demand.SuppressedPhaseMask, sourcePhaseCount, targetPhaseCount));
        return true;
    }

    public static int MapRequiredOneBasedPhase(int phase, int phaseCount)
    {
        return TrafficGroupTimingPolicy.WrapOneBasedPhase(phase, phaseCount);
    }

    public static int MapOptionalOneBasedPhase(int phase, int phaseCount)
    {
        return phase == 0 ? 0 : MapRequiredOneBasedPhase(phase, phaseCount);
    }

    public static int SelectNextPhase(
        VanillaTrafficGroupDemand demand,
        int currentPhase,
        int phaseCount,
        bool preferChange,
        out bool canExtend)
    {
        if (!IsValidPhaseCount(phaseCount))
        {
            canExtend = false;
            return currentPhase;
        }

        int requestedPhaseMask = demand.RequestedPhaseMask;
        if (demand.HighestPriority == 0)
        {
            preferChange = false;
            requestedPhaseMask &= ~demand.SuppressedPhaseMask;
        }

        int nextPhase = currentPhase >= phaseCount ? 1 : currentPhase + 1;
        int firstPhase = preferChange ? nextPhase : Math.Max(1, currentPhase);
        int lastWrappedPhase = preferChange ? currentPhase : currentPhase - 1;

        canExtend = preferChange
            && currentPhase >= 1
            && (demand.ExtendablePhaseMask & (1 << (currentPhase - 1))) != 0;

        for (int phase = firstPhase; phase <= phaseCount; phase++)
        {
            if ((requestedPhaseMask & (1 << (phase - 1))) != 0)
            {
                return phase;
            }
        }

        for (int phase = 1; phase <= lastWrappedPhase; phase++)
        {
            if ((requestedPhaseMask & (1 << (phase - 1))) != 0)
            {
                return phase;
            }
        }

        return currentPhase;
    }

    private static int RemapMask(int mask, int sourcePhaseCount, int targetPhaseCount)
    {
        int remapped = 0;
        for (int sourcePhase = 1; sourcePhase <= sourcePhaseCount; sourcePhase++)
        {
            if ((mask & (1 << (sourcePhase - 1))) == 0)
            {
                continue;
            }

            int targetPhase = MapRequiredOneBasedPhase(sourcePhase, targetPhaseCount);
            remapped |= 1 << (targetPhase - 1);
        }

        return remapped;
    }

    private static bool IsValidPhaseCount(int phaseCount)
    {
        return phaseCount >= 1 && phaseCount <= MaximumPhaseCount;
    }
}
