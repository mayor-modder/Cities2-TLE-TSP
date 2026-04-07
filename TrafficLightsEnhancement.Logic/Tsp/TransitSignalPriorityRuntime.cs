namespace TrafficLightsEnhancement.Logic.Tsp;

public static class TransitSignalPriorityRuntime
{
    public static bool TryBuildRequestForLane(
        TransitSignalPrioritySettings settings,
        bool isTrackLane,
        bool isPublicCarLane,
        bool hasValidatedBusOccupant,
        out TspRequest request)
    {
        request = default;

        if (!settings.m_Enabled)
        {
            return false;
        }

        if (isTrackLane && settings.m_AllowTrackRequests)
        {
            request = new TspRequest(source: TspSource.Track, strength: 1f, extensionEligible: true);
            return true;
        }

        if (isPublicCarLane && hasValidatedBusOccupant && settings.m_AllowPublicCarRequests)
        {
            request = new TspRequest(source: TspSource.PublicCar, strength: 1f, extensionEligible: true);
            return true;
        }

        return false;
    }
}
