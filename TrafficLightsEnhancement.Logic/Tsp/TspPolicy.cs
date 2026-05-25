using System;

namespace TrafficLightsEnhancement.Logic.Tsp;

public enum TspAvailabilityReason : byte
{
    None = 0,
    Disabled = 1,
    GroupedIntersection = 2,
}

public readonly struct TspAvailability : IEquatable<TspAvailability>
{
    public TspAvailability(bool isRuntimeEligible, TspAvailabilityReason reason)
    {
        IsRuntimeEligible = isRuntimeEligible;
        Reason = reason;
    }

    public bool IsRuntimeEligible { get; }
    public TspAvailabilityReason Reason { get; }

    public bool Equals(TspAvailability other)
    {
        return IsRuntimeEligible == other.IsRuntimeEligible
            && Reason == other.Reason;
    }

    public override bool Equals(object? obj)
    {
        return obj is TspAvailability other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((IsRuntimeEligible ? 1 : 0) * 397) ^ (int)Reason;
        }
    }

    public static bool operator ==(TspAvailability left, TspAvailability right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TspAvailability left, TspAvailability right)
    {
        return !left.Equals(right);
    }
}

public static class TspPolicy
{
    public static ushort GetEffectiveRequestHorizonTicks(ushort configuredRequestHorizonTicks)
    {
        return TransitSignalPrioritySettings.NormalizeRequestHorizonTicks(configuredRequestHorizonTicks);
    }

    public static ushort GetEffectiveMaxGreenExtensionTicks(ushort configuredMaxGreenExtensionTicks)
    {
        return TransitSignalPrioritySettings.NormalizeMaxGreenExtensionTicks(configuredMaxGreenExtensionTicks);
    }

    public static bool ShouldBuildApproachIndex(bool hasTransitSignalPrioritySettings)
    {
        return ShouldBuildApproachIndex(
            hasTransitSignalPrioritySettings,
            hasApproachIndexEligibleTransitSignalPrioritySettings: hasTransitSignalPrioritySettings);
    }

    public static bool ShouldBuildApproachIndex(
        bool hasTransitSignalPrioritySettings,
        bool hasApproachIndexEligibleTransitSignalPrioritySettings)
    {
        return hasTransitSignalPrioritySettings && hasApproachIndexEligibleTransitSignalPrioritySettings;
    }

    public static bool IsApproachIndexEligibleSetting(
        TransitSignalPrioritySettings settings,
        bool isGroupedIntersection)
    {
        return settings.m_Enabled
            && settings.m_AllowTrackRequests
            && !isGroupedIntersection;
    }

    public static bool IsBusApproachIndexEligibleSetting(
        TransitSignalPrioritySettings settings,
        bool isGroupedIntersection)
    {
        return settings.m_Enabled
            && settings.m_AllowPublicCarRequests
            && !isGroupedIntersection;
    }

    public static TspAvailability GetAvailability(
        TransitSignalPrioritySettings? settings,
        bool isGroupedIntersection)
    {
        if (!settings.HasValue)
        {
            return new TspAvailability(false, TspAvailabilityReason.Disabled);
        }

        return GetAvailability(settings.Value, isGroupedIntersection);
    }

    public static TspAvailability GetAvailability(
        TransitSignalPrioritySettings settings,
        bool isGroupedIntersection)
    {
        if (!settings.m_Enabled)
        {
            return new TspAvailability(false, TspAvailabilityReason.Disabled);
        }

        if (isGroupedIntersection)
        {
            return new TspAvailability(false, TspAvailabilityReason.GroupedIntersection);
        }

        return new TspAvailability(true, TspAvailabilityReason.None);
    }

    public static bool HasPersistedUserValue(TransitSignalPrioritySettings? settings)
    {
        if (!settings.HasValue)
        {
            return false;
        }

        return HasPersistedUserValue(settings.Value);
    }

    public static bool HasPersistedUserValue(TransitSignalPrioritySettings settings)
    {
        TransitSignalPrioritySettings defaults = new();
        return settings.m_Enabled != defaults.m_Enabled
            || settings.m_AllowTrackRequests != defaults.m_AllowTrackRequests
            || settings.m_AllowPublicCarRequests != defaults.m_AllowPublicCarRequests
            || GetEffectiveRequestHorizonTicks(settings.m_RequestHorizonTicks) != defaults.m_RequestHorizonTicks
            || GetEffectiveMaxGreenExtensionTicks(settings.m_MaxGreenExtensionTicks) != defaults.m_MaxGreenExtensionTicks;
    }
}
