namespace TrafficLightsEnhancement.Logic.Tsp;

public enum TspAvailabilityReason : byte
{
    None = 0,
    Disabled = 1,
    GroupedIntersection = 2,
}

public readonly struct TspAvailability
{
    public TspAvailability(bool isRuntimeEligible, TspAvailabilityReason reason)
    {
        IsRuntimeEligible = isRuntimeEligible;
        Reason = reason;
    }

    public bool IsRuntimeEligible { get; }
    public TspAvailabilityReason Reason { get; }
}

public static class TspPolicy
{
    public static ushort GetEffectiveRequestHorizonTicks(ushort configuredRequestHorizonTicks)
    {
        return configuredRequestHorizonTicks == TransitSignalPrioritySettings.LegacyDefaultRequestHorizonTicks
            ? TransitSignalPrioritySettings.DefaultRequestHorizonTicks
            : configuredRequestHorizonTicks;
    }

    public static TspAvailability GetAvailability(
        TransitSignalPrioritySettings settings,
        bool isGroupedIntersection)
    {
        if (!settings.m_Enabled)
        {
            return new TspAvailability(false, TspAvailabilityReason.Disabled);
        }

        return new TspAvailability(true, TspAvailabilityReason.None);
    }

    public static bool HasPersistedUserValue(TransitSignalPrioritySettings settings)
    {
        TransitSignalPrioritySettings defaults = new();
        return settings.m_Enabled != defaults.m_Enabled
            || settings.m_AllowTrackRequests != defaults.m_AllowTrackRequests
            || settings.m_AllowPublicCarRequests != defaults.m_AllowPublicCarRequests
            || settings.m_AllowGroupPropagation != defaults.m_AllowGroupPropagation
            || GetEffectiveRequestHorizonTicks(settings.m_RequestHorizonTicks) != defaults.m_RequestHorizonTicks
            || settings.m_MaxGreenExtensionTicks != defaults.m_MaxGreenExtensionTicks;
    }
}
