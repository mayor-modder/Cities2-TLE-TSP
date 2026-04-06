namespace TrafficLightsEnhancement.Logic.Tsp;

public struct TransitSignalPrioritySettings
{
    public const ushort DefaultRequestHorizonTicks = 10;
    public const ushort LegacyDefaultRequestHorizonTicks = 120;

    public bool m_Enabled;
    public bool m_AllowTrackRequests;
    public bool m_AllowPublicCarRequests;
    public bool m_AllowGroupPropagation;
    public ushort m_RequestHorizonTicks;
    public ushort m_MaxGreenExtensionTicks;

    public TransitSignalPrioritySettings()
    {
        m_Enabled = false;
        m_AllowTrackRequests = true;
        m_AllowPublicCarRequests = true;
        m_AllowGroupPropagation = true;
        m_RequestHorizonTicks = DefaultRequestHorizonTicks;
        m_MaxGreenExtensionTicks = 45;
    }
}
