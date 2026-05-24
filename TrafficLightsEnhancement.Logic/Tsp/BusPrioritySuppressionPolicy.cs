namespace TrafficLightsEnhancement.Logic.Tsp;

public enum BusStopRelation : byte
{
    None = 0,
    NearSideBeforeSignal = 1,
    FarSideAfterSignal = 2,
    Unknown = 3,
}

public enum BusPrioritySuppressionReason : byte
{
    None = 0,
    Boarding = 1,
    NearSideStop = 2,
    UnknownStopRelation = 3,
}

public readonly struct BusPrioritySuppressionDecision
{
    public BusPrioritySuppressionDecision(bool isSuppressed, BusPrioritySuppressionReason reason)
    {
        IsSuppressed = isSuppressed;
        Reason = reason;
    }

    public bool IsSuppressed { get; }
    public BusPrioritySuppressionReason Reason { get; }
}

public static class BusPrioritySuppressionPolicy
{
    public const float MovingBusSpeedThreshold = 0.5f;

    public static bool IsMovingBus(float speed) => speed > MovingBusSpeedThreshold;

    public static BusPrioritySuppressionDecision EvaluateStopSuppression(
        TransitApproachSuppressionFlags flags,
        BusStopRelation stopRelation,
        bool isDedicatedBusApproach = false,
        bool isVehicleMoving = false)
    {
        if ((flags & TransitApproachSuppressionFlags.Boarding) != 0)
        {
            return new BusPrioritySuppressionDecision(true, BusPrioritySuppressionReason.Boarding);
        }

        bool isArriving = (flags & TransitApproachSuppressionFlags.Arriving) != 0;
        bool requiresStop = (flags & TransitApproachSuppressionFlags.RequireStop) != 0;
        if (!isArriving && !requiresStop)
        {
            return new BusPrioritySuppressionDecision(false, BusPrioritySuppressionReason.None);
        }

        bool isRequireStopOnlySample = !isArriving && requiresStop;
        if (stopRelation == BusStopRelation.Unknown && isRequireStopOnlySample)
        {
            return isDedicatedBusApproach || isVehicleMoving
                ? new BusPrioritySuppressionDecision(false, BusPrioritySuppressionReason.None)
                : new BusPrioritySuppressionDecision(true, BusPrioritySuppressionReason.UnknownStopRelation);
        }

        return stopRelation switch
        {
            BusStopRelation.NearSideBeforeSignal => new BusPrioritySuppressionDecision(
                true,
                BusPrioritySuppressionReason.NearSideStop),
            BusStopRelation.Unknown => new BusPrioritySuppressionDecision(
                true,
                BusPrioritySuppressionReason.UnknownStopRelation),
            _ => new BusPrioritySuppressionDecision(false, BusPrioritySuppressionReason.None),
        };
    }
}
