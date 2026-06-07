namespace TrafficLightsEnhancement.Logic.Tsp;

public static class TspSourcePriority
{
    public static int GetPriority(TspSource source)
    {
        return source switch
        {
            TspSource.Track => 2,
            TspSource.PublicCar => 1,
            _ => 0,
        };
    }

    public static bool IsPreferredRequest(TspRequest candidateRequest, TspRequest existingRequest)
    {
        int candidatePriority = GetPriority(candidateRequest.Source);
        int existingPriority = GetPriority(existingRequest.Source);

        if (candidatePriority != existingPriority)
        {
            return candidatePriority > existingPriority;
        }

        if (candidateRequest.Strength != existingRequest.Strength)
        {
            return candidateRequest.Strength > existingRequest.Strength;
        }

        // Same source priority and strength: prefer a dedicated-lane request so a bus
        // on a marked bus lane keeps its aggressive eligibility over a tied mixed-lane
        // bus regardless of sublane scan order.
        return candidateRequest.OnDedicatedLane && !existingRequest.OnDedicatedLane;
    }
}
