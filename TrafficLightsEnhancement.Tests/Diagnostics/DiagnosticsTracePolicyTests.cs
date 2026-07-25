using TrafficLightsEnhancement.Logic.Diagnostics;
using Xunit;

namespace TrafficLightsEnhancement.Tests.Diagnostics;

public sealed class DiagnosticsTracePolicyTests
{
    [Fact]
    public void Group_member_state_change_is_recorded_without_tsp_activity()
    {
        bool shouldRecord = DiagnosticsTracePolicy.ShouldRecordStateChange(
            hasTspActivity: false,
            hadTspActivity: false,
            isTrafficGroupMember: true);

        Assert.True(shouldRecord);
    }
}
