namespace TrafficLightsEnhancement.Logic.Tsp;

public readonly struct TspSignalRequest
{
    public TspSignalRequest(int targetSignalGroup, TspSource source, float strength, uint expiryTimer, bool extendCurrentPhase)
    {
        TargetSignalGroup = targetSignalGroup;
        Source = source;
        Strength = strength;
        ExpiryTimer = expiryTimer;
        ExtendCurrentPhase = extendCurrentPhase;
    }

    public int TargetSignalGroup { get; }
    public TspSource Source { get; }
    public float Strength { get; }
    public uint ExpiryTimer { get; }
    public bool ExtendCurrentPhase { get; }
}

public static class TspPreemptionPolicy
{
    public const uint ReleaseGraceTicks = 6;

    public static bool TryRefreshOrLatchRequest(
        TspSignalRequest? freshRequest,
        TspSignalRequest? existingRequest,
        ushort requestHorizonTicks,
        byte currentSignalGroup,
        out TspSignalRequest request)
    {
        if (freshRequest.HasValue)
        {
            TspSignalRequest fresh = freshRequest.Value;
            request = new TspSignalRequest(
                fresh.TargetSignalGroup,
                fresh.Source,
                fresh.Strength,
                requestHorizonTicks,
                fresh.ExtendCurrentPhase);
            return true;
        }

        request = default;
        return false;
    }

    public static bool ShouldHoldCurrentGroup(
        byte currentSignalGroup,
        TspSignalRequest request,
        uint signalTimer,
        ushort maxGreenExtensionTicks)
    {
        return currentSignalGroup > 0
            && request.TargetSignalGroup == currentSignalGroup
            && request.ExtendCurrentPhase
            && request.ExpiryTimer > 0
            && signalTimer < maxGreenExtensionTicks;
    }

    public static int GetMinimumGreenDurationTicks(
        int defaultMinimumGreenTicks,
        byte currentSignalGroup,
        TspSignalRequest request)
    {
        return request.TargetSignalGroup > 0
            && request.TargetSignalGroup != currentSignalGroup
            && request.ExpiryTimer > 0
            ? 1
            : defaultMinimumGreenTicks;
    }

    public static bool ShouldAggressivelyPreemptToConflictingGroup(
        byte currentSignalGroup,
        TspSignalRequest request)
    {
        return request.TargetSignalGroup > 0
            && request.TargetSignalGroup != currentSignalGroup
            && request.ExpiryTimer > 0;
    }
}
