using System;
using System.Collections.Generic;

namespace TrafficLightsEnhancement.Logic.TrafficGroups;

public static class TrafficGroupTimingPolicy
{
    public static int WrapOneBasedPhase(int phase, int phaseCount)
    {
        if (phaseCount <= 0 || phase <= 0)
        {
            return 1;
        }

        int wrapped = ((phase - 1) % phaseCount) + 1;
        return wrapped <= 0 ? wrapped + phaseCount : wrapped;
    }

    public static float WrapCyclePosition(float cycleTimer, float offset, float cycleLength)
    {
        if (cycleLength <= 0f || float.IsNaN(cycleLength))
        {
            return 0f;
        }

        float position = (cycleTimer - offset) % cycleLength;
        return position < 0f ? position + cycleLength : position;
    }

    public static int CalculateZeroBasedPhaseOffset(float arrivalTime, float cycleLength, int phaseCount)
    {
        if (cycleLength <= 0f || phaseCount <= 0)
        {
            return 0;
        }

        int offset = (int)(arrivalTime / cycleLength * phaseCount);
        offset %= Math.Max(1, phaseCount);
        return offset < 0 ? offset + phaseCount : offset;
    }

    public static int DetermineOneBasedPhaseFromEvenCycle(float cyclePosition, float cycleLength, int signalGroupCount)
    {
        if (signalGroupCount <= 0)
        {
            return 1;
        }

        float phaseLength = cycleLength / signalGroupCount;
        int phase = (int)(cyclePosition / Math.Max(1f, phaseLength)) + 1;
        return Clamp(phase, 1, signalGroupCount);
    }

    public static int DetermineOneBasedPhaseFromDurations(float cyclePosition, IReadOnlyList<float> maximumDurations)
    {
        if (maximumDurations.Count == 0)
        {
            return 1;
        }

        float accumulated = 0f;
        for (int i = 0; i < maximumDurations.Count; i++)
        {
            accumulated += maximumDurations[i];
            if (cyclePosition < accumulated)
            {
                return i + 1;
            }
        }

        return 1;
    }

    private static int Clamp(int value, int minimum, int maximum)
    {
        if (value < minimum)
        {
            return minimum;
        }

        return value > maximum ? maximum : value;
    }
}
