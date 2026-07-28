using Game.Modding;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests.Packaging;

public sealed class ModEntryPointTests
{
    [Fact]
    public void ManagedAssembliesExposeOnlyTheTrafficLightsEnhancementModEntryPoint()
    {
        var trafficLightsEnhancementMod = typeof(C2VM.TrafficLightsEnhancement.Mod);
        var laneSystemAssembly = typeof(C2VM.CommonLibraries.LaneSystem.CustomLaneDirection).Assembly;
        var laneSystemMod = laneSystemAssembly.GetType("C2VM.CommonLibraries.LaneSystem.Mod", throwOnError: false);

        Assert.True(typeof(IMod).IsAssignableFrom(trafficLightsEnhancementMod));
        Assert.Null(laneSystemMod);
    }
}
