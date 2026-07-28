using System;
using System.Linq;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests.Settings;

public sealed class VersionSettingsTests
{
    [Fact]
    public void VersionGroupExposesOnlyTrafficLightsEnhancementVersions()
    {
        string[] visibleStringProperties = typeof(C2VM.TrafficLightsEnhancement.Settings)
            .GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .Where(property => property.GetCustomAttributesData().Any(attribute =>
                attribute.AttributeType.Name == "SettingsUISectionAttribute"
                && attribute.ConstructorArguments.Any(argument =>
                    Equals(argument.Value, C2VM.TrafficLightsEnhancement.Settings.kGroupVersion))))
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            ["m_ReleaseChannel", "m_TleVersion"],
            visibleStringProperties);
    }
}
