using C2VM.TrafficLightsEnhancement.Components;
using C2VM.TrafficLightsEnhancement.Utils;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests;

public sealed class CustomPhaseUtilsTests
{
    [Fact]
    public void SwapBit_moves_edge_bicycle_go_and_yield_phase_bits()
    {
        var phase = new EdgeGroupMask
        {
            m_Bicycle =
            {
                m_GoGroupMask = 0b_0000_1000,
                m_YieldGroupMask = 0b_0000_0010
            }
        };

        CustomPhaseUtils.SwapBit(ref phase, 3, 1);

        Assert.Equal((ushort)0b_0000_0010, phase.m_Bicycle.m_GoGroupMask);
        Assert.Equal((ushort)0b_0000_1000, phase.m_Bicycle.m_YieldGroupMask);
    }
}
