namespace TrafficLightsEnhancement.Logic.Diagnostics;

public static class DiagnosticsTracePolicy
{
    public static bool ShouldRecordStateChange(
        bool hasTspActivity,
        bool hadTspActivity,
        bool isTrafficGroupMember)
    {
        return isTrafficGroupMember || hasTspActivity || hadTspActivity;
    }
}
