namespace TrafficLightsEnhancement.Logic.Tsp;

public enum TspSource : byte
{
    None = 0,
    Track = 1,
    PublicCar = 2,
}

public struct PhaseScore
{
    public PhaseScore(int phaseIndex, int basePriority, float weightedWaiting, bool servesTrack, bool servesPublicCar)
    {
        PhaseIndex = phaseIndex;
        BasePriority = basePriority;
        WeightedWaiting = weightedWaiting;
        ServesTrack = servesTrack;
        ServesPublicCar = servesPublicCar;
    }

    public int PhaseIndex { get; }
    public int BasePriority { get; }
    public float WeightedWaiting { get; }
    public bool ServesTrack { get; }
    public bool ServesPublicCar { get; }
}

public struct TspRequest
{
    public TspRequest(TspSource source, float strength, bool extensionEligible, bool onDedicatedLane = false)
    {
        Source = source;
        Strength = strength;
        ExtensionEligible = extensionEligible;
        OnDedicatedLane = onDedicatedLane;
    }

    public TspSource Source { get; }
    public float Strength { get; }
    public bool ExtensionEligible { get; }
    public bool OnDedicatedLane { get; }
}

public struct TspDecision
{
    public TspDecision(int nextPhaseIndex, bool canExtendCurrent)
    {
        NextPhaseIndex = nextPhaseIndex;
        CanExtendCurrent = canExtendCurrent;
    }

    public int NextPhaseIndex { get; }
    public bool CanExtendCurrent { get; }
}
